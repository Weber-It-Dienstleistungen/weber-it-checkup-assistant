
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.12
///
/// Reale Regressionstests der
/// handle-basierten Verzeichnislöschung.
///
/// Ausschließlich eigene GUID-Sandbox.
/// Keine Administratorrechte.
/// Keine produktiven Windows-Dateien.
/// Keine rekursive Löschung.
///
/// Der produktive Executor wird nicht aufgerufen.
/// </summary>
public static class
    WindowsTemporaryFilesHandleDirectoryDeletionRegressionTests
{
    private const string ProtectedContent =
        "WEBER_IT_KEEP_NON_EMPTY";

    private const string FileContent =
        "WEBER_IT_NOT_A_DIRECTORY";

    public static string RunAll()
    {
        EnsureNonElevated();

        using var sandbox = new TestSandbox();

        var results = new List<string>();

        Execute(
            "Leerer Ordner wird über Handle entfernt",
            () => TestEmptyDirectory(sandbox),
            results);

        Execute(
            "Nicht leerer Ordner bleibt erhalten",
            () => TestNonEmptyDirectory(sandbox),
            results);

        Execute(
            "Schreibgeschützter Ordner bleibt erhalten",
            () => TestReadOnlyDirectory(sandbox),
            results);

        Execute(
            "Systemgeschützter Ordner bleibt erhalten",
            () => TestSystemDirectory(sandbox),
            results);

        Execute(
            "Ausgetauschter Ordner wird nicht entfernt",
            () => TestIdentityMismatch(sandbox),
            results);

        Execute(
            "Datei wird nicht als Ordner gelöscht",
            () => TestRegularFile(sandbox),
            results);

        return
            $"Alle {results.Count} Handle-Verzeichnistests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: LEERER ORDNER
    // =============================================

    private static void TestEmptyDirectory(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "Empty");

        Require(
            expected.IsDirectory
            && !expected.IsReparsePoint,
            "Der Testordner ist kein "
            + "reguläres Verzeichnis.");

        var deleted =
            WindowsTemporaryFilesHandleDirectoryDeletion
                .DeleteInspectedEmptyDirectory(
                    root,
                    "Empty",
                    expected);

        Require(
            !Directory.Exists(sandbox.EmptyDirectory),
            "Der leere Ordner wurde nicht entfernt.");

        Require(
            deleted.VolumeSerialNumber
                == expected.VolumeSerialNumber
            && deleted.FileIndex
                == expected.FileIndex,
            "Die gemeldete Verzeichnisidentität "
            + "stimmt nicht überein.");
    }

    // =============================================
    // TEST 2: NICHT LEERER ORDNER
    // =============================================

    private static void TestNonEmptyDirectory(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "NonEmpty");

        var rejected = false;

        try
        {
            WindowsTemporaryFilesHandleDirectoryDeletion
                .DeleteInspectedEmptyDirectory(
                    root,
                    "NonEmpty",
                    expected);
        }
        catch (IOException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "Windows hat den nicht leeren "
            + "Ordner nicht zurückgewiesen.");

        Require(
            Directory.Exists(sandbox.NonEmptyDirectory),
            "Der nicht leere Ordner "
            + "wurde entfernt.");

        Require(
            File.Exists(sandbox.ProtectedFile),
            "Die enthaltene Datei "
            + "wurde entfernt.");

        Require(
            File.ReadAllText(sandbox.ProtectedFile)
                == ProtectedContent,
            "Die enthaltene Datei "
            + "wurde verändert.");
    }

    // =============================================
    // TEST 3: SCHREIBSCHUTZ
    // =============================================

    private static void TestReadOnlyDirectory(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "ReadOnly");

        Require(
            expected.IsReadOnly,
            "Das ReadOnly-Attribut "
            + "wurde nicht erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDirectoryDeletion
                    .DeleteInspectedEmptyDirectory(
                        root,
                        "ReadOnly",
                        expected),
            "schreibgeschützt");

        Require(
            Directory.Exists(sandbox.ReadOnlyDirectory),
            "Der schreibgeschützte Ordner "
            + "wurde entfernt.");

        Require(
            (File.GetAttributes(sandbox.ReadOnlyDirectory)
                & FileAttributes.ReadOnly) != 0,
            "Der Schreibschutz wurde verändert.");
    }

    // =============================================
    // TEST 4: SYSTEMSCHUTZ
    // =============================================

    private static void TestSystemDirectory(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "System");

        Require(
            expected.IsSystemProtected,
            "Das Systemattribut "
            + "wurde nicht erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDirectoryDeletion
                    .DeleteInspectedEmptyDirectory(
                        root,
                        "System",
                        expected),
            "systemgeschützt");

        Require(
            Directory.Exists(sandbox.SystemDirectory),
            "Der systemgeschützte Ordner "
            + "wurde entfernt.");

        Require(
            (File.GetAttributes(sandbox.SystemDirectory)
                & FileAttributes.System) != 0,
            "Das Systemattribut wurde verändert.");
    }

    // =============================================
    // TEST 5: IDENTITÄTSWECHSEL
    // =============================================

    private static void TestIdentityMismatch(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        /*
         * Ursprünglichen Ordner untersuchen.
         */
        var original =
            Inspect(root, "Swap");

        /*
         * Diesen Ordner umbenennen,
         * aber nicht löschen.
         */
        Directory.Move(
            sandbox.SwapDirectory,
            sandbox.MovedDirectory);

        /*
         * Einen neuen Ordner unter dem
         * ursprünglichen Namen erstellen.
         */
        Directory.CreateDirectory(
            sandbox.SwapDirectory);

        var replacement =
            Inspect(root, "Swap");

        Require(
            original.VolumeSerialNumber
                != replacement.VolumeSerialNumber
            || original.FileIndex
                != replacement.FileIndex,
            "Der Test konnte keinen "
            + "Identitätswechsel nachweisen.");

        /*
         * Der Löschfunktion bewusst
         * die alte Identität übergeben.
         */
        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDirectoryDeletion
                    .DeleteInspectedEmptyDirectory(
                        root,
                        "Swap",
                        original),
            "Verzeichnisidentität");

        Require(
            Directory.Exists(sandbox.SwapDirectory),
            "Der neue Ordner wurde "
            + "versehentlich entfernt.");

        Require(
            Directory.Exists(sandbox.MovedDirectory),
            "Der ursprüngliche Ordner "
            + "ist verschwunden.");
    }

    // =============================================
    // TEST 6: FALSCHER OBJEKTTYP
    // =============================================

    private static void TestRegularFile(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "ordinary-file.txt");

        Require(
            !expected.IsDirectory,
            "Die Testdatei wurde "
            + "als Ordner erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDirectoryDeletion
                    .DeleteInspectedEmptyDirectory(
                        root,
                        "ordinary-file.txt",
                        expected),
            "kein Verzeichnis");

        Require(
            File.Exists(sandbox.OrdinaryFile),
            "Die normale Datei wurde entfernt.");

        Require(
            File.ReadAllText(sandbox.OrdinaryFile)
                == FileContent,
            "Die normale Datei wurde verändert.");
    }

    // =============================================
    // HANDLE-HILFSMETHODEN
    // =============================================

    private static SafeFileHandle OpenRoot(
        TestSandbox sandbox)
    {
        return
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);
    }

    private static
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
        Inspect(
            SafeFileHandle root,
            string entryName)
    {
        using var handle =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    entryName);

        return
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(handle);
    }

    private static void ExpectSafetyRejection(
        Action action,
        string expectedMessagePart)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                expectedMessagePart,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "Die erwartete Sicherheitsablehnung "
            + "ist ausgeblieben: "
            + expectedMessagePart);
    }

    // =============================================
    // BERECHTIGUNGSPRÜFUNG
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Tests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die Tests "
                + "dürfen nicht erhöht laufen.");
        }
    }

    // =============================================
    // SANDBOX
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string TargetDirectory { get; }

        public string EmptyDirectory { get; }

        public string NonEmptyDirectory { get; }

        public string ReadOnlyDirectory { get; }

        public string SystemDirectory { get; }

        public string SwapDirectory { get; }

        public string MovedDirectory { get; }

        public string ProtectedFile { get; }

        public string OrdinaryFile { get; }

        public TestSandbox()
        {
            var tempRoot =
                Path.GetFullPath(Path.GetTempPath());

            var sharedRoot =
                Path.Combine(
                    tempRoot,
                    "WeberIT.Checkup");

            var sandboxParent =
                Path.Combine(
                    sharedRoot,
                    "WindowsTempSandbox");

            RootDirectory =
                Path.Combine(
                    sandboxParent,
                    "DirectoryDeletion-"
                    + Guid.NewGuid().ToString("N"));

            TargetDirectory =
                Path.Combine(
                    RootDirectory,
                    "Temp");

            EmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Empty");

            NonEmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "NonEmpty");

            ReadOnlyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "ReadOnly");

            SystemDirectory =
                Path.Combine(
                    TargetDirectory,
                    "System");

            SwapDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Swap");

            MovedDirectory =
                Path.Combine(
                    TargetDirectory,
                    "SwapMoved");

            ProtectedFile =
                Path.Combine(
                    NonEmptyDirectory,
                    "keep.txt");

            OrdinaryFile =
                Path.Combine(
                    TargetDirectory,
                    "ordinary-file.txt");

            Require(
                Directory.Exists(tempRoot)
                && !IsReparsePoint(tempRoot),
                "Der temporäre Stammordner "
                + "ist nicht zulässig.");

            EnsurePlainDirectory(sharedRoot);
            EnsurePlainDirectory(sandboxParent);

            Require(
                !Directory.Exists(RootDirectory)
                && !File.Exists(RootDirectory),
                "Der GUID-Testordner "
                + "existiert bereits.");

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(EmptyDirectory);
            EnsurePlainDirectory(NonEmptyDirectory);
            EnsurePlainDirectory(ReadOnlyDirectory);
            EnsurePlainDirectory(SystemDirectory);
            EnsurePlainDirectory(SwapDirectory);

            File.WriteAllText(
                ProtectedFile,
                ProtectedContent);

            File.WriteAllText(
                OrdinaryFile,
                FileContent);

            File.SetAttributes(
                ReadOnlyDirectory,
                File.GetAttributes(ReadOnlyDirectory)
                    | FileAttributes.ReadOnly);

            File.SetAttributes(
                SystemDirectory,
                File.GetAttributes(SystemDirectory)
                    | FileAttributes.System);
        }

        public void Dispose()
        {
            /*
             * Nur ausdrücklich bekannte
             * Testdateien entfernen.
             *
             * Kein rekursives Löschen.
             * Keine fremden Inhalte verändern.
             */

            TryDeleteKnownFile(
                ProtectedFile,
                ProtectedContent);

            TryDeleteKnownFile(
                OrdinaryFile,
                FileContent);

            TryDeleteEmptyDirectory(EmptyDirectory);
            TryDeleteEmptyDirectory(NonEmptyDirectory);
            TryDeleteEmptyDirectory(ReadOnlyDirectory);
            TryDeleteEmptyDirectory(SystemDirectory);
            TryDeleteEmptyDirectory(SwapDirectory);
            TryDeleteEmptyDirectory(MovedDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static void EnsurePlainDirectory(
            string path)
        {
            if (!Directory.Exists(path))
            {
                Require(
                    !File.Exists(path),
                    "Ein Sandboxpfad ist bereits "
                    + "durch eine Datei belegt.");

                Directory.CreateDirectory(path);
            }

            Require(
                !IsReparsePoint(path),
                "Ein Testverzeichnis ist "
                + "ein Reparse Point: "
                + path);
        }

        private static bool IsReparsePoint(
            string path)
        {
            return
                (File.GetAttributes(path)
                    & FileAttributes.ReparsePoint) != 0;
        }

        private static void TryDeleteKnownFile(
            string path,
            string expectedContent)
        {
            try
            {
                if (!File.Exists(path)
                    || IsReparsePoint(path)
                    || File.ReadAllText(path)
                        != expectedContent)
                {
                    return;
                }

                File.Delete(path);
            }
            catch (IOException)
            {
                // Keine erzwungene Entfernung.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }

        private static void TryDeleteEmptyDirectory(
            string path)
        {
            try
            {
                if (!Directory.Exists(path)
                    || IsReparsePoint(path)
                    || Directory.EnumerateFileSystemEntries(path)
                        .Any())
                {
                    return;
                }

                /*
                 * Attribute nur bei eigenen,
                 * nachweislich leeren
                 * Testordnern zurücksetzen.
                 */
                var attributes =
                    File.GetAttributes(path);

                if ((attributes & FileAttributes.ReadOnly) != 0
                    || (attributes & FileAttributes.System) != 0)
                {
                    File.SetAttributes(
                        path,
                        attributes
                            & ~FileAttributes.ReadOnly
                            & ~FileAttributes.System);
                }

                Directory.Delete(
                    path,
                    recursive: false);
            }
            catch (IOException)
            {
                // Keine erzwungene Entfernung.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static void Execute(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Handle-Verzeichnistest fehlgeschlagen: "
                + title
                + Environment.NewLine
                + exception.Message,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

#endif
