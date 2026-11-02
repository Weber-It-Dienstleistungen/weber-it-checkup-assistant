
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.6
///
/// Kontrolliertes Löschen einzelner Dateien
/// über Windows-Dateihandles.
///
/// Voraussetzungen:
/// - Elternverzeichnis wurde sicher geöffnet.
/// - Kindeintrag wurde zuvor untersucht.
/// - Erwartete Dateiidentität liegt vor.
///
/// Sicherheitsregeln:
/// - Kindzugriff relativ zum Elternhandle.
/// - Keine Verfolgung von Reparse Points.
/// - Keine rekursive Löschung.
/// - Keine Änderung von Dateiattributen.
/// - Keine Berechtigungsübernahme.
/// - Keine Löschung von Systemdateien.
/// - Keine Löschung schreibgeschützter Dateien.
/// - Dateiidentität vor Löschung vergleichen.
/// - Löschung nur am geöffneten Dateiobjekt.
///
/// Noch keine Einbindung in den
/// produktiven Bereinigungsprozess.
/// </summary>
internal static class WindowsTemporaryFilesHandleDeletion
{
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;

    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;

    private const uint FileOpen = 1;

    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;

    private const int FileDispositionInfoClass = 4;

    internal readonly record struct DeletedFileInfo(
        ulong SizeBytes,
        uint VolumeSerialNumber,
        ulong FileIndex);

    // =============================================
    // DATEI KONTROLLIERT LÖSCHEN
    // =============================================

