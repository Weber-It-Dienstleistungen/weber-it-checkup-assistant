using System.Globalization;
using System.IO;
using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Maintenance;

public class WindowsImageRepairService
    : IWindowsImageRepairService
{
    private const int RestartRequiredExitCode = 3010;

    private readonly IMaintenanceProcessRunner _processRunner;
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public WindowsImageRepairService(
        IMaintenanceProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<MaintenanceToolResult> RunAsync()
    {
        var lockAcquired =
            await _executionLock.WaitAsync(0);

        if (!lockAcquired)
        {
            return new MaintenanceToolResult
            {
                Status = MaintenanceToolStatus.Failed,
                Summary =
                    "Die Windows-Abbildreparatur wird bereits ausgeführt.",
                Details =
                    "Eine zweite parallele Ausführung von DISM wurde verhindert."
            };
        }

        try
        {
            var dismPath =
                Path.Combine(
                    Environment.SystemDirectory,
                    "DISM.exe");

            var processResult =
                await _processRunner.RunAsync(
                    dismPath,
                    [
                        "/Online",
                        "/Cleanup-Image",
                        "/RestoreHealth"
                    ],
                    requiresAdministrator: true);

            return InterpretResult(
                processResult);
        }
        catch (Exception exception)
        {
            return new MaintenanceToolResult
            {
                Status = MaintenanceToolStatus.Failed,
                Summary =
                    "Die Windows-Abbildreparatur konnte nicht ausgeführt werden.",
                Details =
                    BuildExceptionDetails(exception)
            };
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private static MaintenanceToolResult InterpretResult(
        ProcessExecutionResult processResult)
    {
        if (processResult.ElevationWasCancelled)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Windows-Abbildreparatur wurde nicht gestartet.",
                "Die Anforderung der Administratorrechte wurde abgebrochen.");
        }

        if (!processResult.WasStarted)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Windows-Abbildreparatur konnte nicht gestartet werden.",
                GetErrorDetails(processResult));
        }

        var completeOutput =
            processResult.StandardOutput
            + Environment.NewLine
            + processResult.StandardError;

        var normalizedOutput =
            NormalizeOutput(
                completeOutput);

        if (ContainsMissingSourceMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.ActionRequired,
                "Die erforderlichen Reparaturdateien wurden nicht gefunden.",
                "DISM konnte keine geeignete Reparaturquelle finden. "
                + "Möglicherweise ist Windows Update nicht erreichbar oder es "
                + "muss eine passende Windows-Installationsquelle angegeben werden.");
        }

        if (ContainsNonRepairableMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.ActionRequired,
                "Der Windows-Komponentenspeicher kann nicht repariert werden.",
                "DISM hat eine Beschädigung festgestellt, die mit der verwendeten "
                + "Reparaturquelle nicht behoben werden konnte. Weitere Maßnahmen "
                + "oder eine passende Windows-Installationsquelle sind erforderlich.");
        }

        if (ContainsRestartRequiredMessage(
                normalizedOutput)
            || processResult.ExitCode
            == RestartRequiredExitCode)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.RestartRequired,
                "Die Windows-Abbildreparatur wurde abgeschlossen.",
                "Windows muss neu gestartet werden, damit die durchgeführten "
                + "Wartungsmaßnahmen vollständig wirksam werden.");
        }

        if (ContainsSuccessfulCompletionMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Successful,
                "Die Windows-Abbildreparatur wurde erfolgreich abgeschlossen.",
                "DISM hat den Windows-Komponentenspeicher geprüft und den "
                + "Wiederherstellungsvorgang erfolgreich beendet. Anschließend "
                + "kann die Systemdateiprüfung mit SFC ausgeführt werden.");
        }

        if (ContainsPendingOperationMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.RestartRequired,
                "Eine ausstehende Windows-Wartung verhindert die Reparatur.",
                "Windows meldet einen noch nicht abgeschlossenen Wartungsvorgang. "
                + "Der Computer sollte neu gestartet und DISM anschließend erneut "
                + "ausgeführt werden.");
        }

        if (ContainsExecutionFailureMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Windows-Abbildreparatur konnte nicht abgeschlossen werden.",
                "DISM hat während der Prüfung oder Reparatur einen Fehler gemeldet. "
                + "Die technische Ausgabe enthält weitere Informationen.");
        }

        if (processResult.ExitCode != 0)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Windows-Abbildreparatur wurde mit einem Fehler beendet.",
                $"DISM wurde mit Exitcode {processResult.ExitCode} beendet. "
                + "Die technische Ausgabe enthält möglicherweise weitere Hinweise.");
        }

        return CreateResult(
            processResult,
            MaintenanceToolStatus.Failed,
            "Das Ergebnis der Windows-Abbildreparatur ist nicht eindeutig.",
            "DISM wurde ausgeführt, die Abschlussmeldung konnte jedoch nicht "
            + "zuverlässig interpretiert werden. Die technische Ausgabe sollte "
            + "manuell geprüft werden.");
    }

    private static string NormalizeOutput(
        string output)
    {
        var outputWithoutNullCharacters =
            output.Replace(
                "\0",
                string.Empty,
                StringComparison.Ordinal);

        var decomposedOutput =
            outputWithoutNullCharacters
                .ToLowerInvariant()
                .Normalize(
                    NormalizationForm.FormD);

        var builder =
            new StringBuilder(
                decomposedOutput.Length);

        var previousCharacterWasSpace =
            false;

        foreach (var character in decomposedOutput)
        {
            var unicodeCategory =
                CharUnicodeInfo.GetUnicodeCategory(
                    character);

            if (unicodeCategory
                == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousCharacterWasSpace = false;
                continue;
            }

            if (!previousCharacterWasSpace)
            {
                builder.Append(' ');
                previousCharacterWasSpace = true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static bool ContainsSuccessfulCompletionMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "restore",
                   "operation",
                   "completed",
                   "successfully")
               || ContainsAll(
                   output,
                   "operation",
                   "completed",
                   "successfully")
               || ContainsAll(
                   output,
                   "wiederherstellungsvorgang",
                   "erfolgreich",
                   "abgeschlossen")
               || ContainsAll(
                   output,
                   "vorgang",
                   "erfolgreich",
                   "beendet");
    }

    private static bool ContainsMissingSourceMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "source",
                   "files",
                   "could",
                   "not",
                   "be",
                   "found")
               || ContainsAll(
                   output,
                   "source",
                   "files",
                   "not",
                   "found")
               || ContainsAll(
                   output,
                   "quelldateien",
                   "nicht",
                   "gefunden")
               || output.Contains(
                   "0x800f081f",
                   StringComparison.Ordinal)
               || output.Contains(
                   "0x800f0906",
                   StringComparison.Ordinal);
    }

    private static bool ContainsNonRepairableMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "component",
                   "store",
                   "cannot",
                   "be",
                   "repaired")
               || ContainsAll(
                   output,
                   "component",
                   "store",
                   "not",
                   "repairable")
               || ContainsAll(
                   output,
                   "komponentenspeicher",
                   "kann",
                   "nicht",
                   "repariert",
                   "werden")
               || ContainsAll(
                   output,
                   "komponentenspeicher",
                   "nicht",
                   "reparierbar");
    }

    private static bool ContainsRestartRequiredMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "restart",
                   "required")
               || ContainsAll(
                   output,
                   "reboot",
                   "required")
               || ContainsAll(
                   output,
                   "neustart",
                   "erforderlich");
    }

    private static bool ContainsPendingOperationMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "pending",
                   "actions",
                   "restart")
               || ContainsAll(
                   output,
                   "pending",
                   "operation",
                   "reboot")
               || ContainsAll(
                   output,
                   "ausstehende",
                   "aktionen",
                   "neustart")
               || ContainsAll(
                   output,
                   "ausstehender",
                   "vorgang",
                   "neustart");
    }

    private static bool ContainsExecutionFailureMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "dism",
                   "failed")
               || ContainsAll(
                   output,
                   "operation",
                   "failed")
               || ContainsAll(
                   output,
                   "vorgang",
                   "fehlgeschlagen")
               || ContainsAll(
                   output,
                   "fehler",
                   "dism")
               || output.Contains(
                   "error 0x",
                   StringComparison.Ordinal)
               || output.Contains(
                   "fehler 0x",
                   StringComparison.Ordinal);
    }

    private static bool ContainsAll(
        string output,
        params string[] fragments)
    {
        return fragments.All(
            fragment =>
                output.Contains(
                    fragment,
                    StringComparison.Ordinal));
    }

    private static MaintenanceToolResult CreateResult(
        ProcessExecutionResult processResult,
        MaintenanceToolStatus status,
        string summary,
        string details)
    {
        var additionalError =
            processResult.ErrorMessage;

        if (!string.IsNullOrWhiteSpace(additionalError)
            && !details.Contains(
                additionalError,
                StringComparison.OrdinalIgnoreCase))
        {
            details +=
                Environment.NewLine
                + Environment.NewLine
                + $"Zusätzliche Information: {additionalError}";
        }

        return new MaintenanceToolResult
        {
            Status = status,
            Summary = summary,
            Details = details,
            StandardOutput = CleanDisplayedOutput(
                processResult.StandardOutput),
            StandardError = CleanDisplayedOutput(
                processResult.StandardError),
            ExitCode = processResult.ExitCode,
            StartedAt = processResult.StartedAt,
            FinishedAt = processResult.FinishedAt
        };
    }

    private static string CleanDisplayedOutput(
        string output)
    {
        return output.Replace(
            "\0",
            string.Empty,
            StringComparison.Ordinal);
    }

    private static string GetErrorDetails(
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
            return CleanDisplayedOutput(
                processResult.StandardError);
        }

        return "Der Prozess konnte nicht gestartet werden. "
               + "Es wurden keine weiteren Fehlerdetails zurückgegeben.";
    }

    private static string BuildExceptionDetails(
        Exception exception)
    {
        if (string.IsNullOrWhiteSpace(
            exception.Message))
        {
            return "Es sind keine weiteren Fehlerdetails verfügbar.";
        }

        return $"Technische Details: {exception.Message}";
    }
}