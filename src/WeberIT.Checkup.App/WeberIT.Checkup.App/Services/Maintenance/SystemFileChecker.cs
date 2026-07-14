using System.Globalization;
using System.IO;
using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Maintenance;

public class SystemFileChecker : ISystemFileChecker
{
    private readonly IMaintenanceProcessRunner _processRunner;
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public SystemFileChecker(
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
                    "Die Systemdateiprüfung wird bereits ausgeführt.",
                Details =
                    "Eine zweite parallele Ausführung von SFC wurde verhindert."
            };
        }

        try
        {
            var sfcPath =
                Path.Combine(
                    Environment.SystemDirectory,
                    "sfc.exe");

            var processResult =
                await _processRunner.RunAsync(
                    sfcPath,
                    ["/scannow"],
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
                    "Die Systemdateiprüfung konnte nicht ausgeführt werden.",
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
                "Die Systemdateiprüfung wurde nicht gestartet.",
                "Die Anforderung der Administratorrechte wurde abgebrochen.");
        }

        if (!processResult.WasStarted)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Systemdateiprüfung konnte nicht gestartet werden.",
                GetErrorDetails(processResult));
        }

        var completeOutput =
            processResult.StandardOutput
            + Environment.NewLine
            + processResult.StandardError;

        var normalizedOutput =
            NormalizeOutput(
                completeOutput);

        if (ContainsUnrepairedFilesMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.ActionRequired,
                "Beschädigte Systemdateien konnten nicht vollständig repariert werden.",
                "SFC hat beschädigte Dateien gefunden, konnte aber mindestens "
                + "einen Teil davon nicht reparieren. Weitere Maßnahmen sind erforderlich.");
        }

        if (ContainsSuccessfulRepairMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.SuccessfulWithRepairs,
                "Beschädigte Systemdateien wurden repariert.",
                "SFC hat Integritätsverletzungen gefunden und die betroffenen "
                + "Systemdateien erfolgreich repariert.");
        }

        if (ContainsNoIntegrityViolationsMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Successful,
                "Keine Integritätsverletzungen gefunden.",
                "Die geschützten Windows-Systemdateien wurden überprüft. "
                + "SFC hat keine Beschädigungen festgestellt.");
        }

        if (ContainsExecutionFailureMessage(
            normalizedOutput))
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Systemdateiprüfung konnte nicht vollständig ausgeführt werden.",
                "SFC hat gemeldet, dass der angeforderte Vorgang nicht "
                + "ausgeführt oder nicht abgeschlossen werden konnte.");
        }

        if (processResult.ExitCode != 0)
        {
            return CreateResult(
                processResult,
                MaintenanceToolStatus.Failed,
                "Die Systemdateiprüfung wurde mit einem Fehler beendet.",
                $"SFC wurde mit Exitcode {processResult.ExitCode} beendet. "
                + "Die technische Ausgabe enthält möglicherweise weitere Hinweise.");
        }

        return CreateResult(
            processResult,
            MaintenanceToolStatus.Failed,
            "Das Ergebnis der Systemdateiprüfung ist nicht eindeutig.",
            "SFC wurde ausgeführt, die Abschlussmeldung konnte jedoch nicht "
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

    private static bool ContainsNoIntegrityViolationsMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "did",
                   "not",
                   "find",
                   "integrity",
                   "violations")
               || ContainsAll(
                   output,
                   "keine",
                   "integrit",
                   "verletzungen",
                   "gefunden");
    }

    private static bool ContainsSuccessfulRepairMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "successfully",
                   "repaired")
               || ContainsAll(
                   output,
                   "dateien",
                   "gefunden",
                   "erfolgreich",
                   "repariert");
    }

    private static bool ContainsUnrepairedFilesMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "unable",
                   "fix",
                   "some")
               || ContainsAll(
                   output,
                   "dateien",
                   "nicht",
                   "repariert");
    }

    private static bool ContainsExecutionFailureMessage(
        string output)
    {
        return ContainsAll(
                   output,
                   "could",
                   "not",
                   "perform",
                   "requested",
                   "operation")
               || ContainsAll(
                   output,
                   "angeforderte",
                   "vorgang",
                   "konnte",
                   "nicht",
                   "ausgef",
                   "werden")
               || ContainsAll(
                   output,
                   "reparaturdienst",
                   "konnte",
                   "nicht",
                   "gestartet",
                   "werden");
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