
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Führt die kontrollierte Windows-Temp-Bereinigung
/// über einen gesonderten, erhöhten PowerShell-Prozess aus.
///
/// Sprint 55.13.27:
/// - Ausgelagerten Skriptgenerator verwenden.
/// - Überspringursachen aus Worker-JSON übernehmen.
/// - Ursachen vor Übernahme streng validieren.
///
/// Die bestehenden Prüfungen für Aktionsplan,
/// Administratorrechte, Skriptintegrität und
/// Zielpfad bleiben bestehen.
/// </summary>
public sealed class WindowsTemporaryFilesCleanupExecutor
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    private const int MaximumDiagnosticLength =
        3000;

    private readonly IMaintenanceProcessRunner
        _processRunner;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };

    public WindowsTemporaryFilesCleanupExecutor(
        IMaintenanceProcessRunner processRunner,
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator)
    {
        ArgumentNullException.ThrowIfNull(
            processRunner);

        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        _processRunner =
            processRunner;

        _executionCoordinator =
            executionCoordinator;
    }

    public async Task<CleanupActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidatePlan(
            plan);

        var startedAt =
            DateTimeOffset.Now;

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledBeforeStartResult(
                plan.Id,
                startedAt);
        }

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                plan.ActionCode,
                plan.ActionTitle);

        if (executionLease is null)
        {
            return CreateBlockedResult(
                plan.Id,
                startedAt,
                BuildExecutionBlockedMessage());
        }

        var temporaryDirectory =
            string.Empty;

        try
        {
            temporaryDirectory =
                CreateTemporaryDirectory();

            var cancellationMarkerPath =
                Path.Combine(
                    temporaryDirectory,
                    "cancel.requested");

            var resultFilePath =
                Path.Combine(
                    temporaryDirectory,
                    "worker-result.json");

            var cleanupScriptPath =
                Path.Combine(
                    temporaryDirectory,
                    "windows-temp-cleanup.ps1");

            /*
             * Sprint 55.13.27:
             *
             * BuildCleanupScript verwendet jetzt
             * ausschließlich den ausgelagerten
             * und zuvor geprüften Skriptgenerator.
             */
            var cleanupScript =
                BuildCleanupScript(
                    cancellationMarkerPath,
                    resultFilePath);

            var expectedScriptHash =
                WriteCleanupScript(
                    cleanupScriptPath,
                    cleanupScript);

            var powershellPath =
                ResolvePowerShellPath();

            var verifiedLauncherScript =
                BuildVerifiedLauncherScript(
                    cleanupScriptPath,
                    expectedScriptHash);

            var arguments =
                new List<string>
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    verifiedLauncherScript
                };

            using var cancellationRegistration =
                cancellationToken.Register(
                    () =>
                        TryCreateCancellationMarker(
                            cancellationMarkerPath));

            var processResult =
                await _processRunner.RunAsync(
                    powershellPath,
                    arguments,
                    requiresAdministrator: true);

            if (processResult.ElevationWasCancelled)
            {
                return CreateCancelledBeforeStartResult(
                    plan.Id,
                    startedAt);
            }

            if (!processResult.WasStarted)
            {
                return CreateTechnicalFailureResult(
                    plan.Id,
                    startedAt,
                    BuildProcessFailureMessage(
                        processResult));
            }

            if (!processResult.WasElevated)
            {
                return CreateTechnicalFailureResult(
                    plan.Id,
                    startedAt,
                    "Die Windows-Temp-Bereinigung wurde nicht "
                    + "mit bestätigten Administratorrechten "
                    + "ausgeführt.");
            }

            var workerResult =
                ParseWorkerResult(
                    resultFilePath,
                    processResult);

            ValidateWorkerResult(
                workerResult);

            var plannedCategory =
                plan.CleanupCategories.Single();

            var categoryResult =
                CreateCategoryResult(
                    plannedCategory,
                    workerResult);

            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasCancelled =
                    workerResult.WasCancelled,

                ErrorMessage =
                    DetermineOverallErrorMessage(
                        categoryResult,
                        processResult,
                        workerResult),

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now,

                CategoryResults =
                    new List<
                        CleanupActionCategoryExecutionResult>
                    {
                        categoryResult
                    }
            };
        }
        catch (Exception exception)
        {
            return CreateTechnicalFailureResult(
                plan.Id,
                startedAt,
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die erhöhte Windows-Temp-Bereinigung "
                      + "wurde unerwartet abgebrochen."
                    : exception.Message);
        }
        finally
        {
            TryDeleteTemporaryDirectory(
                temporaryDirectory);
        }
    }

    // =============================================
    // AKTIONSPLAN VALIDIEREN
    // =============================================

    private static void ValidatePlan(
        CheckupTaskActionPlan plan)
    {
        plan.Validate();

        if (!string.Equals(
                plan.TaskCode,
                RestoreCapacityTaskCode,
                StringComparison.Ordinal)
            && !string.Equals(
                plan.TaskCode,
                ControlledCleanupTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan gehört nicht zu einer "
                + "freigegebenen Bereinigungsaufgabe.");
        }

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionscode ist für die erhöhte "
                + "Windows-Temp-Bereinigung nicht freigegeben.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Der Windows-Temp-Bereinigungsplan darf "
                + "keine frei definierten externen Befehle "
                + "enthalten.");
        }

        if (plan.CleanupCategories.Count != 1)
        {
            throw new InvalidOperationException(
                "Für die erhöhte Windows-Temp-Bereinigung "
                + "muss genau eine Kategorie ausgewählt sein.");
        }

        if (!plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Der Windows-Temp-Bereinigungsplan muss "
                + "ausdrücklich Administratorrechte anfordern.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Die Windows-Temp-Bereinigung darf keinen "
                + "automatischen Neustart vorsehen.");
        }

        var category =
            plan.CleanupCategories.Single();

        category.Validate();

        if (category.Category
            != CleanupCategoryType.WindowsTemporaryFiles)
        {
            throw new InvalidOperationException(
                "Der übergebene Bereinigungsplan enthält "
                + "keine Windows-Temp-Kategorie.");
        }
    }

    // =============================================
    // TEMPORÄREN ARBEITSBEREICH ERSTELLEN
    // =============================================

    private static string CreateTemporaryDirectory()
    {
        var temporaryRoot =
            Path.GetFullPath(
                Path.GetTempPath());

        if (IsNetworkPath(
                temporaryRoot))
        {
            throw new InvalidOperationException(
                "Die erhöhte Bereinigung darf keine "
                + "temporären Netzwerkpfade verwenden.");
        }

        var temporaryDirectory =
            Path.Combine(
                temporaryRoot,
                "WeberIT.Checkup",
                "WindowsTempCleanup",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            temporaryDirectory);

        return temporaryDirectory;
    }

    // =============================================
    // SKRIPT SCHREIBEN UND HASH ERMITTELN
    // =============================================

    private static string WriteCleanupScript(
        string cleanupScriptPath,
        string cleanupScript)
    {
        if (string.IsNullOrWhiteSpace(
                cleanupScriptPath))
        {
            throw new ArgumentException(
                "Für das erhöhte Bereinigungsskript "
                + "wurde kein Zielpfad angegeben.",
                nameof(cleanupScriptPath));
        }

        if (string.IsNullOrWhiteSpace(
                cleanupScript))
        {
            throw new ArgumentException(
                "Das erhöhte Bereinigungsskript ist leer.",
                nameof(cleanupScript));
        }

        var normalizedScriptPath =
            Path.GetFullPath(
                cleanupScriptPath);

        if (IsNetworkPath(
                normalizedScriptPath))
        {
            throw new InvalidOperationException(
                "Das erhöhte Bereinigungsskript darf nicht "
                + "auf einem Netzwerkpfad gespeichert werden.");
        }

        var parentDirectory =
            Path.GetDirectoryName(
                normalizedScriptPath);

        if (string.IsNullOrWhiteSpace(
                parentDirectory)
            || !Directory.Exists(
                parentDirectory))
        {
            throw new DirectoryNotFoundException(
                "Der temporäre Ordner für das erhöhte "
                + "Bereinigungsskript ist nicht vorhanden.");
        }

        File.WriteAllText(
            normalizedScriptPath,
            cleanupScript,
            Encoding.Unicode);

        if (!File.Exists(
                normalizedScriptPath))
        {
            throw new IOException(
                "Das erhöhte Bereinigungsskript konnte "
                + "nicht zuverlässig gespeichert werden.");
        }

        var writtenContent =
            File.ReadAllText(
                normalizedScriptPath,
                Encoding.Unicode);

        var scriptHashBytes =
            SHA256.HashData(
                Encoding.Unicode.GetBytes(
                    writtenContent));

        return Convert.ToHexString(
            scriptHashBytes);
    }

    // =============================================
    // WINDOWS-POWERSHELL ERMITTELN
    // =============================================

    private static string ResolvePowerShellPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die erhöhte Windows-Temp-Bereinigung "
                + "kann nur unter Windows ausgeführt werden.");
        }

        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis konnte nicht "
                + "eindeutig bestimmt werden.");
        }

        var powershellPath =
            Path.GetFullPath(
                Path.Combine(
                    windowsDirectory,
                    "System32",
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe"));

        if (IsNetworkPath(
                powershellPath)
            || !File.Exists(
                powershellPath))
        {
            throw new FileNotFoundException(
                "Die lokale Windows-PowerShell wurde nicht "
                + "am erwarteten Systempfad gefunden.",
                powershellPath);
        }

        return powershellPath;
    }

    // =============================================
    // INTEGRITÄTSGEPRÜFTEN STARTER ERZEUGEN
    // =============================================

    private static string BuildVerifiedLauncherScript(
        string cleanupScriptPath,
        string expectedScriptHash)
    {
        var escapedCleanupScriptPath =
            EscapePowerShellLiteral(
                cleanupScriptPath);

        var escapedExpectedScriptHash =
            EscapePowerShellLiteral(
                expectedScriptHash);

        return
            "$ErrorActionPreference = 'Stop'; "
            + "$scriptPath = '"
            + escapedCleanupScriptPath
            + "'; "
            + "$expectedHash = '"
            + escapedExpectedScriptHash
            + "'; "
            + "$scriptContent = "
            + "[System.IO.File]::ReadAllText("
            + "$scriptPath, "
            + "[System.Text.Encoding]::Unicode); "
            + "$scriptBytes = "
            + "[System.Text.Encoding]::Unicode.GetBytes("
            + "$scriptContent); "
            + "$sha256 = "
            + "[System.Security.Cryptography.SHA256]::Create(); "
            + "try { "
            + "$actualHash = "
            + "[System.BitConverter]::ToString("
            + "$sha256.ComputeHash($scriptBytes))"
            + ".Replace('-', ''); "
            + "} finally { "
            + "$sha256.Dispose(); "
            + "} "
            + "if (-not [string]::Equals("
            + "$actualHash, "
            + "$expectedHash, "
            + "[System.StringComparison]::OrdinalIgnoreCase)) "
            + "{ "
            + "throw 'Die Integritätsprüfung des erhöhten "
            + "Bereinigungsskripts ist fehlgeschlagen.'; "
            + "} "
            + "& $scriptPath;";
    }

    // =============================================
    // NEUEN SKRIPTGENERATOR VERWENDEN
    // =============================================

    private static string BuildCleanupScript(
        string cancellationMarkerPath,
        string resultFilePath)
    {
        /*
         * Diese Methode bleibt absichtlich
         * als eindeutiger Einstiegspunkt
         * erhalten.
         *
         * Vorhandene Regressionstests
         * verwenden diesen Vertrag.
         *
         * Der eigentliche PowerShell-Code
         * liegt nun vollständig in:
         *
         * WindowsTemporaryFilesCleanupPowerShellScript
         */
        return
            WindowsTemporaryFilesCleanupPowerShellScript
                .Build(
                    cancellationMarkerPath,
                    resultFilePath);
    }

    private static string EscapePowerShellLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    // =============================================
    // WORKER-ERGEBNIS EINLESEN
    // =============================================

    private static WindowsTemporaryFilesWorkerResult
        ParseWorkerResult(
            string resultFilePath,
            ProcessExecutionResult processResult)
    {
        var resultFileReadError =
            string.Empty;

        if (File.Exists(
                resultFilePath))
        {
            try
            {
                var resultFileContent =
                    File.ReadAllText(
                        resultFilePath);

                if (TryDeserializeWorkerResult(
                        resultFileContent,
                        out var fileResult))
                {
                    return fileResult;
                }

                resultFileReadError =
                    "Die Ergebnisdatei war vorhanden, enthielt "
                    + "aber kein gültiges JSON-Ergebnis.";
            }
            catch (Exception exception)
            {
                resultFileReadError =
                    "Die Ergebnisdatei konnte nicht gelesen "
                    + "werden: "
                    + exception.Message;
            }
        }
        else
        {
            resultFileReadError =
                "Die erhöhte Ausführung hat keine "
                + "Ergebnisdatei angelegt.";
        }

        if (TryParseWorkerResultFromOutput(
                processResult.StandardOutput,
                out var outputResult))
        {
            return outputResult;
        }

        var diagnosticParts =
            new List<string>
            {
                "Der erhöhte Bereinigungsprozess lieferte "
                + "kein auswertbares technisches Ergebnis.",

                resultFileReadError
            };

        if (!string.IsNullOrWhiteSpace(
                processResult.StandardError))
        {
            diagnosticParts.Add(
                "PowerShell-Fehlerausgabe:"
                + Environment.NewLine
                + LimitDiagnosticText(
                    processResult.StandardError));
        }

        if (!string.IsNullOrWhiteSpace(
                processResult.StandardOutput))
        {
            diagnosticParts.Add(
                "PowerShell-Standardausgabe:"
                + Environment.NewLine
                + LimitDiagnosticText(
                    processResult.StandardOutput));
        }

        if (!string.IsNullOrWhiteSpace(
                processResult.ErrorMessage))
        {
            diagnosticParts.Add(
                "Prozessfehler:"
                + Environment.NewLine
                + LimitDiagnosticText(
                    processResult.ErrorMessage));
        }

        diagnosticParts.Add(
            "Exitcode: "
            + (processResult.ExitCode?.ToString()
               ?? "nicht verfügbar"));

        throw new InvalidOperationException(
            string.Join(
                Environment.NewLine
                + Environment.NewLine,
                diagnosticParts.Where(
                    part =>
                        !string.IsNullOrWhiteSpace(
                            part))));
    }

    private static bool TryDeserializeWorkerResult(
        string json,
        out WindowsTemporaryFilesWorkerResult result)
    {
        result =
            new WindowsTemporaryFilesWorkerResult();

        if (string.IsNullOrWhiteSpace(
                json))
        {
            return false;
        }

        try
        {
            var deserializedResult =
                JsonSerializer.Deserialize<
                    WindowsTemporaryFilesWorkerResult>(
                    json.Trim(),
                    JsonOptions);

            if (deserializedResult is null)
            {
                return false;
            }

            result =
                deserializedResult;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseWorkerResultFromOutput(
        string standardOutput,
        out WindowsTemporaryFilesWorkerResult result)
    {
        result =
            new WindowsTemporaryFilesWorkerResult();

        if (string.IsNullOrWhiteSpace(
                standardOutput))
        {
            return false;
        }

        var outputLines =
            standardOutput
                .Split(
                    new[]
                    {
                        "\r\n",
                        "\n",
                        "\r"
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    line =>
                        line.Trim())
                .Where(
                    line =>
                        line.StartsWith(
                            "{",
                            StringComparison.Ordinal)
                        && line.EndsWith(
                            "}",
                            StringComparison.Ordinal))
                .Reverse();

        foreach (var outputLine in outputLines)
        {
            if (TryDeserializeWorkerResult(
                    outputLine,
                    out result))
            {
                return true;
            }
        }

        return false;
    }

    private static string LimitDiagnosticText(
        string text)
    {
        var normalizedText =
            text.Trim();

        if (normalizedText.Length
            <= MaximumDiagnosticLength)
        {
            return normalizedText;
        }

        return normalizedText[
                   ..MaximumDiagnosticLength]
               + Environment.NewLine
               + "… technische Ausgabe gekürzt.";
    }

    // =============================================
    // ERGEBNIS VALIDIEREN
    // =============================================

    private static void ValidateWorkerResult(
        WindowsTemporaryFilesWorkerResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.DeletedFileCount < 0
            || result.DeletedDirectoryCount < 0
            || result.FailedEntryCount < 0
            || result.SkippedEntryCount < 0)
        {
            throw new InvalidOperationException(
                "Das erhöhte Bereinigungsergebnis enthält "
                + "ungültige Zählerwerte.");
        }

        if (!result.WasStarted
            && (result.DeletedFileCount > 0
                || result.DeletedDirectoryCount > 0
                || result.DeletedSizeBytes > 0))
        {
            throw new InvalidOperationException(
                "Das erhöhte Bereinigungsergebnis meldet "
                + "Dateiänderungen ohne gestartete Ausführung.");
        }

        if (result.WasStarted)
        {
            if (string.IsNullOrWhiteSpace(
                    result.TargetPath))
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess meldete "
                    + "keinen eindeutigen Zielpfad.");
            }

            var expectedTargetPath =
                ResolveWindowsTemporaryDirectory();

            var reportedTargetPath =
                NormalizeDirectoryPath(
                    result.TargetPath);

            if (!string.Equals(
                    expectedTargetPath,
                    reportedTargetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess meldete "
                    + "einen nicht freigegebenen Zielbereich.");
            }
        }

        if (!result.WasStarted
            && !result.WasCancelled
            && string.IsNullOrWhiteSpace(
                result.ErrorMessage))
        {
            throw new InvalidOperationException(
                "Der erhöhte Bereinigungsprozess wurde "
                + "nicht eindeutig gestartet oder abgebrochen.");
        }
    }

    private static string ResolveWindowsTemporaryDirectory()
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis konnte nicht "
                + "eindeutig bestimmt werden.");
        }

        return NormalizeDirectoryPath(
            Path.Combine(
                windowsDirectory,
                "Temp"));
    }

    private static string NormalizeDirectoryPath(
        string path)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        var rootPath =
            Path.GetPathRoot(
                fullPath);

        if (string.IsNullOrWhiteSpace(
                rootPath))
        {
            throw new InvalidOperationException(
                "Der Pfad besitzt kein eindeutiges "
                + "lokales Stammverzeichnis.");
        }

        if (string.Equals(
                fullPath,
                rootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    // =============================================
    // KATEGORIEERGEBNIS ERSTELLEN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateCategoryResult(
            CleanupActionCategory category,
            WindowsTemporaryFilesWorkerResult workerResult)
    {
        /*
         * Erst jetzt werden die vom erhöhten
         * Prozess übertragenen Ursachennamen
         * in unser persistiertes Enum-Modell
         * übernommen.
         *
         * Ungültige Einträge, Dubletten oder
         * widersprüchliche Zähler führen
         * ausdrücklich zu einem technischen
         * Fehler, statt stillschweigend
         * akzeptiert zu werden.
         */
        var skippedEntryReasons =
            WindowsTemporaryFilesSkipReasonContract
                .Normalize(
                    workerResult.SkippedEntryReasons,
                    workerResult.SkippedEntryCount);

        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.WindowsTemporaryFiles,

            CategoryTitle =
                string.IsNullOrWhiteSpace(
                    category.Title)
                    ? "Windows-Temp"
                    : category.Title,

            TargetPath =
                workerResult.TargetPath,

            WasStarted =
                workerResult.WasStarted,

            WasCancelled =
                workerResult.WasCancelled,

            DeletedFileCount =
                workerResult.DeletedFileCount,

            DeletedDirectoryCount =
                workerResult.DeletedDirectoryCount,

            DeletedSizeBytes =
                workerResult.DeletedSizeBytes,

            FailedEntryCount =
                workerResult.FailedEntryCount,

            SkippedEntryCount =
                workerResult.SkippedEntryCount,

            SkippedEntryReasons =
                skippedEntryReasons,

            ErrorMessage =
                workerResult.ErrorMessage
        };
    }

    // =============================================
    // GESAMTSTATUS BESTIMMEN
    // =============================================

    private static string DetermineOverallErrorMessage(
        CleanupActionCategoryExecutionResult categoryResult,
        ProcessExecutionResult processResult,
        WindowsTemporaryFilesWorkerResult workerResult)
    {
        if (workerResult.WasCancelled)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(
                workerResult.ErrorMessage))
        {
            return workerResult.ErrorMessage;
        }

        if (processResult.ExitCode.HasValue
            && processResult.ExitCode.Value != 0)
        {
            if (!string.IsNullOrWhiteSpace(
                    processResult.StandardError))
            {
                return
                    "Die erhöhte PowerShell-Ausführung wurde "
                    + "mit einem Fehler beendet: "
                    + processResult.StandardError.Trim();
            }

            return
                "Die erhöhte Windows-Temp-Bereinigung "
                + "wurde mit Exitcode "
                + processResult.ExitCode.Value
                + " beendet.";
        }

        if (categoryResult.IsSuccessful
            || categoryResult.IsPartiallySuccessful)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(
                processResult.ErrorMessage))
        {
            return processResult.ErrorMessage;
        }

        return
            "Die Windows-Temp-Bereinigung konnte nicht "
            + "ausreichend technisch abgeschlossen werden.";
    }

    private static string BuildProcessFailureMessage(
        ProcessExecutionResult processResult)
    {
        if (!string.IsNullOrWhiteSpace(
                processResult.ErrorMessage))
        {
            return processResult.ErrorMessage;
        }

        if (!string.IsNullOrWhiteSpace(
                processResult.StandardError))
        {
            return
                "Die erhöhte Windows-Temp-Bereinigung "
                + "konnte nicht gestartet werden: "
                + processResult.StandardError.Trim();
        }

        return
            "Die erhöhte Windows-Temp-Bereinigung "
            + "konnte nicht technisch gestartet werden.";
    }

    // =============================================
    // AUSFÜHRUNGSKOORDINATION
    // =============================================

    private string BuildExecutionBlockedMessage()
    {
        if (!_executionCoordinator.IsExecutionRunning)
        {
            return
                "Die Windows-Temp-Bereinigung konnte "
                + "nicht exklusiv gesperrt werden.";
        }

        if (string.IsNullOrWhiteSpace(
                _executionCoordinator.ActiveActionTitle))
        {
            return
                "Eine andere technische Systemaktion "
                + "wird bereits ausgeführt.";
        }

        return
            "Die Windows-Temp-Bereinigung kann noch nicht "
            + "gestartet werden, weil bereits folgende "
            + "Aktion läuft: "
            + _executionCoordinator.ActiveActionTitle;
    }

    private static CleanupActionExecutionResult
        CreateBlockedResult(
            Guid planId,
            DateTimeOffset startedAt,
            string errorMessage)
    {
        return new CleanupActionExecutionResult
        {
            PlanId =
                planId,

            WasBlocked =
                true,

            ErrorMessage =
                errorMessage,

            StartedAt =
                startedAt,

            FinishedAt =
                DateTimeOffset.Now
        };
    }

    private static CleanupActionExecutionResult
        CreateCancelledBeforeStartResult(
            Guid planId,
            DateTimeOffset startedAt)
    {
        return new CleanupActionExecutionResult
        {
            PlanId =
                planId,

            WasCancelled =
                true,

            StartedAt =
                startedAt,

            FinishedAt =
                DateTimeOffset.Now
        };
    }

    private static CleanupActionExecutionResult
        CreateTechnicalFailureResult(
            Guid planId,
            DateTimeOffset startedAt,
            string errorMessage)
    {
        return new CleanupActionExecutionResult
        {
            PlanId =
                planId,

            ErrorMessage =
                errorMessage,

            StartedAt =
                startedAt,

            FinishedAt =
                DateTimeOffset.Now
        };
    }

    // =============================================
    // ABBRUCHMARKER UND AUFRÄUMEN
    // =============================================

    private static void TryCreateCancellationMarker(
        string cancellationMarkerPath)
    {
        try
        {
            var parentDirectory =
                Path.GetDirectoryName(
                    cancellationMarkerPath);

            if (string.IsNullOrWhiteSpace(
                parentDirectory)
                || !Directory.Exists(
                    parentDirectory))
            {
                return;
            }

            File.WriteAllText(
                cancellationMarkerPath,
                "cancel");
        }
        catch
        {
            // Ein fehlgeschlagener Abbruchmarker darf
            // den erhöhten Prozess nicht gewaltsam beenden.
        }
    }

    private static void TryDeleteTemporaryDirectory(
        string temporaryDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                temporaryDirectory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(
                    temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
        catch
        {
            // Temporäre Kommunikationsdaten dürfen die
            // fachliche Ergebnisverarbeitung nicht überlagern.
        }
    }

    private static bool IsNetworkPath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }

    // =============================================
    // JSON-DATENVERTRAG DES ERHÖHTEN WORKERS
    // =============================================

    private sealed class WindowsTemporaryFilesWorkerResult
    {
        public bool WasStarted { get; set; }

        public bool WasCancelled { get; set; }

        public string TargetPath { get; set; } =
            string.Empty;

        public long DeletedFileCount { get; set; }

        public long DeletedDirectoryCount { get; set; }

        public ulong DeletedSizeBytes { get; set; }

        public long FailedEntryCount { get; set; }

        public long SkippedEntryCount { get; set; }

        /*
         * Neues optionales JSON-Feld.
         *
         * Bei älteren Ergebnissen fehlt
         * dieses Feld vollständig.
         *
         * Normalize akzeptiert deshalb
         * null und behält den historischen
         * Gesamtzähler unverändert bei.
         */
        public List<WindowsTemporaryFilesSkipReasonRecord?>?
            SkippedEntryReasons
        {
            get;
            set;
        }

        public string ErrorMessage { get; set; } =
            string.Empty;
    }
}
