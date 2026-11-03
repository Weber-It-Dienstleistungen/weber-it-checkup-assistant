
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.SandboxTestRunner;

/// <summary>
/// Sprint 55.14.17
///
/// Eigenständiger Teststarter für die
/// Windows-Temp-Bereinigung.
///
/// 59 bestehende Regressionstests
/// plus sechs neue Tests zur
/// handle-basierten Verzeichnisdurchsuchung.
///
/// Sämtliche Tests ohne Administratorrechte.
///
/// Echte Löschoperationen betreffen
/// ausschließlich künstliche Sandbox-Daten.
///
/// Keine produktive Windows-Temp-Bereinigung.
/// </summary>
internal static class Program
{
    private const int ProcessTimeoutMilliseconds = 20000;

    [STAThread]
    private static int Main()
    {
#if DEBUG
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            " WEBER IT - SANDBOX TESTRUNNER");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(
                "FEHLER: Windows wird benötigt.");

            return 2;
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine(
                "SICHERHEITSSTOPP: "
                + "Der TestRunner darf nicht "
                + "mit Administratorrechten laufen.");

            return 2;
        }

        Console.WriteLine(
            "Berechtigungsprüfung: OK");

        Console.WriteLine(
            "Ausführung ohne Administratorrechte.");

        Console.WriteLine();

        try
        {
            // =================================
            // TESTBLOCK 1: ABBRUCH
            // =================================

            RunTestBlock(
                "TESTBLOCK 1 - ABBRUCH",
                WindowsTemporaryFilesCleanupSandboxCancellationRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 2: KONTROLLIERTE LÖSCHUNG
            // =================================

            RunTestBlock(
                "TESTBLOCK 2 - KONTROLLIERTE LÖSCHUNG",
                WindowsTemporaryFilesCleanupSandboxDeletionRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 3: SCHUTZMECHANISMEN
            // =================================

            RunTestBlock(
                "TESTBLOCK 3 - SCHUTZMECHANISMEN",
                WindowsTemporaryFilesCleanupSandboxProtectionRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 4: VERZEICHNISGRENZEN
            // =================================

            RunTestBlock(
                "TESTBLOCK 4 - VERZEICHNISGRENZEN",
                WindowsTemporaryFilesCleanupSandboxBoundaryRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 5: BASISVERTRÄGE
            // =================================

            RunTestBlock(
                "TESTBLOCK 5 - BASISVERTRÄGE",
                WindowsTemporaryFilesCleanupBaselineRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 6: VERIFIZIERTER LAUNCHER
            // =================================

            PrintHeading(
                "TESTBLOCK 6 - VERIFIZIERTER LAUNCHER");

            RunVerifiedLauncherTests();

            // =================================
            // TESTBLOCK 7: HANDLE-ZUGRIFF
            // =================================

            RunTestBlock(
                "TESTBLOCK 7 - HANDLE-BASIERTER DATEIZUGRIFF",
                WindowsTemporaryFilesHandleAccessRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 8: HANDLE-DATEILÖSCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 8 - HANDLE-BASIERTE DATEILÖSCHUNG");

            Console.WriteLine(
                "Prüfe echte Dateilöschung, "
                + "Schreibschutz, Systemschutz, "
                + "Dateisperren und Identitätswechsel.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDeletionRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 9: VERZEICHNISLÖSCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 9 - HANDLE-BASIERTE VERZEICHNISLÖSCHUNG");

            Console.WriteLine(
                "Prüfe leere und nicht leere Ordner, "
                + "Schreibschutz, Systemschutz, "
                + "Identitätswechsel und Objekttypen.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDirectoryDeletionRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 10: VERZEICHNISDURCHSUCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 10 - HANDLE-BASIERTE VERZEICHNISDURCHSUCHUNG");

            Console.WriteLine(
                "Prüfe Verzeichnisaufzählung, "
                + "verschachtelte Ordner, "
                + "leere Verzeichnisse, "
                + "Abbruch, Identitätswechsel "
                + "und Junction-Schutz.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDirectoryEnumerationRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // GESAMTERGEBNIS
            // =================================

            Console.WriteLine(
                "========================================");

            Console.WriteLine(
                " ERGEBNIS: ALLE 65 TESTS BESTANDEN");

            Console.WriteLine(
                "========================================");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();

            Console.Error.WriteLine(
                "========================================");

            Console.Error.WriteLine(
                " ERGEBNIS: TEST FEHLGESCHLAGEN");

            Console.Error.WriteLine(
                "========================================");

            Console.WriteLine();

            Console.Error.WriteLine(
                exception.ToString());

            return 1;
        }
#else
        Console.Error.WriteLine(
            "Dieser TestRunner ist ausschließlich "
            + "im Debug-Modus verfügbar.");

        return 2;
#endif
    }

#if DEBUG

    // =============================================
    // TESTBLOCK AUSFÜHREN
    // =============================================

    private static void RunTestBlock(
        string title,
        Func<string> runTests)
    {
        PrintHeading(title);

        Console.WriteLine(
            runTests());

        Console.WriteLine();
    }

    // =============================================
    // LAUNCHER-FUNKTIONSTESTS
    // =============================================

    private static void RunVerifiedLauncherTests()
    {
        /*
         * Diese beiden Tests verwenden
         * denselben Launcher wie der
         * produktive Executor.
         *
         * Es werden ausschließlich
         * harmlose Markierungsdateien
         * in einer GUID-Sandbox erzeugt.
         */

        var sandboxRoot =
            Path.Combine(
                Path.GetFullPath(Path.GetTempPath()),
                "WeberIT.Checkup",
                "WindowsTempSandbox",
                "Launcher-"
                    + Guid.NewGuid().ToString("N"));

        var validScriptPath =
            Path.Combine(
                sandboxRoot,
                "valid.ps1");

        var validMarkerPath =
            Path.Combine(
                sandboxRoot,
                "valid-result.txt");

        var changedScriptPath =
            Path.Combine(
                sandboxRoot,
                "changed.ps1");

        var rejectedMarkerPath =
            Path.Combine(
                sandboxRoot,
                "rejected-result.txt");

        var forgedMarkerPath =
            Path.Combine(
                sandboxRoot,
                "forged-result.txt");

        EnsureSandboxDirectory(sandboxRoot);

        var validScript =
            BuildMarkerScript(
                validMarkerPath,
                "VERIFIED_OK");

        var originalChangedScript =
            BuildMarkerScript(
                rejectedMarkerPath,
                "ORIGINAL");

        var forgedScript =
            BuildMarkerScript(
                forgedMarkerPath,
                "FORGED");

        try
        {
            // =================================
            // TEST 1: KORREKTER SHA-256-HASH
            // =================================

            File.WriteAllText(
                validScriptPath,
                validScript,
                Encoding.Unicode);

            var validHash =
                CalculateScriptHash(
                    validScript);

            var validLauncher =
                BuildVerifiedLauncher(
                    validScriptPath,
                    validHash);

            var successfulExecution =
                ExecutePowerShell(
                    validLauncher);

            Require(
                successfulExecution.ExitCode == 0,
                "Der Launcher hat ein korrekt "
                + "verifiziertes Skript abgelehnt."
                + Environment.NewLine
                + successfulExecution.StandardError);

            Require(
                File.Exists(validMarkerPath),
                "Das verifizierte Skript "
                + "wurde nicht ausgeführt.");

            Require(
                File.ReadAllText(validMarkerPath)
                    == "VERIFIED_OK",
                "Das verifizierte Skript "
                + "hat ein falsches Ergebnis erzeugt.");

            Console.WriteLine(
                "OK: Korrekter Hash erlaubt "
                + "die Skriptausführung.");

            // =================================
            // TEST 2: MANIPULIERTE SKRIPTDATEI
            // =================================

            File.WriteAllText(
                changedScriptPath,
                originalChangedScript,
                Encoding.Unicode);

            var originalHash =
                CalculateScriptHash(
                    originalChangedScript);

            /*
             * Die Datei wird erst nach
             * Berechnung des erwarteten
             * Hashwerts verändert.
             *
             * Der Launcher darf diese
             * Datei nicht ausführen.
             */
            File.WriteAllText(
                changedScriptPath,
                forgedScript,
                Encoding.Unicode);

            var changedLauncher =
                BuildVerifiedLauncher(
                    changedScriptPath,
                    originalHash);

            var rejectedExecution =
                ExecutePowerShell(
                    changedLauncher);

            Require(
                rejectedExecution.ExitCode != 0,
                "Der Launcher hat ein verändertes "
                + "Skript nicht zurückgewiesen.");

            Require(
                !File.Exists(rejectedMarkerPath),
                "Das ursprüngliche Testskript "
                + "wurde unerwartet ausgeführt.");

            Require(
                !File.Exists(forgedMarkerPath),
                "Das manipulierte Testskript "
                + "wurde ausgeführt.");

            Require(
                rejectedExecution.StandardError.Contains(
                    "Bereinigungsskripts",
                    StringComparison.OrdinalIgnoreCase)
                && rejectedExecution.StandardError.Contains(
                    "fehlgeschlagen",
                    StringComparison.OrdinalIgnoreCase),
                "Die Ablehnung ist nicht eindeutig "
                + "auf die Integritätsprüfung "
                + "zurückzuführen."
                + Environment.NewLine
                + rejectedExecution.StandardError);

            Console.WriteLine(
                "OK: Veränderte Skriptdatei wird "
                + "durch die Hashprüfung abgelehnt.");

            Console.WriteLine();

            Console.WriteLine(
                "Alle 2 Launcher-Funktionstests "
                + "erfolgreich.");
        }
        finally
        {
            /*
             * Ausschließlich eigene,
             * bekannte Testdateien entfernen.
             *
             * Keine rekursive Löschung.
             */

            TryDeleteKnownFile(
                validScriptPath,
                validScript,
                Encoding.Unicode);

            TryDeleteKnownFile(
                changedScriptPath,
                forgedScript,
                Encoding.Unicode);

            TryDeleteKnownFile(
                validMarkerPath,
                "VERIFIED_OK",
                Encoding.UTF8);

            TryDeleteKnownFile(
                rejectedMarkerPath,
                "ORIGINAL",
                Encoding.UTF8);

            TryDeleteKnownFile(
                forgedMarkerPath,
                "FORGED",
                Encoding.UTF8);

            try
            {
                if (Directory.Exists(sandboxRoot)
                    && !IsReparsePoint(sandboxRoot))
                {
                    Directory.Delete(
                        sandboxRoot,
                        recursive: false);
                }
            }
            catch (IOException)
            {
                // Unbekannte Inhalte bleiben erhalten.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }
    }

    // =============================================
    // PRODUKTIVEN LAUNCHER AUFRUFEN
    // =============================================

    private static string BuildVerifiedLauncher(
        string scriptPath,
        string expectedHash)
    {
        var method =
            typeof(WindowsTemporaryFilesCleanupExecutor)
                .GetMethod(
                    "BuildVerifiedLauncherScript",
                    BindingFlags.NonPublic
                        | BindingFlags.Static);

        Require(
            method is not null,
            "Der produktive Launcher-Einstiegspunkt "
            + "wurde nicht gefunden.");

        var result =
            method!.Invoke(
                null,
                [
                    scriptPath,
                    expectedHash
                ]);

        return result as string
            ?? throw new InvalidOperationException(
                "Der Launcher lieferte "
                + "keinen Skripttext.");
    }

    private static string CalculateScriptHash(
        string script)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.Unicode.GetBytes(script)));
    }

    private static string BuildMarkerScript(
        string markerPath,
        string content)
    {
        var escapedPath =
            markerPath.Replace(
                "'",
                "''",
                StringComparison.Ordinal);

        return
            "[System.IO.File]::WriteAllText("
            + "'"
            + escapedPath
            + "', "
            + "'"
            + content
            + "', "
            + "[System.Text.Encoding]::UTF8)";
    }

    // =============================================
    // POWERSHELL OHNE ADMINISTRATORRECHTE
    // =============================================

    private static (
        int ExitCode,
        string StandardOutput,
        string StandardError)
        ExecutePowerShell(
            string launcher)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        var powerShellPath =
            Path.Combine(
                windowsDirectory,
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");

        Require(
            File.Exists(powerShellPath),
            "Windows-PowerShell wurde "
            + "nicht gefunden.");

        var encodedCommand =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(launcher));

        var startInfo =
            new ProcessStartInfo
            {
                FileName = powerShellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        Require(
            process.Start(),
            "Der PowerShell-Testprozess "
            + "konnte nicht gestartet werden.");

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(
                ProcessTimeoutMilliseconds))
        {
            try
            {
                process.Kill(
                    entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Prozess war bereits beendet.
            }

            throw new InvalidOperationException(
                "Der Launcher-Test hat das "
                + "Zeitlimit überschritten.");
        }

        return (
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    // =============================================
    // SANDBOX-SCHUTZ
    // =============================================

    private static void EnsureSandboxDirectory(
        string sandboxRoot)
    {
        var tempRoot =
            Path.GetFullPath(Path.GetTempPath());

        var firstParent =
            Path.Combine(
                tempRoot,
                "WeberIT.Checkup");

        var secondParent =
            Path.Combine(
                firstParent,
                "WindowsTempSandbox");

        Require(
            !IsReparsePoint(tempRoot),
            "Der temporäre Stammordner "
            + "ist ein Reparse Point.");

        EnsurePlainDirectory(firstParent);
        EnsurePlainDirectory(secondParent);

        Require(
            !Directory.Exists(sandboxRoot)
                && !File.Exists(sandboxRoot),
            "Der GUID-Testordner "
            + "existiert bereits.");

        EnsurePlainDirectory(sandboxRoot);
    }

    private static void EnsurePlainDirectory(
        string path)
    {
        if (Directory.Exists(path))
        {
            Require(
                !IsReparsePoint(path),
                "Ein Sandbox-Elternordner "
                + "ist ein Reparse Point: "
                + path);

            return;
        }

        Require(
            !File.Exists(path),
            "Ein Sandbox-Verzeichnispfad "
            + "ist bereits durch eine Datei belegt.");

        Directory.CreateDirectory(path);

        Require(
            !IsReparsePoint(path),
            "Ein neuer Sandboxordner "
            + "ist ein Reparse Point.");
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
        string expectedContent,
        Encoding encoding)
    {
        try
        {
            if (!File.Exists(path)
                || IsReparsePoint(path))
            {
                return;
            }

            if (File.ReadAllText(path, encoding)
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

    // =============================================
    // KONSOLENAUSGABE
    // =============================================

    private static void PrintHeading(
        string heading)
    {
        Console.WriteLine();

        Console.WriteLine(
            "----------------------------------------");

        Console.WriteLine(" " + heading);

        Console.WriteLine(
            "----------------------------------------");

        Console.WriteLine();
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

#endif
}