    /// <summary>
    /// Löscht ausschließlich eine reguläre Datei,
    /// deren Identität zuvor ermittelt wurde.
    ///
    /// Das Elternhandle muss aus der
    /// kontrollierten HandleAccess-Klasse
    /// stammen und geöffnet bleiben.
    ///
    /// Die Methode öffnet den Kindeintrag
    /// erneut relativ zum Elternhandle.
    ///
    /// Entscheidend:
    /// Nach dem Öffnen werden Attribute,
    /// Dateityp und Identität ausschließlich
    /// über dieses eine Handle geprüft.
    ///
    /// Die spätere Löschung verwendet
    /// genau dasselbe Handle.
    ///
    /// Keine Löschung über einen Dateipfad.
    /// </summary>
    internal static DeletedFileInfo DeleteInspectedFile(
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

        ValidateExpectedSnapshot(expectedSnapshot);

        /*
         * Das Dateiobjekt wird genau einmal
         * zum Löschen geöffnet.
         *
         * Gewünschte Rechte:
         * DELETE und READ_ATTRIBUTES.
         *
         * ShareAccess = 0:
         *
         * Die Löschung wird nur versucht,
         * wenn Windows einen exklusiven
         * Zugriff auf die Datei erlaubt.
         *
         * Bereits anderweitig geöffnete
         * Dateien werden konservativ
         * ausgeschlossen.
         */
        using var fileHandle =
            OpenFileForDeletion(
                parentDirectory,
                entryName);

        /*
         * Ab hier keine erneute
         * Auflösung des Dateinamens.
         */
        var actualSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(fileHandle);

        ValidateActualSnapshot(actualSnapshot);

        /*
         * Eine zwischen Untersuchung und
         * Löschöffnung ausgetauschte Datei
         * muss abgewiesen werden.
         */
        if (actualSnapshot.VolumeSerialNumber
                != expectedSnapshot.VolumeSerialNumber
            || actualSnapshot.FileIndex
                != expectedSnapshot.FileIndex)
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die Dateiidentität "
                + "hat sich zwischen Untersuchung "
                + "und Löschversuch verändert.");
        }

        /*
         * Keine erneute Prüfung über
         * File.GetAttributes(dateipfad).
         *
         * Wir verwenden ausschließlich
         * die Attribute des geöffneten
         * Dateiobjekts.
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
                fileHandle,
                FileDispositionInfoClass,
                ref disposition,
                dispositionSize))
        {
            var error =
                Marshal.GetLastWin32Error();

            throw new IOException(
                "Windows hat das Löschen des "
                + "geprüften Dateiobjekts abgelehnt. "
                + "Win32-Fehler: "
                + error,
                CreateWin32HResult((uint)error));
        }

        /*
         * FileDispositionInfo markiert die
         * Datei zum Löschen beim Schließen
         * des entsprechenden Handles.
         *
         * Es werden keine zusätzlichen
         * Dateipfade geöffnet.
         */
        return new DeletedFileInfo(
            actualSnapshot.SizeBytes,
            actualSnapshot.VolumeSerialNumber,
            actualSnapshot.FileIndex);
    }

    // =============================================
    // ERWARTETE DATEIIDENTITÄT PRÜFEN
    // =============================================

    private static void ValidateExpectedSnapshot(
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            snapshot)
    {
        if (snapshot.IsDirectory)
        {
            throw new InvalidOperationException(
                "Das erwartete Objekt ist "
                + "ein Verzeichnis und darf "
                + "nicht als Datei gelöscht werden.");
        }

        if (snapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Ein erwarteter Reparse Point "
                + "darf nicht gelöscht werden.");
        }

        /*
         * Ohne eine belastbare Dateiidentität
         * darf keine Löschentscheidung
         * getroffen werden.
         */
        if (snapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Die ursprüngliche Dateiidentität "
                + "ist nicht eindeutig bestimmbar.");
        }

        if (snapshot.IsSystemProtected)
        {
            throw new InvalidOperationException(
                "Die ursprünglich untersuchte Datei "
                + "ist systemgeschützt.");
        }

        if (snapshot.IsReadOnly)
        {
            throw new InvalidOperationException(
                "Die ursprünglich untersuchte Datei "
                + "ist schreibgeschützt.");
        }
    }

    // =============================================
    // AKTUELLES DATEIOBJEKT PRÜFEN
    // =============================================

    private static void ValidateActualSnapshot(
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            snapshot)
    {
        if (snapshot.IsDirectory)
        {
            throw new InvalidOperationException(
                "Das geöffnete Objekt ist "
                + "ein Verzeichnis.");
        }

        if (snapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Das geöffnete Objekt ist "
                + "ein Reparse Point.");
        }

        if (snapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Die aktuelle Dateiidentität "
                + "ist nicht eindeutig bestimmbar.");
        }

        if (snapshot.IsSystemProtected)
        {
            throw new InvalidOperationException(
                "Die aktuelle Datei ist "
                + "systemgeschützt.");
        }

        if (snapshot.IsReadOnly)
        {
            throw new InvalidOperationException(
                "Die aktuelle Datei ist "
                + "schreibgeschützt.");
        }
    }

    // =============================================
    // DATEI RELATIV ZUM ELTERNHANDLE ÖFFNEN
    // =============================================

    private static SafeFileHandle OpenFileForDeletion(
        SafeFileHandle parentDirectory,
        string entryName)
    {
        var nameLength =
            checked(entryName.Length * sizeof(char));

        if (nameLength > ushort.MaxValue - 2)
        {
            throw new ArgumentException(
                "Der Dateiname ist für "
                + "die native Windows-Schnittstelle "
                + "zu lang.",
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
                        (ushort)nameLength,

                    MaximumLength =
                        (ushort)(nameLength + 2),

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
             * Das Elternhandle bleibt während
             * des nativen Öffnens gültig.
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
             * FILE_NON_DIRECTORY_FILE:
             * Ein Verzeichnis darf hier
             * nicht als Datei geöffnet werden.
             *
             * FILE_OPEN_REPARSE_POINT:
             * Verknüpfung selbst öffnen,
             * nicht deren Ziel.
             *
             * OBJ_DONT_REPARSE:
             * Keine automatische Verfolgung
             * von Reparse Points während
             * der Namensauflösung.
             *
             * ShareAccess = 0:
             * Exklusiver Öffnungsversuch.
             */
            var status =
                NtCreateFile(
                    out var fileHandle,
                    DeleteAccess
                        | ReadAttributes
                        | Synchronize,
                    ref objectAttributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    0,
                    FileOpen,
                    FileNonDirectoryFile
                        | FileSynchronousIoNonAlert
                        | FileOpenReparsePoint,
                    IntPtr.Zero,
                    0);

            if (status < 0)
            {
                fileHandle?.Dispose();

                var win32Error =
                    RtlNtStatusToDosError(status);

                throw new IOException(
                    "Die Datei konnte nicht "
                    + "exklusiv zum Löschen "
                    + "geöffnet werden. "
                    + "NTSTATUS: 0x"
                    + unchecked((uint)status).ToString("X8")
                    + ", Win32-Fehler: "
                    + win32Error,
                    CreateWin32HResult(win32Error));
            }

            if (fileHandle is null
                || fileHandle.IsInvalid)
            {
                fileHandle?.Dispose();

                throw new IOException(
                    "Windows hat kein gültiges "
                    + "Löschhandle zurückgegeben.");
            }

            return fileHandle;
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
    // EINGABEN UND PLATTFORM PRÜFEN
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
                "Ein Löschvorgang erlaubt nur "
                + "einen einzelnen, unveränderten "
                + "Dateinamen ohne Pfadbestandteile.",
                nameof(entryName));
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die handle-basierte Löschung "
                + "wird nur unter Windows unterstützt.");
        }
    }

    private static int CreateWin32HResult(
        uint error)
    {
        return unchecked(
            (int)(0x80070000u | (error & 0xFFFFu)));
    }

    // =============================================
    // NATIVE DATENSTRUKTUREN
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
     * Windows FILE_DISPOSITION_INFO enthält
     * ein BOOLEAN mit genau einem Byte.
     */
    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation
    {
        public byte DeleteFile;
    }

    // =============================================
    // NATIVE WINDOWS-SCHNITTSTELLEN
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
