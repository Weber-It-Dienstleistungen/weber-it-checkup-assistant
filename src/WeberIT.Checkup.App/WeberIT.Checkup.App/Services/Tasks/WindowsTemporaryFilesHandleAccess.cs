
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.1a
///
/// Grundlage für eine handle-basierte
/// Windows-Temp-Bereinigung.
///
/// Sicherheitsprinzipien:
///
/// - Verzeichnisse als Handles öffnen.
/// - Kindeinträge nur relativ zum
///   geöffneten Elternverzeichnis öffnen.
/// - Keine Verfolgung von Reparse Points.
/// - Attribute am geöffneten Objekt lesen.
/// - Keine Pfade für Kindzugriffe zusammensetzen.
/// - Keine Dateien erstellen oder löschen.
/// - Keine Berechtigungen verändern.
///
/// Diese Klasse führt noch keine
/// Bereinigung durch.
/// </summary>
internal static class WindowsTemporaryFilesHandleAccess
{
    private const uint FileListDirectory = 0x00000001;
    private const uint FileReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;

    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;

    private const uint OpenExisting = 3;

    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;

    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;

    private const uint NtFileOpen = 1;
    private const uint NtFileSynchronousIoNonAlert = 0x00000020;
    private const uint NtFileOpenReparsePoint = 0x00200000;

    /// <summary>
    /// Eigenschaften eines bereits geöffneten
    /// Dateisystemobjekts.
    ///
    /// VolumeSerialNumber und FileIndex
    /// dienen später zur Identitätsprüfung.
    /// </summary>
    internal readonly record struct EntrySnapshot(
        FileAttributes Attributes,
        uint VolumeSerialNumber,
        ulong FileIndex,
        ulong SizeBytes)
    {
        public bool IsDirectory =>
            (Attributes & FileAttributes.Directory) != 0;

        public bool IsReparsePoint =>
            (Attributes & FileAttributes.ReparsePoint) != 0;

        public bool IsReadOnly =>
            (Attributes & FileAttributes.ReadOnly) != 0;

        public bool IsSystemProtected =>
            (Attributes & FileAttributes.System) != 0;
    }

    // =============================================
    // STAMMVERZEICHNIS ÖFFNEN
    // =============================================

    /// <summary>
    /// Öffnet ein lokales Verzeichnis und
    /// verifiziert das tatsächlich geöffnete
    /// Dateisystemobjekt.
    ///
    /// Diese Methode ist noch nicht fest
    /// an C:\Windows\Temp gebunden, damit
    /// sie zuerst in einer Sandbox
    /// getestet werden kann.
    ///
    /// Die produktive Zielbeschränkung muss
    /// der spätere Worker zusätzlich
    /// ausdrücklich durchsetzen.
    /// </summary>
    internal static SafeFileHandle OpenRootDirectory(
        string directoryPath)
    {
        EnsureWindows();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            directoryPath);

        if (!Path.IsPathFullyQualified(directoryPath)
            || IsNetworkOrDevicePath(directoryPath))
        {
            throw new InvalidOperationException(
                "Es ist ausschließlich ein vollständig "
                + "qualifizierter lokaler Verzeichnispfad "
                + "zulässig.");
        }

        var normalizedPath =
            NormalizeDirectoryPath(
                Path.GetFullPath(directoryPath));

        var rootPath =
            Path.GetPathRoot(normalizedPath);

