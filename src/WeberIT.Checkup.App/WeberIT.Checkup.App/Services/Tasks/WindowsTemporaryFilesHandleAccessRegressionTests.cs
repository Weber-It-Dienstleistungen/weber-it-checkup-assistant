
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.2
///
/// Gezielte Funktionstests für den neuen
/// handle-basierten Dateisystemzugriff.
///
/// Es werden ausschließlich selbst erzeugte
/// Testdateien innerhalb einer GUID-Sandbox
/// verwendet.
///
/// Kein Administratorzugriff.
/// Keine produktiven Windows-Verzeichnisse.
/// Keine Löschung durch die getestete Klasse.
/// Keine rekursive Bereinigung.
/// </summary>
public static class
    WindowsTemporaryFilesHandleAccessRegressionTests
{
    private const string NormalContent =
        "WEBER_IT_HANDLE_NORMAL";

    private const string NestedContent =
        "WEBER_IT_HANDLE_NESTED";

    private const string OutsideContent =
        "WEBER_IT_HANDLE_OUTSIDE";

    public static string RunAll()
    {
        EnsureNonElevated();

        using var sandbox = new TestSandbox();

        var results = new List<string>();

        Execute(
            "Sandbox-Stammordner wird korrekt geöffnet",
            () => TestRootDirectory(sandbox),
            results);

        Execute(
            "Datei wird relativ zum Elternhandle geöffnet",
            () => TestNormalFile(sandbox),
            results);

        Execute(
            "Verschachtelte Ordner bleiben handle-gebunden",
            () => TestNestedDirectory(sandbox),
            results);

        Execute(
            "Dateiidentität bleibt bei erneutem Öffnen gleich",
            () => TestStableIdentity(sandbox),
            results);

        Execute(
            "Ungültige und ausbrechende Dateinamen werden abgewiesen",
            () => TestInvalidNames(sandbox),
            results);

        /*
         * Die Junction wird erst nach
         * den normalen Zugriffstests erzeugt.
         */
        sandbox.CreateJunction();

        Execute(
            "Junction wird weder als Kind noch als Stamm betreten",
            () => TestJunction(sandbox),
            results);

        return
            $"Alle {results.Count} Handle-Zugriffstests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: STAMMORDNER
    // =============================================

    private static void TestRootDirectory(
        TestSandbox sandbox)
    {
        using var handle =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        var snapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(handle);

        Require(
            snapshot.IsDirectory,
            "Der Stammordner wurde nicht "
            + "als Verzeichnis erkannt.");

        Require(
            !snapshot.IsReparsePoint,
            "Der Stammordner wurde als "
            + "Reparse Point erkannt.");

        Require(
            !handle.IsInvalid && !handle.IsClosed,
            "Das Stammhandle ist ungültig.");
    }

    // =============================================
    // TEST 2: NORMALE DATEI
    // =============================================

    private static void TestNormalFile(
        TestSandbox sandbox)
    {
        using var root =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        using var file =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    "normal.txt");

        var snapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(file);

        Require(
            !snapshot.IsDirectory,
            "Eine normale Datei wurde "
            + "als Verzeichnis erkannt.");

        Require(
            !snapshot.IsReparsePoint,
            "Eine normale Datei wurde "
            + "als Reparse Point erkannt.");

        Require(
            snapshot.SizeBytes
                == (ulong)new FileInfo(
                    sandbox.NormalFile).Length,
            "Die Dateigröße aus dem Handle "
            + "stimmt nicht überein.");

        Require(
            File.ReadAllText(
                sandbox.NormalFile) == NormalContent,
            "Die normale Datei wurde verändert.");
    }

    // =============================================
    // TEST 3: VERSCHACHTELTE ORDNER
    // =============================================

    private static void TestNestedDirectory(
        TestSandbox sandbox)
    {
        using var root =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        using var nestedDirectory =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    "Nested");

        var directorySnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(nestedDirectory);

        Require(
            directorySnapshot.IsDirectory,
            "Der Unterordner wurde nicht "
            + "als Verzeichnis erkannt.");

        Require(
            !directorySnapshot.IsReparsePoint,
            "Der Unterordner ist "
            + "unerwartet ein Reparse Point.");

        /*
         * Wichtig:
         *
         * Die Datei wird relativ zum
         * geöffneten Unterordner geöffnet.
         *
         * Es wird kein vollständiger
         * Dateipfad an NtCreateFile übergeben.
         */
        using var nestedFile =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    nestedDirectory,
                    "nested.txt");

        var fileSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(nestedFile);

        Require(
            !fileSnapshot.IsDirectory,
            "Die verschachtelte Datei wurde "
            + "als Ordner erkannt.");

        Require(
            fileSnapshot.SizeBytes
                == (ulong)new FileInfo(
                    sandbox.NestedFile).Length,
            "Die Größe der verschachtelten "
            + "Datei stimmt nicht.");

        Require(
            File.ReadAllText(
                sandbox.NestedFile) == NestedContent,
            "Die verschachtelte Datei "
            + "wurde verändert.");
    }

    // =============================================
    // TEST 4: DATEIIDENTITÄT
    // =============================================

    private static void TestStableIdentity(
        TestSandbox sandbox)
    {
        using var root =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        using var first =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    "normal.txt");

        using var second =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    "normal.txt");

        var firstSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(first);

        var secondSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(second);

        Require(
            firstSnapshot.VolumeSerialNumber
                == secondSnapshot.VolumeSerialNumber,
            "Die Volume-Identität "
            + "hat sich verändert.");

        Require(
            firstSnapshot.FileIndex
                == secondSnapshot.FileIndex,
            "Die Dateiidentität "
            + "hat sich verändert.");

        Require(
            firstSnapshot.SizeBytes
                == secondSnapshot.SizeBytes,
            "Die Dateigröße ist "
            + "zwischen den Zugriffen unterschiedlich.");
    }

    // =============================================
    // TEST 5: UNZULÄSSIGE NAMEN
    // =============================================

    private static void TestInvalidNames(
        TestSandbox sandbox)
    {
        using var root =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        string[] invalidNames =
        [
            "..",
            ".",
            @"..\outside.txt",
            @"folder\another.txt",
            "../outside.txt",
            @"C:\Windows",
            "file:stream",
            "trailing.",
            "trailing ",
            "",
            " "
        ];

        foreach (var name in invalidNames)
        {
            var wasRejected = false;

            try
            {
                using var handle =
                    WindowsTemporaryFilesHandleAccess
                        .OpenChildForInspection(
                            root,
                            name);
            }
            catch (ArgumentException)
            {
                wasRejected = true;
            }

            Require(
                wasRejected,
                "Ein unzulässiger Dateiname "
                + "wurde akzeptiert: "
                + name);
        }
    }

    // =============================================
    // TEST 6: JUNCTION
    // =============================================

    private static void TestJunction(
        TestSandbox sandbox)
    {
        var attributes =
            File.GetAttributes(
                sandbox.JunctionPath);

        Require(
            (attributes & FileAttributes.ReparsePoint) != 0,
            "Die Test-Junction besitzt "
            + "kein Reparse-Attribut.");

        using var root =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);

        var childWasRejected = false;

        try
        {
            using var child =
                WindowsTemporaryFilesHandleAccess
                    .OpenChildForInspection(
                        root,
                        "OutsideJunction");
        }
        catch (IOException)
        {
            childWasRejected = true;
        }
        catch (InvalidOperationException)
        {
            childWasRejected = true;
        }

        Require(
            childWasRejected,
            "Die Junction wurde als "
            + "normaler Kindeintrag geöffnet.");

        var rootWasRejected = false;

        try
        {
            using var junctionRoot =
                WindowsTemporaryFilesHandleAccess
                    .OpenRootDirectory(
                        sandbox.JunctionPath);
        }
        catch (IOException)
        {
            rootWasRejected = true;
        }
        catch (InvalidOperationException)
        {
            rootWasRejected = true;
        }

        Require(
            rootWasRejected,
            "Die Junction wurde als "
            + "Stammverzeichnis akzeptiert.");

        Require(
            File.Exists(sandbox.OutsideFile),
            "Die externe Kontroll-Datei fehlt.");

        Require(
            File.ReadAllText(
                sandbox.OutsideFile) == OutsideContent,
            "Die externe Kontroll-Datei "
            + "wurde verändert.");

        Require(
            Directory.Exists(sandbox.JunctionPath),
            "Die Junction wurde unerwartet entfernt.");
    }

    // =============================================
    // RECHTEPRÜFUNG
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Handle-Zugriffstests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Diese Tests "
                + "dürfen nicht erhöht ausgeführt werden.");
        }
    }

    // =============================================
    // SANDBOX
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string NestedDirectory { get; }

        public string OutsideDirectory { get; }

        public string NormalFile { get; }

        public string NestedFile { get; }

        public string OutsideFile { get; }

        public string JunctionPath { get; }

        public TestSandbox()
        {
            var tempRoot =
                Path.GetFullPath(
                    Path.GetTempPath());

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
                    "Handle-"
                    + Guid.NewGuid().ToString("N"));

            WindowsDirectory =
                Path.Combine(
                    RootDirectory,
                    "Windows");

            TargetDirectory =
                Path.Combine(
                    WindowsDirectory,
                    "Temp");

            NestedDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Nested");

            OutsideDirectory =
                Path.Combine(
                    RootDirectory,
                    "OutsideTarget");

            NormalFile =
                Path.Combine(
                    TargetDirectory,
                    "normal.txt");

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
                "Der GUID-Sandboxordner "
                + "existiert bereits.");

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(WindowsDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(NestedDirectory);
            EnsurePlainDirectory(OutsideDirectory);

            File.WriteAllText(
                NormalFile,
                NormalContent);

            File.WriteAllText(
                NestedFile,
                NestedContent);

            File.WriteAllText(
                OutsideFile,
                OutsideContent);
        }

        public void CreateJunction()
        {
            Require(
                !Directory.Exists(JunctionPath)
                && !File.Exists(JunctionPath),
                "Der vorgesehene Junction-Pfad "
                + "existiert bereits.");

            var commandPath =
                Path.Combine(
                    Environment.SystemDirectory,
                    "cmd.exe");

            Require(
                File.Exists(commandPath),
                "Windows-Befehlsprozessor "
                + "wurde nicht gefunden.");

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
                + "nicht erstellt werden.");

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
                    // Prozess bereits beendet.
                }

                throw new InvalidOperationException(
                    "Junction-Erstellung hat "
                    + "das Zeitlimit überschritten.");
            }

            var output =
                outputTask.GetAwaiter().GetResult();

            var error =
                errorTask.GetAwaiter().GetResult();

            Require(
                process.ExitCode == 0,
                "Die Test-Junction konnte "
                + "nicht erstellt werden."
                + Environment.NewLine
                + output
                + Environment.NewLine
                + error);

            Require(
                IsReparsePoint(JunctionPath),
                "Das erstellte Objekt "
                + "ist keine Junction.");
        }

        public void Dispose()
        {
            /*
             * Nur explizit bekannte Testdaten
             * entfernen.
             *
             * Keine rekursive Löschung.
             * Keine Änderung fremder Dateien.
             */
            TryRemoveJunction();

            TryDeleteKnownFile(
                NormalFile,
                NormalContent);

            TryDeleteKnownFile(
                NestedFile,
                NestedContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideContent);

            TryDeleteEmptyDirectory(NestedDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(WindowsDirectory);
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
                    "Ein Testverzeichnispfad "
                    + "ist bereits durch eine Datei belegt.");

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
                    || IsReparsePoint(path))
                {
                    return;
                }

                if (File.ReadAllText(path)
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
                    && !IsReparsePoint(path))
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
                "Handle-Zugriffstest fehlgeschlagen: "
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
