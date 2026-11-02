
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.31
///
/// Prüft die tatsächliche JSON-Übergabe
/// zwischen Windows-PowerShell und dem
/// Windows-Temp-Executor.
///
/// Verwendet den echten PowerShell-
/// Klassifizierungshelfer, aber ausschließlich
/// simulierte Bereinigungsergebnisse.
///
/// Keine Administratorrechte.
/// Keine produktive Bereinigung.
/// Keine Löschoperationen.
/// Keine temporären Skriptdateien.
/// </summary>
public static class
    WindowsTemporaryFilesWorkerJsonRegressionTests
{
    private const int ExpectedTestCount = 4;

    private const int ProcessTimeoutMilliseconds = 30000;

    private static readonly Type ExecutorType =
        typeof(WindowsTemporaryFilesCleanupExecutor);

    private const string BootstrapScript =
        """
        $ErrorActionPreference = 'Stop'

        try
        {
            $encodedSource =
                [System.Console]::In.ReadToEnd()

            $sourceBytes =
                [System.Convert]::FromBase64String(
                    $encodedSource)

            $sourceText =
                [System.Text.Encoding]::Unicode.GetString(
                    $sourceBytes)

            $scriptBlock =
                [System.Management.Automation.ScriptBlock]::Create(
                    $sourceText)

            & $scriptBlock

            exit 0
        }
        catch
        {
            [System.Console]::Error.WriteLine(
                $_.Exception.ToString())

            exit 1
        }
        """;

    public static string RunAll()
    {
        var results = new List<string>();

        /*
         * Ein einziger, nicht erhöhter
         * PowerShell-Prozess erzeugt vier
         * voneinander unabhängige Ergebnisse.
         *
         * Anschließend durchlaufen alle
         * Ergebnisse die echte C#-Auswertung.
         */
        var snapshots =
            CreateWorkerSnapshots();

        Execute(
            "Leeres PowerShell-Ergebnis wird übernommen",
            () => TestEmptyResult(snapshots),
            results);

        Execute(
            "Einzelne Ursache übersteht PowerShell-JSON",
            () => TestSingleReason(snapshots),
            results);

        Execute(
            "Mehrere Ursachen bleiben getrennt",
            () => TestMultipleReasons(snapshots),
            results);

        Execute(
            "Technische Fehler bleiben von Ausschlüssen getrennt",
            () => TestTechnicalFailure(snapshots),
            results);

        if (results.Count != ExpectedTestCount)
        {
            throw new InvalidOperationException(
                "Die PowerShell-JSON-Tests wurden "
                + "nicht vollständig ausgeführt.");
        }

        return
            $"Alle {results.Count} PowerShell-JSON-"
            + "Integrationstests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TESTSZENARIEN PRÜFEN
    // =============================================

    private static void TestEmptyResult(
        IReadOnlyDictionary<
            string,
            CleanupActionCategoryExecutionResult> snapshots)
    {
        var result = GetSnapshot(
            snapshots,
            "empty");

        Require(
            result.WasStarted,
            "Die Ausführung wurde nicht "
            + "als gestartet übernommen.");

        Require(
            result.SkippedEntryCount == 0,
            "Der Überspringzähler ist falsch.");

        Require(
            result.SkippedEntryReasons.Count == 0,
            "Es wurden nicht vorhandene "
            + "Ursachen erzeugt.");

        Require(
            result.FailedEntryCount == 0,
            "Ein unerwarteter technischer "
            + "Fehler wurde gemeldet.");

        Require(
            result.IsSuccessful,
            "Das leere Ergebnis wurde "
            + "nicht als Erfolg erkannt.");
    }

    private static void TestSingleReason(
        IReadOnlyDictionary<
            string,
            CleanupActionCategoryExecutionResult> snapshots)
    {
        var result = GetSnapshot(
            snapshots,
            "single");

        Require(
            result.SkippedEntryCount == 1,
            "Der Gesamtzähler wurde "
            + "falsch übertragen.");

        Require(
            result.SkippedEntryReasons.Count == 1,
            "Die einzelne Ursache wurde "
            + "nicht als Liste übernommen.");

        Require(
            result.SkippedEntryReasons[0].Reason
                == CleanupSkippedEntryReason.ReparsePoint,
            "Der Reparse Point wurde "
            + "falsch klassifiziert.");

        Require(
            result.SkippedEntryReasons[0].Count == 1,
            "Der Ursachenzähler ist falsch.");

        Require(
            result.HasSkippedEntryReasons,
            "Der Anzeigeindikator fehlt.");

        Require(
            result.IsSuccessful,
            "Ein erwarteter Ausschluss wurde "
            + "fälschlich zum technischen Fehler.");
    }

    private static void TestMultipleReasons(
        IReadOnlyDictionary<
            string,
            CleanupActionCategoryExecutionResult> snapshots)
    {
        var result = GetSnapshot(
            snapshots,
            "multiple");

        Require(
            result.SkippedEntryCount == 3,
            "Der Gesamtzähler wurde "
            + "falsch übertragen.");

        Require(
            result.SkippedEntryReasons.Count == 2,
            "Die Ursachenliste wurde "
            + "nicht korrekt übertragen.");

        Require(
            result.SkippedEntryReasons.Any(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.ReadOnly
                    && entry.Count == 2),
            "Die beiden Schreibschutzfälle fehlen.");

        Require(
            result.SkippedEntryReasons.Any(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.LockedByProcess
                    && entry.Count == 1),
            "Die Prozesssperre fehlt.");

        Require(
            result.SkippedEntryReasons.Sum(
                entry => entry.Count) == 3,
            "Die einzelnen Ursachen ergeben "
            + "nicht die gemeldete Gesamtzahl.");

        Require(
            result.FailedEntryCount == 0,
            "Die erwartete Prozesssperre "
            + "wurde als technischer Fehler gewertet.");

        Require(
            result.IsSuccessful,
            "Das kontrollierte Ergebnis wurde "
            + "nicht als Erfolg erkannt.");
    }

    private static void TestTechnicalFailure(
        IReadOnlyDictionary<
            string,
            CleanupActionCategoryExecutionResult> snapshots)
    {
        var result = GetSnapshot(
            snapshots,
            "technical");

        Require(
            result.SkippedEntryCount == 1,
            "Der erwartete Ausschluss "
            + "wurde nicht übernommen.");

        Require(
            result.SkippedEntryReasons.Count == 1,
            "Die dokumentierte Ursache fehlt.");

        Require(
            result.SkippedEntryReasons[0].Reason
                == CleanupSkippedEntryReason.AlreadyMissing,
            "Die Ursache wurde falsch übertragen.");

        Require(
            result.FailedEntryCount == 1,
            "Der technische Fehler ging verloren.");

        Require(
            result.DeletedFileCount == 1,
            "Die simulierte Änderung "
            + "wurde nicht übernommen.");

        Require(
            !result.IsSuccessful,
            "Das Ergebnis wurde trotz "
            + "technischem Fehler als "
            + "vollständiger Erfolg gewertet.");

        Require(
            result.IsPartiallySuccessful,
            "Der Teilerfolg wurde nicht "
            + "korrekt erkannt.");
    }

    private static CleanupActionCategoryExecutionResult
        GetSnapshot(
            IReadOnlyDictionary<
                string,
                CleanupActionCategoryExecutionResult> snapshots,
            string name)
    {
        if (!snapshots.TryGetValue(
                name,
                out var result))
        {
            throw new InvalidOperationException(
                "Das erwartete PowerShell-"
                + "Ergebnis fehlt: "
                + name);
        }

        return result;
    }

    // =============================================
    // POWERSHELL ERGEBNISSE ERZEUGEN
    // =============================================

    private static Dictionary<
        string,
        CleanupActionCategoryExecutionResult>
        CreateWorkerSnapshots()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die PowerShell-JSON-Tests "
                + "benötigen Windows.");
        }

        var source =
            WindowsTemporaryFilesSkipReasonPowerShellScript.Source
            + Environment.NewLine
            + PowerShellScenarios;

        var output =
            ExecutePowerShell(source);

        var snapshots =
            new Dictionary<
                string,
                CleanupActionCategoryExecutionResult>(
                    StringComparer.Ordinal);

        var lines =
            output.Split(
                [
                    "\r\n",
                    "\n",
                    "\r"
                ],
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            /*
             * Jede Zeile muss genau ein
             * JSON-Ergebnis enthalten.
             *
             * Unerwartete PowerShell-Ausgaben
             * sollen nicht stillschweigend
             * ignoriert werden.
             */
            using var document =
                JsonDocument.Parse(line);

            var root =
                document.RootElement;

            var scenario =
                root.GetProperty("Scenario")
                    .GetString();

            if (string.IsNullOrWhiteSpace(
                    scenario))
            {
                throw new InvalidOperationException(
                    "Das PowerShell-Ergebnis besitzt "
                    + "keinen Szenarionamen.");
            }

            var workerJson =
                root.GetProperty("Worker")
                    .GetRawText();

            var categoryResult =
                ConvertWorkerJson(workerJson);

            if (!snapshots.TryAdd(
                    scenario,
                    categoryResult))
            {
                throw new InvalidOperationException(
                    "Das PowerShell-Ergebnis wurde "
                    + "mehrfach übertragen: "
                    + scenario);
            }
        }

        if (snapshots.Count != ExpectedTestCount)
        {
            throw new InvalidOperationException(
                "Die PowerShell-Ausführung hat "
                + "nicht alle erwarteten "
                + "Ergebnisse geliefert."
                + Environment.NewLine
                + output);
        }

        return snapshots;
    }

    private static string ExecutePowerShell(
        string source)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis "
                + "konnte nicht bestimmt werden.");
        }

        var powerShellPath =
            Path.Combine(
                windowsDirectory,
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");

        if (!File.Exists(powerShellPath))
        {
            throw new FileNotFoundException(
                "Windows-PowerShell wurde "
                + "nicht gefunden.",
                powerShellPath);
        }

        var encodedBootstrap =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    BootstrapScript));

        var encodedSource =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    source));

        var startInfo =
            new ProcessStartInfo
            {
                FileName = powerShellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedBootstrap);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Die PowerShell-Testausführung "
                + "konnte nicht gestartet werden.");
        }

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        /*
         * Wie bei den bereits erfolgreichen
         * PowerShell-Verhaltenstests:
         *
         * Das Testskript wird kodiert über
         * stdin übertragen, nicht über
         * eine lange Kommandozeile.
         */
        process.StandardInput.Write(
            encodedSource);

        process.StandardInput.Close();

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
                "Die PowerShell-JSON-Prüfung "
                + "hat das Zeitlimit überschritten.");
        }

        var output =
            outputTask.GetAwaiter().GetResult();

        var error =
            errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Die PowerShell-JSON-Prüfung "
                + "ist fehlgeschlagen."
                + Environment.NewLine
                + "Exitcode: "
                + process.ExitCode
                + Environment.NewLine
                + error
                + Environment.NewLine
                + output);
        }

        return output;
    }

    // =============================================
    // ECHTE EXECUTOR-AUSWERTUNG
    // =============================================

    private static CleanupActionCategoryExecutionResult
        ConvertWorkerJson(
            string json)
    {
        object?[] deserializeArguments =
        [
            json,
            null
        ];

        var success =
            GetMethod("TryDeserializeWorkerResult")
                .Invoke(
                    null,
                    deserializeArguments);

        Require(
            success is true,
            "Das PowerShell-JSON konnte "
            + "nicht deserialisiert werden.");

        var worker =
            deserializeArguments[1]
            ?? throw new InvalidOperationException(
                "Das Worker-Ergebnis fehlt.");

        GetMethod("ValidateWorkerResult")
            .Invoke(
                null,
                [worker]);

        var category =
            new CleanupActionCategory
            {
                Category =
                    CleanupCategoryType.WindowsTemporaryFiles,

                Title =
                    "Windows-Temp"
            };

        var converted =
            GetMethod("CreateCategoryResult")
                .Invoke(
                    null,
                    [
                        category,
                        worker
                    ]);

        return converted
            as CleanupActionCategoryExecutionResult
            ?? throw new InvalidOperationException(
                "Die Ergebnisübernahme "
                + "hat kein gültiges "
                + "Kategorieergebnis erzeugt.");
    }

    private static MethodInfo GetMethod(
        string name)
    {
        return ExecutorType.GetMethod(
                   name,
                   BindingFlags.NonPublic
                   | BindingFlags.Static)
               ?? throw new InvalidOperationException(
                   "Die Executor-Methode fehlt: "
                   + name);
    }

    // =============================================
    // POWERSHELL-TESTSZENARIEN
    // =============================================

    private const string PowerShellScenarios =
        """
        $ErrorActionPreference = 'Stop'

        $windowsDirectory =
            [System.Environment]::GetFolderPath(
                [System.Environment+SpecialFolder]::Windows)

        if ([string]::IsNullOrWhiteSpace($windowsDirectory))
        {
            throw 'Das Windows-Verzeichnis wurde nicht gefunden.'
        }

        $targetPath =
            [System.IO.Path]::GetFullPath(
                [System.IO.Path]::Combine(
                    $windowsDirectory,
                    'Temp'))

        function Initialize-Result
        {
            $script:skipReasonCounts = @{}

            $script:result = [ordered]@{
                WasStarted = $true
                WasCancelled = $false
                TargetPath = $targetPath
                DeletedFileCount = [int64]0
                DeletedDirectoryCount = [int64]0
                DeletedSizeBytes = [uint64]0
                FailedEntryCount = [int64]0
                SkippedEntryCount = [int64]0
                SkippedEntryReasons = @()
                ErrorMessage = ''
            }
        }

        function Write-TestResult
        {
            param(
                [string]$Scenario
            )

            $script:result.SkippedEntryReasons =
                @(Get-SkippedReasonRecords)

            $envelope = [ordered]@{
                Scenario = $Scenario
                Worker = $script:result
            }

            $json =
                $envelope |
                    ConvertTo-Json -Compress -Depth 6

            [System.Console]::Out.WriteLine(
                $json)
        }

        # ==========================================
        # TEST 1: KEINE ÜBERSPRINGURSACHEN
        # ==========================================

        Initialize-Result

        Write-TestResult -Scenario 'empty'

        # ==========================================
        # TEST 2: EINE EINZELNE URSACHE
        # ==========================================

        Initialize-Result

        Add-SkippedEntry -Reason 'ReparsePoint'

        Write-TestResult -Scenario 'single'

        # ==========================================
        # TEST 3: MEHRERE URSACHEN
        # ==========================================

        Initialize-Result

        Add-SkippedEntry -Reason 'ReadOnly'
        Add-SkippedEntry -Reason 'ReadOnly'

        $sharingException =
            New-Object System.IO.IOException(
                'Simulierte Prozesssperre',
                -2147024864)

        Record-FileSystemException `
            -Exception $sharingException

        Write-TestResult -Scenario 'multiple'

        # ==========================================
        # TEST 4: TECHNISCHER FEHLER
        # ==========================================

        Initialize-Result

        Add-SkippedEntry -Reason 'AlreadyMissing'

        $accessDenied =
            New-Object System.UnauthorizedAccessException(
                'Simulierter unbekannter Zugriffsfehler')

        Record-FileSystemException `
            -Exception $accessDenied

        # Nur ein simulierter Ergebniszähler.
        # Es wird keine echte Datei gelöscht.

        $script:result.DeletedFileCount = [int64]1

        Write-TestResult -Scenario 'technical'
        """;

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
                "PowerShell-JSON-Integrationstest "
                + "fehlgeschlagen: "
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
            throw new InvalidOperationException(
                message);
        }
    }
}

#endif