        if (string.IsNullOrWhiteSpace(rootPath)
            || string.Equals(
                normalizedPath,
                NormalizeDirectoryPath(rootPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ein Laufwerksstamm darf nicht als "
                + "Bereinigungsverzeichnis geöffnet werden.");
        }

        /*
         * OPEN_REPARSE_POINT:
         * Eine Verknüpfung am Ziel selbst
         * wird nicht automatisch verfolgt.
         *
         * BACKUP_SEMANTICS:
         * Erlaubt das Öffnen von Verzeichnissen.
         *
         * FILE_SHARE_DELETE wird bewusst
         * nicht vergeben, um Umbenennungen
         * während der Handle-Lebensdauer
         * möglichst auszuschließen.
         */
        var handle =
            CreateFileW(
                normalizedPath,
                FileListDirectory
                    | FileReadAttributes
                    | Synchronize,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics
                    | FileFlagOpenReparsePoint,
                IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error =
                Marshal.GetLastWin32Error();

            handle.Dispose();

            throw new Win32Exception(
                error,
                "Das Stammverzeichnis konnte "
                + "nicht sicher geöffnet werden.");
        }

        try
        {
            var snapshot =
                ReadSnapshot(handle);

            if (!snapshot.IsDirectory)
            {
                throw new InvalidOperationException(
                    "Das geöffnete Stammobjekt "
                    + "ist kein Verzeichnis.");
            }

            if (snapshot.IsReparsePoint)
            {
                throw new InvalidOperationException(
                    "Das Stammverzeichnis ist "
                    + "ein Reparse Point.");
            }

            /*
             * Der tatsächlich aufgelöste Pfad
             * muss dem erwarteten lokalen
             * Verzeichnis entsprechen.
             *
             * So erkennen wir insbesondere
             * unerwünschte Umleitungen in
             * übergeordneten Pfadbestandteilen.
             */
            var actualPath =
                NormalizeDirectoryPath(
                    GetFinalPath(handle));

            if (!string.Equals(
                normalizedPath,
                actualPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Der tatsächlich geöffnete "
                    + "Verzeichnispfad stimmt nicht "
                    + "mit dem freigegebenen Pfad überein.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    // =============================================
    // EINEN DIREKTEN KINDEINTRAG ÖFFNEN
    // =============================================

    /// <summary>
    /// Öffnet einen direkten Kindeintrag
    /// relativ zum bereits geöffneten
    /// Elternverzeichnis.
    ///
    /// Es ist ausschließlich ein einzelner
    /// Dateiname erlaubt, kein relativer
    /// oder absoluter Pfad.
    ///
    /// Die Methode öffnet mit Leserechten
    /// für Attribute. Sie kann nicht löschen.
    /// </summary>
    internal static SafeFileHandle OpenChildForInspection(
        SafeFileHandle parentDirectory,
        string entryName)
    {
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(
            parentDirectory);

        if (parentDirectory.IsClosed
            || parentDirectory.IsInvalid)
        {
            throw new InvalidOperationException(
                "Das Elternverzeichnis besitzt "
                + "kein gültiges offenes Handle.");
        }

        ValidateEntryName(entryName);

        var unicodeLength =
            checked(entryName.Length * sizeof(char));

        if (unicodeLength > ushort.MaxValue - 2)
        {
            throw new ArgumentException(
                "Der Dateiname ist für die native "
                + "Windows-Schnittstelle zu lang.",
                nameof(entryName));
        }

        var nameBuffer =
            IntPtr.Zero;

        var nameStructurePointer =
            IntPtr.Zero;

        var parentReferenceAdded = false;

        try
        {
            nameBuffer =
                Marshal.StringToHGlobalUni(
                    entryName);

            var unicodeName =
                new UnicodeString
                {
                    Length =
                        (ushort)unicodeLength,

                    MaximumLength =
                        (ushort)(unicodeLength + 2),

                    Buffer =
                        nameBuffer
                };

            nameStructurePointer =
                Marshal.AllocHGlobal(
                    Marshal.SizeOf<UnicodeString>());

            Marshal.StructureToPtr(
                unicodeName,
                nameStructurePointer,
                false);

            /*
             * Elternhandle während NtCreateFile
             * ausdrücklich gegen Dispose
             * absichern.
             */
            parentDirectory.DangerousAddRef(
                ref parentReferenceAdded);

            var objectAttributes =
                new ObjectAttributes
                {
                    Length =
                        (uint)Marshal.SizeOf<ObjectAttributes>(),

                    RootDirectory =
                        parentDirectory.DangerousGetHandle(),

                    ObjectName =
                        nameStructurePointer,

                    Attributes =
                        ObjectCaseInsensitive
                        | ObjectDontReparse,

                    SecurityDescriptor =
                        IntPtr.Zero,

                    SecurityQualityOfService =
                        IntPtr.Zero
                };

            /*
             * NtCreateFile erhält nur den
             * einzelnen Dateinamen.
             *
             * Das bereits geöffnete
             * Elternverzeichnis definiert
             * den Bezugspunkt.
             *
             * FILE_OPEN_REPARSE_POINT und
             * OBJ_DONT_REPARSE verhindern,
             * dass eine Verknüpfung einfach
             * als Zielverzeichnis betreten wird.
             */
            var status =
                NtCreateFile(
                    out var childHandle,
                    FileReadAttributes | Synchronize,
                    ref objectAttributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    FileShareRead | FileShareWrite,
                    NtFileOpen,
                    NtFileSynchronousIoNonAlert
                        | NtFileOpenReparsePoint,
                    IntPtr.Zero,
                    0);

            if (status < 0)
            {
                childHandle?.Dispose();

                throw new IOException(
                    "Der Kindeintrag konnte nicht "
                    + "sicher relativ zum geöffneten "
                    + "Elternverzeichnis geöffnet werden. "
                    + "NTSTATUS: 0x"
                    + unchecked((uint)status).ToString("X8"));
            }

            if (childHandle is null
                || childHandle.IsInvalid)
            {
                childHandle?.Dispose();

                throw new IOException(
                    "Windows lieferte kein gültiges "
                    + "Handle für den Kindeintrag.");
            }

            try
            {
                var snapshot =
                    ReadSnapshot(childHandle);

                /*
                 * Auch wenn Windows den
                 * Reparse Point selbst
                 * geöffnet hat, darf er
                 * nicht weiterverarbeitet werden.
                 */
                if (snapshot.IsReparsePoint)
                {
                    throw new InvalidOperationException(
                        "Der Kindeintrag ist ein "
                        + "Reparse Point und wird "
                        + "nicht betreten.");
                }

                return childHandle;
            }
            catch
            {
                childHandle.Dispose();
                throw;
            }
        }
        finally
        {
            if (parentReferenceAdded)
            {
                parentDirectory.DangerousRelease();
            }

            if (nameStructurePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    nameStructurePointer);
            }

            if (nameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    nameBuffer);
            }
        }
    }

    // =============================================
    // OBJEKTINFORMATIONEN AUS HANDLE LESEN
    // =============================================

    internal static EntrySnapshot ReadSnapshot(
        SafeFileHandle handle)
    {
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(handle);

        if (handle.IsClosed || handle.IsInvalid)
        {
            throw new InvalidOperationException(
                "Die Dateiinformationen können "
                + "nur über ein gültiges Handle "
                + "abgerufen werden.");
        }

        if (!GetFileInformationByHandle(
                handle,
                out var information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Die Dateiinformationen konnten "
                + "nicht über das Handle gelesen werden.");
        }

        var fileIndex =
            ((ulong)information.FileIndexHigh << 32)
            | information.FileIndexLow;

        var fileSize =
            ((ulong)information.FileSizeHigh << 32)
            | information.FileSizeLow;

        return new EntrySnapshot(
            (FileAttributes)information.FileAttributes,
            information.VolumeSerialNumber,
            fileIndex,
            fileSize);
    }

    // =============================================
    // TATSÄCHLICH GEÖFFNETEN PFAD LESEN
    // =============================================

    private static string GetFinalPath(
        SafeFileHandle handle)
    {
        var buffer =
            new StringBuilder(512);

        var length =
            GetFinalPathNameByHandleW(
                handle,
                buffer,
                (uint)buffer.Capacity,
                0);

        if (length == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Der tatsächliche Verzeichnispfad "
                + "konnte nicht ermittelt werden.");
        }

        if (length >= buffer.Capacity)
        {
            buffer =
                new StringBuilder(
                    checked((int)length + 1));

            length =
                GetFinalPathNameByHandleW(
                    handle,
                    buffer,
                    (uint)buffer.Capacity,
                    0);

            if (length == 0
                || length >= buffer.Capacity)
            {
                throw new IOException(
                    "Der tatsächliche Verzeichnispfad "
                    + "konnte nicht vollständig gelesen werden.");
            }
        }

        var finalPath =
            buffer.ToString();

        /*
         * Windows verwendet normalerweise
         * das erweiterte Präfix \\?\.
         */
        if (finalPath.StartsWith(
                @"\\?\",
                StringComparison.Ordinal))
        {
            finalPath = finalPath[4..];
        }

        if (IsNetworkOrDevicePath(finalPath))
        {
            throw new InvalidOperationException(
                "Der geöffnete Ordner wurde "
                + "auf einen nicht zulässigen "
                + "Netzwerk- oder Gerätepfad aufgelöst.");
        }

        return finalPath;
    }

    // =============================================
    // EINGABEN VALIDIEREN
    // =============================================

    private static void ValidateEntryName(
        string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entryName);

        /*
         * Sprint 55.14.1a:
         *
         * EndsWith(string, StringComparison)
         * statt einer ungültigen Kombination
         * aus char und StringComparison.
         */
        if (entryName is "." or ".."
            || entryName.Contains('\\')
            || entryName.Contains('/')
            || entryName.Contains(':')
            || entryName.EndsWith(
                ".",
                StringComparison.Ordinal)
            || entryName.EndsWith(
                " ",
                StringComparison.Ordinal)
            || entryName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Es ist ausschließlich ein "
                + "einzelner, unveränderter "
                + "Windows-Dateiname zulässig.",
                nameof(entryName));
        }
    }

    private static string NormalizeDirectoryPath(
        string path)
    {
        return path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static bool IsNetworkOrDevicePath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Der sichere Dateizugriff "
                + "wird ausschließlich "
                + "unter Windows unterstützt.");
        }
    }

    // =============================================
    // NATIVE WINDOWS-DATENSTRUKTUREN
    // =============================================

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public uint Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    /*
     * Alle FILETIME-Felder werden bewusst
     * als jeweils zwei DWORDs dargestellt.
     *
     * Damit entspricht das Layout exakt
     * BY_HANDLE_FILE_INFORMATION, ohne
     * plattformabhängige long-Ausrichtung.
     */
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileInformation
    {
        public uint FileAttributes;

        public uint CreationTimeLow;
        public uint CreationTimeHigh;

        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;

        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;

        public uint VolumeSerialNumber;

        public uint FileSizeHigh;
        public uint FileSizeLow;

        public uint NumberOfLinks;

        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    // =============================================
    // NATIVE WINDOWS-SCHNITTSTELLEN
    // =============================================

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out NativeFileInformation information);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle fileHandle,
        StringBuilder filePath,
        uint bufferLength,
        uint flags);

    [DllImport(
        "ntdll.dll",
        ExactSpelling = true)]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr eaBuffer,
        uint eaLength);
}
