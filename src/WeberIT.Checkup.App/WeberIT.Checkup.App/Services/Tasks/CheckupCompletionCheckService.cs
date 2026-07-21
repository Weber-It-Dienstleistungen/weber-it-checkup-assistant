using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class CheckupCompletionCheckService
{
    private const string CompletionCheckActionCode =
        "control.checkup.completion-check";

    private const string CompletionCheckActionTitle =
        "Abschlusskontrolle";

    private readonly ICheckupScanner
        _checkupScanner;

    private readonly ICheckupAssessmentService
        _checkupAssessmentService;

    private readonly IDeviceIdentityService
        _deviceIdentityService;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    public CheckupCompletionCheckService(
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        IDeviceIdentityService deviceIdentityService,
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator)
    {
        ArgumentNullException.ThrowIfNull(
            checkupScanner);

        ArgumentNullException.ThrowIfNull(
            checkupAssessmentService);

        ArgumentNullException.ThrowIfNull(
            deviceIdentityService);

        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        _checkupScanner =
            checkupScanner;

        _checkupAssessmentService =
            checkupAssessmentService;

        _deviceIdentityService =
            deviceIdentityService;

        _executionCoordinator =
            executionCoordinator;
    }

    public CheckupCompletionCheckResult Run(
        CheckupSession sourceCheckup)
    {
        ArgumentNullException.ThrowIfNull(
            sourceCheckup);

        var tasksAwaitingVerification =
            sourceCheckup.TaskList.Tasks
                .Where(
                    task =>
                        task
                            .HasSuccessfulActionAwaitingVerification)
                .ToList();

        if (tasksAwaitingVerification.Count == 0)
        {
            throw new InvalidOperationException(
                "Für diesen Checkup steht keine "
                + "Abschlusskontrolle einer erfolgreich "
                + "ausgeführten Aktion aus.");
        }

        ValidateSourceTasks(
            tasksAwaitingVerification);

        var startedAt =
            DateTimeOffset.Now;

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                CompletionCheckActionCode,
                CompletionCheckActionTitle);

        if (executionLease is null)
        {
            throw new InvalidOperationException(
                BuildExecutionBlockedMessage());
        }

        var verificationCheckup =
            _checkupScanner.Scan();

        EnsureSameDevice(
            sourceCheckup,
            verificationCheckup);

        verificationCheckup.Assessment =
            _checkupAssessmentService.Assess(
                verificationCheckup);

        var currentTaskCodes =
            verificationCheckup.TaskList.Tasks
                .Select(
                    task =>
                        task.TaskCode)
                .Where(
                    taskCode =>
                        !string.IsNullOrWhiteSpace(
                            taskCode))
                .ToHashSet(
                    StringComparer.Ordinal);

        var taskResults =
            tasksAwaitingVerification
                .Select(
                    task =>
                        new CheckupTaskCompletionCheckResult
                        {
                            TaskId =
                                task.Id,

                            TaskCode =
                                task.TaskCode,

                            TaskTitle =
                                task.Title,

                            FindingStillPresent =
                                currentTaskCodes.Contains(
                                    task.TaskCode)
                        })
                .ToList();

        var finishedAt =
            DateTimeOffset.Now;

        return new CheckupCompletionCheckResult
        {
            StartedAt =
                startedAt,

            FinishedAt =
                finishedAt,

            VerificationScanDate =
                GetVerificationScanDate(
                    verificationCheckup,
                    finishedAt),

            CurrentFindingCount =
                verificationCheckup.Assessment
                    .Findings.Count,

            CurrentTaskCount =
                verificationCheckup.TaskList
                    .TotalTaskCount,

            TaskResults =
                taskResults
        };
    }

    private static void ValidateSourceTasks(
        IReadOnlyCollection<CheckupTask> tasks)
    {
        if (tasks.Any(
                task =>
                    task.Id == Guid.Empty))
        {
            throw new InvalidOperationException(
                "Mindestens eine zu prüfende Aufgabe "
                + "besitzt keine eindeutige Kennung.");
        }

        if (tasks.Any(
                task =>
                    string.IsNullOrWhiteSpace(
                        task.TaskCode)))
        {
            throw new InvalidOperationException(
                "Mindestens eine zu prüfende Aufgabe "
                + "besitzt keinen stabilen Aufgabencode.");
        }

        var hasDuplicateTaskIds =
            tasks
                .GroupBy(
                    task =>
                        task.Id)
                .Any(
                    group =>
                        group.Count() > 1);

        if (hasDuplicateTaskIds)
        {
            throw new InvalidOperationException(
                "Die Abschlusskontrolle enthält "
                + "mehrfach verwendete Aufgabenkennungen.");
        }
    }

    private void EnsureSameDevice(
        CheckupSession sourceCheckup,
        CheckupSession verificationCheckup)
    {
        var referenceDevice =
            new CustomerDevice
            {
                DisplayName =
                    string.IsNullOrWhiteSpace(
                        sourceCheckup
                            .DeviceInformation
                            .Name)
                        ? "Referenzgerät"
                        : sourceCheckup
                            .DeviceInformation
                            .Name,

                CheckupSession =
                    sourceCheckup
            };

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                new[]
                {
                    referenceDevice
                },
                verificationCheckup.DeviceInformation);

        if (matchingDevice is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Der Kontrollscan konnte dem ursprünglichen "
            + "Gerät nicht eindeutig zugeordnet werden. "
            + "Es wurde kein Aufgabenstatus verändert.");
    }

    private string BuildExecutionBlockedMessage()
    {
        if (string.IsNullOrWhiteSpace(
                _executionCoordinator
                    .ActiveActionTitle))
        {
            return
                "Die Abschlusskontrolle kann nicht "
                + "gestartet werden, weil bereits eine "
                + "andere Systemaktion ausgeführt wird.";
        }

        return
            "Die Abschlusskontrolle kann nicht "
            + "gestartet werden. Aktuell läuft bereits: "
            + _executionCoordinator.ActiveActionTitle
            + ".";
    }

    private static DateTimeOffset
        GetVerificationScanDate(
            CheckupSession verificationCheckup,
            DateTimeOffset fallback)
    {
        if (!verificationCheckup.ScanDate.HasValue)
        {
            return fallback;
        }

        return new DateTimeOffset(
            verificationCheckup.ScanDate.Value);
    }
}