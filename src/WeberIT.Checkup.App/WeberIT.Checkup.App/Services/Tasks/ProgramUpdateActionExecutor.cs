using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class ProgramUpdateActionExecutor :
    IProgramUpdateActionExecutor
{
    private const string SupportedTaskCode =
        "task.program-updates.available";

    private const string SupportedActionCode =
        "action.program-updates.selected-upgrades";

    private const string SupportedExecutable =
        "winget.exe";

    private const string SupportedSource =
        "winget";

    private readonly IMaintenanceProcessRunner
        _processRunner;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    public ProgramUpdateActionExecutor(
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

    public async Task<ProgramUpdateActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidatePlan(
            plan);

        var startedAt =
            DateTimeOffset.Now;

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                plan.ActionCode,
                plan.ActionTitle);

        if (executionLease is null)
        {
            return new ProgramUpdateActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasBlocked =
                    true,

                ErrorMessage =
                    BuildExecutionBlockedMessage(),

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now
            };
        }

        var commandResults =
            new List<ProcessExecutionResult>();

        try
        {
            foreach (var command in plan.Commands)
            {
                var commandResult =
                    await _processRunner.RunAsync(
                        command.FileName,
                        command.Arguments,
                        command.RequiresAdministrator);

                commandResults.Add(
                    commandResult);

                if (!IsSuccessful(
                        commandResult))
                {
                    return new ProgramUpdateActionExecutionResult
                    {
                        PlanId =
                            plan.Id,

                        ErrorMessage =
                            BuildCommandFailureMessage(
                                commandResult),

                        StartedAt =
                            startedAt,

                        FinishedAt =
                            DateTimeOffset.Now,

                        CommandResults =
                            commandResults
                    };
                }
            }

            return new ProgramUpdateActionExecutionResult
            {
                PlanId =
                    plan.Id,

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now,

                CommandResults =
                    commandResults
            };
        }
        catch (Exception exception)
        {
            return new ProgramUpdateActionExecutionResult
            {
                PlanId =
                    plan.Id,

                ErrorMessage =
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Die kontrollierte WinGet-Aktion "
                          + "wurde unerwartet abgebrochen."
                        : exception.Message,

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now,

                CommandResults =
                    commandResults
            };
        }
    }

    private static void ValidatePlan(
        CheckupTaskActionPlan plan)
    {
        plan.Validate();

        if (!string.Equals(
                plan.TaskCode,
                SupportedTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan gehört nicht zu einer "
                + "freigegebenen Programmupdateaufgabe.");
        }

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionscode ist für die kontrollierte "
                + "Programmupdateausführung nicht freigegeben.");
        }

        if (plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Der Programmupdateplan darf keine pauschale "
                + "Ausführung mit Administratorrechten verlangen.");
        }

        var packageIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var command in plan.Commands)
        {
            ValidateCommand(
                command);

            var packageId =
                command.Arguments[2];

            if (!packageIds.Add(
                    packageId))
            {
                throw new InvalidOperationException(
                    $"Das Paket \"{packageId}\" ist im "
                    + "Aktionsplan mehrfach enthalten.");
            }
        }
    }

    private static void ValidateCommand(
        CheckupTaskActionCommandPreview command)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        command.Validate();

        if (!string.Equals(
                command.FileName,
                SupportedExecutable,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan enthält ein nicht "
                + "freigegebenes ausführbares Programm.");
        }

        if (command.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Ein Programmupdatebefehl darf keine "
                + "pauschale Rechteerhöhung anfordern.");
        }

        if (command.Arguments.Count != 11)
        {
            throw new InvalidOperationException(
                "Der WinGet-Befehl besitzt nicht die "
                + "erwartete Argumentstruktur.");
        }

        ValidateFixedArgument(
            command,
            0,
            "upgrade");

        ValidateFixedArgument(
            command,
            1,
            "--id");

        ValidatePackageId(
            command.Arguments[2]);

        ValidateFixedArgument(
            command,
            3,
            "--version");

        ValidateTargetVersion(
            command.Arguments[4]);

        ValidateFixedArgument(
            command,
            5,
            "--source");

        ValidateFixedArgument(
            command,
            6,
            SupportedSource);

        ValidateFixedArgument(
            command,
            7,
            "--exact");

        ValidateFixedArgument(
            command,
            8,
            "--disable-interactivity");

        ValidateFixedArgument(
            command,
            9,
            "--accept-source-agreements");

        ValidateFixedArgument(
            command,
            10,
            "--accept-package-agreements");
    }

    private static void ValidateFixedArgument(
        CheckupTaskActionCommandPreview command,
        int index,
        string expectedValue)
    {
        if (!string.Equals(
                command.Arguments[index],
                expectedValue,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der WinGet-Befehl enthält ein nicht "
                + "freigegebenes Argument.");
        }
    }

    private static void ValidatePackageId(
        string packageId)
    {
        if (string.IsNullOrWhiteSpace(
                packageId))
        {
            throw new InvalidOperationException(
                "Der WinGet-Befehl enthält keine "
                + "gültige Paket-ID.");
        }

        if (!string.Equals(
                packageId,
                packageId.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die Paket-ID enthält unzulässige "
                + "äußere Leerzeichen.");
        }

        if (packageId.Any(
                character =>
                    char.IsControl(
                        character)))
        {
            throw new InvalidOperationException(
                "Die Paket-ID enthält unzulässige "
                + "Steuerzeichen.");
        }
    }

    private static void ValidateTargetVersion(
        string targetVersion)
    {
        if (string.IsNullOrWhiteSpace(
                targetVersion))
        {
            throw new InvalidOperationException(
                "Der WinGet-Befehl enthält keine "
                + "gültige Zielversion.");
        }

        if (!string.Equals(
                targetVersion,
                targetVersion.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die Zielversion enthält unzulässige "
                + "äußere Leerzeichen.");
        }

        if (targetVersion.Any(
                character =>
                    char.IsControl(
                        character)))
        {
            throw new InvalidOperationException(
                "Die Zielversion enthält unzulässige "
                + "Steuerzeichen.");
        }
    }

    private string BuildExecutionBlockedMessage()
    {
        if (!_executionCoordinator.IsExecutionRunning)
        {
            return "Die Systemaktion konnte nicht gesperrt werden.";
        }

        if (string.IsNullOrWhiteSpace(
                _executionCoordinator.ActiveActionTitle))
        {
            return "Eine andere Systemaktion wird bereits ausgeführt.";
        }

        return "Die Systemaktion kann noch nicht gestartet werden, "
               + "weil bereits folgende Aktion läuft: "
               + _executionCoordinator.ActiveActionTitle;
    }

    private static bool IsSuccessful(
        ProcessExecutionResult result)
    {
        return result.WasStarted
               && !result.ElevationWasCancelled
               && result.ExitCode == 0;
    }

    private static string BuildCommandFailureMessage(
        ProcessExecutionResult result)
    {
        if (result.ElevationWasCancelled)
        {
            return "Die Anforderung der Administratorrechte "
                   + "wurde abgebrochen.";
        }

        if (!result.WasStarted)
        {
            return string.IsNullOrWhiteSpace(
                result.ErrorMessage)
                ? "WinGet konnte nicht gestartet werden."
                : result.ErrorMessage;
        }

        if (!string.IsNullOrWhiteSpace(
                result.StandardError))
        {
            return "WinGet meldete einen Fehler: "
                   + result.StandardError.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
                result.ErrorMessage))
        {
            return result.ErrorMessage;
        }

        return result.ExitCode.HasValue
            ? $"WinGet wurde mit Exitcode "
              + $"{result.ExitCode.Value} beendet."
            : "WinGet wurde ohne auswertbaren "
              + "Exitcode beendet.";
    }
}