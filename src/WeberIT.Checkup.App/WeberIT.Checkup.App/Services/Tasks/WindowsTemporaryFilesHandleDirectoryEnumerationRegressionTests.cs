
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.16
///
/// Gezielte Funktionstests für die
/// handle-basierte Verzeichnisdurchsuchung.
///
/// Nur selbst erzeugte GUID-Sandbox-Daten.
/// Keine Administratorrechte.
/// Keine produktiven Windows-Dateien.
/// Keine Löschung durch die getestete Klasse.
/// Kein rekursives Löschen beim Aufräumen.
/// </summary>
public static class
    WindowsTemporaryFilesHandleDirectoryEnumerationRegressionTests
{
    private const string RootContent =
        "WEBER_IT_ENUM_ROOT";

    private const string NestedContent =
        "WEBER_IT_ENUM_NESTED";

    private const string OutsideContent =
        "WEBER_IT_ENUM_OUTSIDE";

    public static string RunAll()
    {
        EnsureNonElevated();

        using var sandbox = new TestSandbox();

        var results = new List<string>();

        Execute(
            "Stammverzeichnis wird korrekt aufgelistet",
            () => TestRootEnumeration(sandbox),
            results);

        Execute(
            "Unterordner wird relativ geöffnet und aufgelistet",
            () => TestNestedEnumeration(sandbox),
            results);

        Execute(
            "Leeres Verzeichnis liefert keine Einträge",
            () => TestEmptyEnumeration(sandbox),
            results);

        Execute(
            "Angeforderter Abbruch wird berücksichtigt",
            () => TestCancellation(sandbox),
            results);

        Execute(
            "Ausgetauschter Unterordner wird abgewiesen",
            () => TestIdentityMismatch(sandbox),
            results);

        /*
         * Die Junction wird erst nach
         * den übrigen Tests angelegt.
         */
        sandbox.CreateJunction();

        Execute(
            "Junction wird erkannt und nicht betreten",
            () => TestJunction(sandbox),
            results);

        return
            $"Alle {results.Count} Handle-Aufzählungstests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: STAMMVERZEICHNIS
    // =============================================

    private static void TestRootEnumeration(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var entries =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .EnumerateDirectChildren(root);

        Require(
            entries.Count == 4,
            "Der Stammordner enthält nicht "
            + "genau vier Testeinträge.");

        Require(
            entries.Any(entry =>
                entry.Name == "alpha.txt"
                && !entry.IsListedDirectory),
            "Die normale Datei fehlt.");

        Require(
            entries.Any(entry =>
                entry.Name == "Nested"
                && entry.IsListedDirectory),
            "Der verschachtelte Ordner fehlt.");

        Require(
            entries.Any(entry =>
                entry.Name == "Empty"
                && entry.IsListedDirectory),
            "Der leere Ordner fehlt.");

        Require(
            entries.Any(entry =>
                entry.Name == "Swap"
                && entry.IsListedDirectory),
            "Der austauschbare Ordner fehlt.");

        Require(
            File.ReadAllText(sandbox.RootFile)
                == RootContent,
            "Die Aufzählung hat die "
            + "Testdatei verändert.");
    }

    // =============================================
    // TEST 2: VERSCHACHTELTES VERZEICHNIS
    // =============================================

    private static void TestNestedEnumeration(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "Nested");

        using var nested =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .OpenChildDirectoryForEnumeration(
                    root,
                    "Nested",
                    expected);

        var actual =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(nested);

        Require(
            actual.IsDirectory
            && !actual.IsReparsePoint,
            "Der geöffnete Unterordner "
            + "ist kein reguläres Verzeichnis.");

        Require(
            actual.VolumeSerialNumber
                == expected.VolumeSerialNumber
            && actual.FileIndex
                == expected.FileIndex,
            "Die Identität des geöffneten "
            + "Unterordners stimmt nicht.");

        var entries =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .EnumerateDirectChildren(nested);

        Require(
            entries.Count == 1,
            "Der Unterordner enthält nicht "
            + "genau einen Eintrag.");

        Require(
            entries[0].Name == "nested.txt"
            && !entries[0].IsListedDirectory,
            "Die verschachtelte Datei "
            + "wurde nicht korrekt erkannt.");

        Require(
            File.ReadAllText(sandbox.NestedFile)
                == NestedContent,
            "Der Inhalt der verschachtelten "
            + "Datei wurde verändert.");
    }

    // =============================================
    // TEST 3: LEERER ORDNER
    // =============================================

    private static void TestEmptyEnumeration(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var expected =
            Inspect(root, "Empty");

        using var empty =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .OpenChildDirectoryForEnumeration(
                    root,
                    "Empty",
                    expected);

        var entries =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .EnumerateDirectChildren(empty);

        Require(
            entries.Count == 0,
            "Ein tatsächlich leerer Ordner "
            + "wurde nicht leer gemeldet.");

        Require(
            Directory.Exists(sandbox.EmptyDirectory),
            "Der leere Ordner wurde "
            + "unerwartet entfernt.");
    }

    // =============================================
    // TEST 4: ABBRUCH
    // =============================================

    private static void TestCancellation(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var cancelled =
            new CancellationToken(
                canceled: true);

        var wasCancelled = false;

        try
        {
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .EnumerateDirectChildren(
                    root,
                    cancelled);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        Require(
            wasCancelled,
            "Ein bereits angeforderter "
            + "Abbruch wurde ignoriert.");

        Require(
            File.Exists(sandbox.RootFile),
            "Der Abbruchtest hat "
            + "eine Datei verändert.");
    }

    // =============================================
    // TEST 5: IDENTITÄTSWECHSEL
    // =============================================

    private static void TestIdentityMismatch(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var original =
            Inspect(root, "Swap");

        /*
         * Ursprünglichen Ordner behalten,
         * aber unter anderem Namen ablegen.
         */
        Directory.Move(
            sandbox.SwapDirectory,
            sandbox.MovedSwapDirectory);

        /*
         * Neuen Ordner unter dem
         * ursprünglichen Namen erzeugen.
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

        var rejected = false;

        try
        {
            using var unexpected =
                WindowsTemporaryFilesHandleDirectoryEnumeration
                    .OpenChildDirectoryForEnumeration(
                        root,
                        "Swap",
                        original);
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "Verzeichnisidentität",
                StringComparison.OrdinalIgnoreCase))
        {
            rejected = true;
        }

        Require(
            rejected,
            "Der ausgetauschte Ordner "
            + "wurde nicht zurückgewiesen.");

        Require(
            Directory.Exists(sandbox.SwapDirectory),
            "Der neue Ordner wurde entfernt.");

        Require(
            Directory.Exists(sandbox.MovedSwapDirectory),
            "Der ursprüngliche Ordner "
            + "wurde entfernt.");
    }

    // =============================================
    // TEST 6: JUNCTION
    // =============================================

    private static void TestJunction(
        TestSandbox sandbox)
    {
        using var root = OpenRoot(sandbox);

        var entries =
            WindowsTemporaryFilesHandleDirectoryEnumeration
                .EnumerateDirectChildren(root);

        var junctionEntry =
            entries.SingleOrDefault(entry =>
                entry.Name == "OutsideJunction");

        Require(
            junctionEntry.Name == "OutsideJunction",
            "Die Test-Junction wurde "
            + "nicht aufgelistet.");

        Require(
            junctionEntry.IsListedReparsePoint,
            "Die Junction wurde nicht "
            + "als Reparse Point erkannt.");

        /*
         * Wir verwenden absichtlich
         * die Identität eines regulären
         * Test-Unterordners.
         *
         * Selbst damit darf die Junction
         * nicht als reguläres Verzeichnis
         * geöffnet werden.
         */
        var expected =
            Inspect(root, "Nested");

        var rejected = false;

        try
        {
            using var unexpected =
                WindowsTemporaryFilesHandleDirectoryEnumeration
                    .OpenChildDirectoryForEnumeration(
                        root,
                        "OutsideJunction",
                        expected);
        }
        catch (IOException)
        {
            rejected = true;
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "Die Junction wurde als "
            + "regulärer Unterordner geöffnet.");

        Require(
            Directory.Exists(sandbox.JunctionPath),
            "Die Junction wurde entfernt.");

        Require(
            File.Exists(sandbox.OutsideFile),
            "Das Junction-Ziel wurde verändert.");

        Require(
            File.ReadAllText(sandbox.OutsideFile)
                == OutsideContent,
            "Der Inhalt des Junction-Ziels "
            + "wurde verändert.");
    }

    // =============================================
    // HANDLE-HILFSMETHODEN
    // =============================================

    private static SafeFileHandle OpenRoot(
        TestSandbox sandbox)
    {
        return WindowsTemporaryFilesHandleAccess
            .OpenRootDirectory(
                sandbox.TargetDirectory);
    }

    private static
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
        Inspect(
            SafeFileHandle root,
            string entryName)
    {
        using var child =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    entryName);

        return WindowsTemporaryFilesHandleAccess
            .ReadSnapshot(child);
    }

    // =============================================
    // BERECHTIGUNGEN
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Diese Tests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: "
                + "Die Tests dürfen nicht "
                + "mit Administratorrechten laufen.");
        }
    }

    // =============================================
    // ISOLIERTE SANDBOX
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }
        public string TargetDirectory { get; }
        public string NestedDirectory { get; }
        public string EmptyDirectory { get; }
        public string SwapDirectory { get; }
        public string MovedSwapDirectory { get; }
        public string OutsideDirectory { get; }

        public string RootFile { get; }
        public string NestedFile { get; }
        public string OutsideFile { get; }

        public string JunctionPath { get; }

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
                    "Enumeration-"
                    + Guid.NewGuid().ToString("N"));

            TargetDirectory =
                Path.Combine(
                    RootDirectory,
                    "Temp");

            NestedDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Nested");

            EmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Empty");

            SwapDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Swap");

            MovedSwapDirectory =
                Path.Combine(
                    TargetDirectory,
                    "SwapMoved");

            OutsideDirectory =
                Path.Combine(
                    RootDirectory,
                    "OutsideTarget");

            RootFile =
                Path.Combine(
                    TargetDirectory,
                    "alpha.txt");

            NestedFile =
                Path.Combine(
                    NestedDirectory,
                    "nested.txt");

            OutsideFile =
                Path.Combine(
                    OutsideDirectory,
                    "outside.txt");

            JunctionPath =
                Path.Combine(
                    TargetDirectory,
                    "OutsideJunction");

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
                "Der neue Sandboxordner "
                + "existiert bereits.");

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(NestedDirectory);
            EnsurePlainDirectory(EmptyDirectory);
            EnsurePlainDirectory(SwapDirectory);
            EnsurePlainDirectory(OutsideDirectory);

            File.WriteAllText(RootFile, RootContent);
            File.WriteAllText(NestedFile, NestedContent);
            File.WriteAllText(OutsideFile, OutsideContent);
        }

        public void CreateJunction()
        {
            Require(
                !Directory.Exists(JunctionPath)
                && !File.Exists(JunctionPath),
                "Der Junction-Pfad existiert bereits.");

            var commandPath =
                Path.Combine(
                    Environment.SystemDirectory,
                    "cmd.exe");

            Require(
                File.Exists(commandPath),
                "Der Windows-Befehlsprozessor "
                + "wurde nicht gefunden.");

            /*
             * Die Junction zeigt ausschließlich
             * auf den selbst erzeugten
             * OutsideTarget-Testordner.
             */
            var startInfo =
                new ProcessStartInfo
                {
                    FileName = commandPath,

                    Arguments =
                        "/d /c mklink /J \""
                        + JunctionPath
                        + "\" \""
                        + OutsideDirectory
                        + "\"",

                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            Require(
                process.Start(),
                "Die Test-Junction konnte "
                + "nicht angelegt werden.");

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            var errorTask =
                process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10000))
            {
                try
                {
                    process.Kill(
                        entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Prozess ist bereits beendet.
                }

                throw new InvalidOperationException(
                    "Das Erstellen der Test-Junction "
                    + "hat das Zeitlimit überschritten.");
            }

            var output =
                outputTask.GetAwaiter().GetResult();

            var error =
                errorTask.GetAwaiter().GetResult();

            Require(
                process.ExitCode == 0,
                "Die Junction konnte "
                + "nicht erstellt werden."
                + Environment.NewLine
                + output
                + Environment.NewLine
                + error);

            Require(
                IsReparsePoint(JunctionPath),
                "Das erstellte Objekt "
                + "ist kein Reparse Point.");
        }

        public void Dispose()
        {
            /*
             * Ausschließlich bekannte
             * Testdateien entfernen.
             *
             * Keine rekursive Löschung.
             * Keine fremden Dateien verändern.
             */
            TryRemoveJunction();

            TryDeleteKnownFile(
                RootFile,
                RootContent);

            TryDeleteKnownFile(
                NestedFile,
                NestedContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideContent);

            TryDeleteEmptyDirectory(NestedDirectory);
            TryDeleteEmptyDirectory(EmptyDirectory);
            TryDeleteEmptyDirectory(SwapDirectory);
            TryDeleteEmptyDirectory(MovedSwapDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(OutsideDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private void TryRemoveJunction()
        {
            try
            {
                if (Directory.Exists(JunctionPath)
                    && IsReparsePoint(JunctionPath))
                {
                    Directory.Delete(
                        JunctionPath,
                        recursive: false);
                }
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

        private static void EnsurePlainDirectory(
            string path)
        {
            if (!Directory.Exists(path))
            {
                Require(
                    !File.Exists(path),
                    "Ein Verzeichnispfad "
                    + "ist durch eine Datei belegt.");

                Directory.CreateDirectory(path);
            }

            Require(
                !IsReparsePoint(path),
                "Ein Sandboxordner "
                + "ist ein Reparse Point: "
                + path);
        }

        private static bool IsReparsePoint(string path)
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
                if (Directory.Exists(path)
                    && !IsReparsePoint(path)
                    && !Directory
                        .EnumerateFileSystemEntries(path)
                        .Any())
                {
                    Directory.Delete(
                        path,
                        recursive: false);
                }
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
            results.Add("OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Handle-Aufzählungstest fehlgeschlagen: "
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
