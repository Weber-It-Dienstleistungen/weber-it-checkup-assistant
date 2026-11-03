
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.21
///
/// Handle-basierter Windows-Dateisystemzugriff.
///
/// Sicherheitsregeln:
/// - Stammverzeichnis kontrolliert öffnen.
/// - Kindeinträge relativ zum Elternhandle öffnen.
/// - Keine Reparse Points verfolgen.
/// - Dateiidentität über Handles ermitteln.
/// - Keine Dateien löschen.
/// - Keine Berechtigungen verändern.
///
/// Neu:
/// Native NTSTATUS-Fehler werden in Win32-
/// Fehlercodes und passende HRESULT-Werte
/// übersetzt.
///
/// Damit kann die Bereinigungs-Engine
/// Dateisperren, fehlende Dateien und
/// technische Fehler unterscheiden.
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

    // =============================================
    // DATEISYSTEM-SNAPSHOT
    // =============================================

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
         * BACKUP_SEMANTICS erlaubt das Öffnen
         * von Verzeichnissen.
         *
         * OPEN_REPARSE_POINT verhindert das
         * automatische Verfolgen des Zielobjekts.
         *
         * FILE_SHARE_DELETE wird bewusst
         * nicht freigegeben.
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
             * Auch übergeordnete Pfadumleitungen
             * dürfen nicht unbemerkt zu einem
             * anderen Ziel führen.
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
    // KINDEINTRAG RELATIV ÖFFNEN
    // =============================================

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

        var nameBuffer = IntPtr.Zero;
        var nameStructurePointer = IntPtr.Zero;
        var parentReferenceAdded = false;

        try
        {
            nameBuffer =
                Marshal.StringToHGlobalUni(entryName);

            var unicodeName =
                new UnicodeString
                {
                    Length = (ushort)unicodeLength,
                    MaximumLength =
                        (ushort)(unicodeLength + 2),
                    Buffer = nameBuffer
                };

            nameStructurePointer =
                Marshal.AllocHGlobal(
                    Marshal.SizeOf<UnicodeString>());

            Marshal.StructureToPtr(
                unicodeName,
                nameStructurePointer,
                false);

            /*
             * Das Elternhandle muss während
             * der nativen Windows-Operation
             * geöffnet bleiben.
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
             * NtCreateFile erhält ausschließlich
             * den Dateinamen und den zuvor
             * geöffneten Elternhandle.
             *
             * Keine neue Auflösung eines
             * vollständigen Dateipfads.
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

                /*
                 * Sprint 55.14.21:
                 *
                 * Der bisherige Code erzeugte
                 * eine IOException ohne den
                 * eigentlichen Win32-Fehlercode.
                 *
                 * Dadurch war die Ursache für
                 * die Engine nicht auswertbar.
                 *
                 * Wir übersetzen den NTSTATUS
                 * jetzt ausdrücklich.
                 */
                var win32Error =
                    RtlNtStatusToDosError(status);

                throw new IOException(
                    "Der Kindeintrag konnte nicht "
                    + "sicher relativ zum geöffneten "
                    + "Elternverzeichnis geöffnet werden. "
                    + "NTSTATUS: 0x"
                    + unchecked((uint)status).ToString("X8")
                    + ", Win32-Fehler: "
                    + win32Error,
                    CreateWin32HResult(win32Error));
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
                Marshal.FreeHGlobal(nameBuffer);
            }
        }
    }

    // =============================================
    // DATEIINFORMATIONEN ÜBER HANDLE LESEN
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
    // TATSÄCHLICH GEÖFFNETEN PFAD ERMITTELN
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
    // WINDOWS-FEHLERCODES ÜBERSETZEN
    // =============================================

    /// <summary>
    /// Erstellt ein HRESULT mit dem angegebenen
    /// Windows-Fehlercode.
    ///
    /// Beispiele:
    ///
    /// ERROR_FILE_NOT_FOUND    -> 0x80070002
    /// ERROR_SHARING_VIOLATION -> 0x80070020
    ///
    /// Die Engine kann den Win32-Code
    /// anschließend über die unteren
    /// 16 Bits des HRESULT auslesen.
    /// </summary>
    private static int CreateWin32HResult(
        uint error)
    {
        return unchecked(
            (int)(0x80070000u | (error & 0xFFFFu)));
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

    [DllImport(
        "ntdll.dll",
        ExactSpelling = true)]
    private static extern uint RtlNtStatusToDosError(
        int status);
}
