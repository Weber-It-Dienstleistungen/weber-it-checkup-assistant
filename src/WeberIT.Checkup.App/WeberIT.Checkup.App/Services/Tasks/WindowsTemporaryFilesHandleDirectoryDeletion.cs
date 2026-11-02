
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.11
///
/// Entfernt einen zuvor untersuchten,
/// tatsächlich leeren Unterordner über
/// ein geprüftes Windows-Dateihandle.
///
/// Sicherheitsregeln:
///
/// - Kein Löschen über vollständige Pfade.
/// - Kein rekursives Entfernen.
/// - Kein Verfolgen von Reparse Points.
/// - Kein Ändern von Dateiattributen.
/// - Keine Berechtigungsübernahme.
/// - Keine Administratorrechte anfordern.
/// - Identität vor Löschung erneut prüfen.
/// - Geschützte Ordner erhalten.
/// - Nicht leere Ordner erhalten.
///
/// Die Methode wird noch nicht durch
/// den produktiven Executor aufgerufen.
/// </summary>
internal static class
    WindowsTemporaryFilesHandleDirectoryDeletion
{
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;

    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;

    private const uint FileOpen = 1;

    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;

    private const int FileDispositionInfoClass = 4;

    internal readonly record struct DeletedDirectoryInfo(
        uint VolumeSerialNumber,
        ulong FileIndex);

    // =============================================
    // VERZEICHNIS KONTROLLIERT ENTFERNEN
    // =============================================

    /// <summary>
    /// Entfernt einen einzelnen direkten
    /// Unterordner relativ zu einem bereits
    /// geöffneten Elternverzeichnis.
    ///
    /// Das erwartete Snapshot stammt aus
    /// einer vorherigen Untersuchung.
    ///
    /// Der geöffnete Elternhandle muss
    /// während der Operation gültig bleiben.
    ///
    /// Windows entscheidet selbst, ob das
    /// Verzeichnis tatsächlich leer ist.
    /// </summary>
    internal static DeletedDirectoryInfo
        DeleteInspectedEmptyDirectory(
            SafeFileHandle parentDirectory,
            string entryName,
            WindowsTemporaryFilesHandleAccess.EntrySnapshot
                expectedSnapshot)
    {
        EnsureWindows();

        ArgumentNullException.ThrowIfNull(
            parentDirectory);

        if (parentDirectory.IsClosed
            || parentDirectory.IsInvalid)
        {
            throw new InvalidOperationException(
                "Das Elternverzeichnis besitzt "
                + "kein gültiges Handle.");
        }

        ValidateEntryName(entryName);

        var parentSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(parentDirectory);

        if (!parentSnapshot.IsDirectory
            || parentSnapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Das Elternhandle verweist nicht "
                + "auf ein reguläres Verzeichnis.");
        }

        ValidateExpectedSnapshot(
            expectedSnapshot);

        /*
         * Ausschließlich der einzelne
         * Kindeintrag wird relativ zum
         * bereits geöffneten Elternhandle
         * erneut geöffnet.
         *
         * Noch kein Löschversuch.
         */
        using var directoryHandle =
            OpenDirectoryForDeletion(
                parentDirectory,
                entryName);

        /*
         * Ab hier wird der Dateiname
         * nicht erneut aufgelöst.
         */
        var actualSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(directoryHandle);

        ValidateActualSnapshot(
            actualSnapshot);

        /*
         * Wenn das Verzeichnis zwischen
         * erster Untersuchung und dieser
         * Öffnung ausgetauscht wurde,
         * wird der Löschversuch verweigert.
         */
        if (actualSnapshot.VolumeSerialNumber
                != expectedSnapshot.VolumeSerialNumber
            || actualSnapshot.FileIndex
                != expectedSnapshot.FileIndex)
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die "
                + "Verzeichnisidentität hat sich "
                + "zwischen Untersuchung und "
                + "Löschversuch verändert.");
        }

        /*
         * Windows bekommt ausschließlich
         * das bereits geprüfte Handle.
         *
         * Keine rekursive Löschung.
         *
         * Ist der Ordner nicht leer,
         * muss Windows den Vorgang ablehnen.
         */
        var disposition =
            new FileDispositionInformation
            {
                DeleteFile = 1
            };

        var dispositionSize =
            (uint)Marshal.SizeOf<
                FileDispositionInformation>();

        if (!SetFileInformationByHandle(
                directoryHandle,
                FileDispositionInfoClass,
                ref disposition,
                dispositionSize))
        {
            var error =
                Marshal.GetLastWin32Error();

            throw new IOException(
                "Windows hat das Entfernen "
                + "des geprüften Verzeichnisses "
                + "abgelehnt. Win32-Fehler: "
                + error,
                CreateWin32HResult((uint)error));
        }

        /*
         * Die Löschvormerkung wird beim
         * Schließen des Handles wirksam.
         *
         * using var stellt sicher, dass
         * der Handle beim Verlassen dieser
         * Methode freigegeben wird.
         */
        return new DeletedDirectoryInfo(
            actualSnapshot.VolumeSerialNumber,
            actualSnapshot.FileIndex);
    }

    // =============================================
    // ERWARTETEN VERZEICHNISZUSTAND PRÜFEN
    // =============================================

    private static void ValidateExpectedSnapshot(
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            snapshot)
    {
        if (!snapshot.IsDirectory)
        {
            throw new InvalidOperationException(
                "Das ursprünglich untersuchte "
                + "Objekt ist kein Verzeichnis.");
        }

        if (snapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Ein Reparse Point darf nicht "
                + "als Verzeichnis entfernt werden.");
        }

        if (snapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Die ursprüngliche "
                + "Verzeichnisidentität fehlt.");
        }

        if (snapshot.IsSystemProtected)
        {
            throw new InvalidOperationException(
                "Das ursprünglich untersuchte "
                + "Verzeichnis ist systemgeschützt.");
        }

        if (snapshot.IsReadOnly)
        {
            throw new InvalidOperationException(
                "Das ursprünglich untersuchte "
                + "Verzeichnis ist schreibgeschützt.");
        }
    }

    // =============================================
    // TATSÄCHLICH GEÖFFNETES OBJEKT PRÜFEN
    // =============================================

    private static void ValidateActualSnapshot(
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            snapshot)
    {
        if (!snapshot.IsDirectory)
        {
            throw new InvalidOperationException(
                "Das geöffnete Objekt ist "
                + "kein Verzeichnis.");
        }

        if (snapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Das geöffnete Verzeichnis "
                + "ist ein Reparse Point.");
        }

        if (snapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Die aktuelle "
                + "Verzeichnisidentität fehlt.");
        }

        if (snapshot.IsSystemProtected)
        {
            throw new InvalidOperationException(
                "Das geöffnete Verzeichnis "
                + "ist systemgeschützt.");
        }

        if (snapshot.IsReadOnly)
        {
            throw new InvalidOperationException(
                "Das geöffnete Verzeichnis "
                + "ist schreibgeschützt.");
        }
    }

    // =============================================
    // UNTERORDNER RELATIV ÖFFNEN
    // =============================================

    private static SafeFileHandle
        OpenDirectoryForDeletion(
            SafeFileHandle parentDirectory,
            string entryName)
    {
        var nameLength =
            checked(entryName.Length * sizeof(char));

        if (nameLength > ushort.MaxValue - 2)
        {
            throw new ArgumentException(
                "Der Verzeichnisname ist für "
                + "die native Windows-Schnittstelle "
                + "zu lang.",
                nameof(entryName));
        }

        var nameBuffer = IntPtr.Zero;
        var nameStructurePointer = IntPtr.Zero;

        var parentReferenceAdded = false;

        try
        {
            nameBuffer =
                Marshal.StringToHGlobalUni(
                    entryName);

            var unicodeName =
                new UnicodeString
                {
                    Length = (ushort)nameLength,

                    MaximumLength =
                        (ushort)(nameLength + 2),

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
             * Während NtCreateFile läuft,
             * darf das Elternhandle nicht
             * freigegeben werden.
             */
            parentDirectory.DangerousAddRef(
                ref parentReferenceAdded);

            var objectAttributes =
                new ObjectAttributes
                {
                    Length =
                        (uint)Marshal.SizeOf<
                            ObjectAttributes>(),

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
             * Es wird bewusst kein
             * FILE_DIRECTORY_FILE erzwungen.
             *
             * Stattdessen öffnen wir das
             * Objekt ohne Verfolgung von
             * Reparse Points und prüfen
             * anschließend seinen Typ
             * am zurückgegebenen Handle.
             *
             * Eine versehentlich an dieser
             * Stelle befindliche Datei
             * wird dadurch abgewiesen.
             *
             * ShareAccess = 0:
             * Konservativer exklusiver Zugriff.
             */
            var status =
                NtCreateFile(
                    out var directoryHandle,
                    DeleteAccess
                        | ReadAttributes
                        | Synchronize,
                    ref objectAttributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    0,
                    FileOpen,
                    FileSynchronousIoNonAlert
                        | FileOpenReparsePoint,
                    IntPtr.Zero,
                    0);

            if (status < 0)
            {
                directoryHandle?.Dispose();

                var win32Error =
                    RtlNtStatusToDosError(status);

                throw new IOException(
                    "Das Verzeichnis konnte nicht "
                    + "exklusiv zum Entfernen "
                    + "geöffnet werden. "
                    + "NTSTATUS: 0x"
                    + unchecked((uint)status).ToString("X8")
                    + ", Win32-Fehler: "
                    + win32Error,
                    CreateWin32HResult(win32Error));
            }

            if (directoryHandle is null
                || directoryHandle.IsInvalid)
            {
                directoryHandle?.Dispose();

                throw new IOException(
                    "Windows hat kein gültiges "
                    + "Verzeichnishandle zurückgegeben.");
            }

            return directoryHandle;
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
                + "einzelner Verzeichnisname "
                + "ohne Pfadbestandteile zulässig.",
                nameof(entryName));
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die handle-basierte "
                + "Verzeichnislöschung ist "
                + "nur unter Windows verfügbar.");
        }
    }

    private static int CreateWin32HResult(
        uint error)
    {
        return unchecked(
            (int)(0x80070000u | (error & 0xFFFFu)));
    }

    // =============================================
    // NATIVE WINDOWS-STRUKTUREN
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
    private struct FileDispositionInformation
    {
        public byte DeleteFile;
    }

    // =============================================
    // NATIVE WINDOWS-API
    // =============================================

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

    [DllImport(
        "kernel32.dll",
        EntryPoint = "SetFileInformationByHandle",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool
        SetFileInformationByHandle(
            SafeFileHandle fileHandle,
            int fileInformationClass,
            ref FileDispositionInformation information,
            uint bufferSize);
}
