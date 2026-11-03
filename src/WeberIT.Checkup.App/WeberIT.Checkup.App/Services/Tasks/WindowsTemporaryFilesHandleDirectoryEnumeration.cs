
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.15
///
/// Sichere Verzeichnisdurchsuchung über
/// bereits geöffnete Windows-Handles.
///
/// - Keine pfadbasierte Aufzählung.
/// - Keine Verfolgung von Reparse Points.
/// - Unterordner relativ zum Elternhandle öffnen.
/// - Identität geöffneter Ordner kontrollieren.
/// - Keine Dateien oder Ordner löschen.
/// - Keine Berechtigungen verändern.
/// - Abbruch über CancellationToken.
/// - Begrenzte Anzahl von Einträgen.
///
/// Noch keine produktive Integration.
/// </summary>
internal static class
    WindowsTemporaryFilesHandleDirectoryEnumeration
{
    private const uint FileListDirectory = 0x00000001;
    private const uint FileReadAttributes = 0x00000080;
    private const uint Synchronize = 0x00100000;

    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;

    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;

    private const uint FileOpen = 1;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;

    private const int FileFullDirectoryInfo = 14;
    private const int FileFullDirectoryRestartInfo = 15;

    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;

    private const int BufferSize = 64 * 1024;
    private const int EntryHeaderSize = 68;

    private const int MaximumDirectoryEntries = 100000;

    /// <summary>
    /// Vorläufige Information aus der
    /// Verzeichnisaufzählung.
    ///
    /// Die Attribute dürfen später NICHT
    /// allein als Löschfreigabe dienen.
    ///
    /// Der betreffende Eintrag muss vor
    /// jeder Operation erneut über einen
    /// Handle geöffnet und geprüft werden.
    /// </summary>
    internal readonly record struct DirectoryEntry(
        string Name,
        FileAttributes ListedAttributes)
    {
        public bool IsListedDirectory =>
            (ListedAttributes & FileAttributes.Directory) != 0;

        public bool IsListedReparsePoint =>
            (ListedAttributes & FileAttributes.ReparsePoint) != 0;
    }

    // =============================================
    // VERZEICHNIS ÜBER HANDLE AUFLISTEN
    // =============================================

    /// <summary>
    /// Liest ausschließlich die direkten
    /// Einträge eines geöffneten Verzeichnisses.
    ///
    /// Es werden keine Unterordner betreten.
    /// Es wird keine Löschung ausgeführt.
    ///
    /// Das Verzeichnishandle muss mit
    /// geeigneten Aufzählungsrechten
    /// geöffnet worden sein.
    /// </summary>
    internal static IReadOnlyList<DirectoryEntry>
        EnumerateDirectChildren(
            SafeFileHandle directoryHandle,
            CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidateDirectoryHandle(directoryHandle);

        var results =
            new List<DirectoryEntry>();

        var observedNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var buffer =
            Marshal.AllocHGlobal(BufferSize);

        try
        {
            var firstRequest = true;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                /*
                 * Der erste Aufruf startet die
                 * Aufzählung ausdrücklich neu.
                 *
                 * Weitere Aufrufe setzen am
                 * selben Verzeichnishandle fort.
                 */
                var informationClass =
                    firstRequest
                        ? FileFullDirectoryRestartInfo
                        : FileFullDirectoryInfo;

                firstRequest = false;

                var success =
                    GetFileInformationByHandleEx(
                        directoryHandle,
                        informationClass,
                        buffer,
                        BufferSize);

                if (!success)
                {
                    var error =
                        Marshal.GetLastWin32Error();

                    if (error == ErrorNoMoreFiles
                        || error == ErrorHandleEof)
                    {
                        break;
                    }

                    throw new Win32Exception(
                        error,
                        "Die handle-basierte "
                        + "Verzeichnisaufzählung "
                        + "ist fehlgeschlagen.");
                }

                ParseDirectoryBuffer(
                    buffer,
                    results,
                    observedNames,
                    cancellationToken);
            }

            return results;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // =============================================
    // NATIVE AUFZÄHLUNG AUSWERTEN
    // =============================================

    private static void ParseDirectoryBuffer(
        IntPtr buffer,
        List<DirectoryEntry> results,
        HashSet<string> observedNames,
        CancellationToken cancellationToken)
    {
        /*
         * FILE_FULL_DIR_INFO:
         *
         * NextEntryOffset : Offset 0
         * FileIndex       : Offset 4
         * Zeitstempel     : Offset 8-55
         * FileAttributes  : Offset 56
         * FileNameLength  : Offset 60
         * EaSize          : Offset 64
         * FileName        : Offset 68
         *
         * FileNameLength ist in Bytes
         * angegeben (UTF-16).
         */
        var offset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (offset < 0
                || offset > BufferSize - EntryHeaderSize)
            {
                throw new InvalidDataException(
                    "Ein Verzeichniseintrag "
                    + "liegt außerhalb des Puffers.");
            }

            var nextOffset =
                unchecked((uint)Marshal.ReadInt32(
                    buffer,
                    offset));

            var rawAttributes =
                unchecked((uint)Marshal.ReadInt32(
                    buffer,
                    offset + 56));

            var fileNameLength =
                Marshal.ReadInt32(
                    buffer,
                    offset + 60);

            if (fileNameLength <= 0
                || (fileNameLength & 1) != 0
                || fileNameLength >
                    BufferSize - offset - EntryHeaderSize)
            {
                throw new InvalidDataException(
                    "Die Länge eines Dateinamens "
                    + "ist ungültig.");
            }

            var minimumEntryLength =
                EntryHeaderSize + fileNameLength;

            if (nextOffset != 0)
            {
                if (nextOffset < minimumEntryLength
                    || nextOffset > BufferSize - offset
                    || (nextOffset & 7) != 0)
                {
                    throw new InvalidDataException(
                        "Die Verkettung der "
                        + "Verzeichniseinträge "
                        + "ist ungültig.");
                }
            }

            var namePointer =
                IntPtr.Add(
                    buffer,
                    offset + EntryHeaderSize);

            var name =
                Marshal.PtrToStringUni(
                    namePointer,
                    fileNameLength / sizeof(char));

            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException(
                    "Ein Verzeichniseintrag "
                    + "enthält keinen gültigen Namen.");
            }

            /*
             * Die Windows-Pseudoeinträge
             * "." und ".." sind niemals
             * Bereinigungsziele.
             */
            if (name is not "." and not "..")
            {
                ValidateReturnedName(name);

                if (!observedNames.Add(name))
                {
                    throw new InvalidDataException(
                        "Die Verzeichnisaufzählung "
                        + "enthält einen mehrfachen "
                        + "Dateinamen.");
                }

                if (results.Count >= MaximumDirectoryEntries)
                {
                    throw new InvalidDataException(
                        "Die Sicherheitsgrenze "
                        + "für Verzeichniseinträge "
                        + "wurde überschritten.");
                }

                results.Add(
                    new DirectoryEntry(
                        name,
                        (FileAttributes)rawAttributes));
            }

            if (nextOffset == 0)
            {
                break;
            }

            offset += checked((int)nextOffset);
        }
    }

    // =============================================
    // UNTERORDNER RELATIV ÖFFNEN
    // =============================================

    /// <summary>
    /// Öffnet einen bereits untersuchten
    /// Unterordner mit Aufzählungsrechten.
    ///
    /// expectedSnapshot stammt aus einer
    /// vorherigen Handle-Untersuchung.
    ///
    /// Nur bei unveränderter Identität
    /// wird der neue Handle zurückgegeben.
    /// </summary>
    internal static SafeFileHandle
        OpenChildDirectoryForEnumeration(
            SafeFileHandle parentDirectory,
            string entryName,
            WindowsTemporaryFilesHandleAccess.EntrySnapshot
                expectedSnapshot)
    {
        EnsureWindows();
        ValidateDirectoryHandle(parentDirectory);
        ValidateEntryName(entryName);
        ValidateExpectedDirectory(expectedSnapshot);

        var unicodeLength =
            checked(entryName.Length * sizeof(char));

        if (unicodeLength > ushort.MaxValue - 2)
        {
            throw new ArgumentException(
                "Der Verzeichnisname ist zu lang.",
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

                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero
                };

            /*
             * FILE_DIRECTORY_FILE:
             * Ausschließlich Verzeichnisse.
             *
             * FILE_OPEN_REPARSE_POINT:
             * Eine Verknüpfung selbst öffnen,
             * nicht ihr Ziel.
             *
             * Kein FILE_SHARE_DELETE:
             * Konservativer Schutz gegen
             * Umbenennung und Austausch.
             */
            var status =
                NtCreateFile(
                    out var directoryHandle,
                    FileListDirectory
                        | FileReadAttributes
                        | Synchronize,
                    ref objectAttributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    FileShareRead | FileShareWrite,
                    FileOpen,
                    FileDirectoryFile
                        | FileSynchronousIoNonAlert
                        | FileOpenReparsePoint,
                    IntPtr.Zero,
                    0);

            if (status < 0)
            {
                directoryHandle?.Dispose();

                throw new IOException(
                    "Der Unterordner konnte nicht "
                    + "sicher geöffnet werden. "
                    + "NTSTATUS: 0x"
                    + unchecked((uint)status).ToString("X8"));
            }

            if (directoryHandle is null
                || directoryHandle.IsInvalid)
            {
                directoryHandle?.Dispose();

                throw new IOException(
                    "Windows hat kein gültiges "
                    + "Verzeichnishandle geliefert.");
            }

            try
            {
                var actualSnapshot =
                    WindowsTemporaryFilesHandleAccess
                        .ReadSnapshot(directoryHandle);

                ValidateExpectedDirectory(actualSnapshot);

                if (actualSnapshot.VolumeSerialNumber
                        != expectedSnapshot.VolumeSerialNumber
                    || actualSnapshot.FileIndex
                        != expectedSnapshot.FileIndex)
                {
                    throw new InvalidOperationException(
                        "SICHERHEITSSTOPP: Die "
                        + "Verzeichnisidentität hat "
                        + "sich verändert.");
                }

                return directoryHandle;
            }
            catch
            {
                directoryHandle.Dispose();
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
    // SICHERHEITSPRÜFUNGEN
    // =============================================

    private static void ValidateDirectoryHandle(
        SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (handle.IsInvalid || handle.IsClosed)
        {
            throw new InvalidOperationException(
                "Das Verzeichnishandle ist ungültig.");
        }

        var snapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(handle);

        ValidateExpectedDirectory(snapshot);
    }

    private static void ValidateExpectedDirectory(
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            snapshot)
    {
        if (!snapshot.IsDirectory)
        {
            throw new InvalidOperationException(
                "Das Objekt ist kein Verzeichnis.");
        }

        if (snapshot.IsReparsePoint)
        {
            throw new InvalidOperationException(
                "Ein Reparse Point darf "
                + "nicht betreten werden.");
        }

        if (snapshot.IsReadOnly
            || snapshot.IsSystemProtected)
        {
            throw new InvalidOperationException(
                "Ein geschütztes Verzeichnis "
                + "wird nicht durchsucht.");
        }

        if (snapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Die Verzeichnisidentität "
                + "konnte nicht bestimmt werden.");
        }
    }

    private static void ValidateEntryName(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name is "." or ".."
            || name.Contains('\\')
            || name.Contains('/')
            || name.Contains(':')
            || name.EndsWith(".", StringComparison.Ordinal)
            || name.EndsWith(" ", StringComparison.Ordinal)
            || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Es ist ausschließlich "
                + "ein einzelner Verzeichnisname "
                + "zulässig.",
                nameof(name));
        }
    }

    private static void ValidateReturnedName(
        string name)
    {
        /*
         * Unzulässige Pfadseparatoren
         * und Steuerzeichen führen
         * zum sicheren Abbruch.
         *
         * Andere ungewöhnliche Namen
         * werden unverändert zurückgegeben.
         * Ob sie später bearbeitet werden
         * dürfen, entscheidet der Worker.
         */
        if (name.Contains('\\')
            || name.Contains('/')
            || name.Contains(':')
            || name.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "Die Verzeichnisaufzählung "
                + "enthält einen unzulässigen "
                + "Dateinamen.");
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die handle-basierte "
                + "Verzeichnisdurchsuchung "
                + "benötigt Windows.");
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

    // =============================================
    // NATIVE WINDOWS-SCHNITTSTELLEN
    // =============================================

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFileInformationByHandleEx",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        IntPtr fileInformation,
        int bufferSize);

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
