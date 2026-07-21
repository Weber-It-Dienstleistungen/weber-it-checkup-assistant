using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTaskList : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? PersistenceRequested;

    public int TaskListVersion { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTimeOffset? LastCompletionCheckAt
    {
        get;
        set;
    }

    public string LastCompletionCheckSummary
    {
        get;
        set;
    } = string.Empty;

    public CheckupCompletionCheckResult? LastCompletionCheckResult
    {
        get;
        set;
    }

    public List<CheckupTask> Tasks { get; set; } =
        new();

    [JsonIgnore]
    public bool IsAvailable =>
        TaskListVersion > 0;

    [JsonIgnore]
    public bool HasTasks =>
        IsAvailable
        && Tasks.Count > 0;

    [JsonIgnore]
    public int TotalTaskCount =>
        Tasks.Count;

    [JsonIgnore]
    public int OpenTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                == CheckupTaskStatus.Open);

    [JsonIgnore]
    public int CompletedTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                == CheckupTaskStatus.Completed);

    [JsonIgnore]
    public int SkippedTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                == CheckupTaskStatus.Skipped);

    [JsonIgnore]
    public int NotFeasibleTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                == CheckupTaskStatus.NotFeasible);

    [JsonIgnore]
    public int DocumentedTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                != CheckupTaskStatus.Open);

    [JsonIgnore]
    public int RequiredOpenTaskCount =>
        Tasks.Count(
            task =>
                task.Priority
                    == CheckupTaskPriority.Required
                && task.Status
                    == CheckupTaskStatus.Open);

    [JsonIgnore]
    public int ActionResultCount =>
        Tasks.Sum(
            task =>
                task.ActionResultCount);

    [JsonIgnore]
    public bool HasActionResults =>
        ActionResultCount > 0;

    [JsonIgnore]
    public int AwaitingVerificationTaskCount =>
        Tasks.Count(
            task =>
                task.HasSuccessfulActionAwaitingVerification);

    [JsonIgnore]
    public bool HasTasksAwaitingVerification =>
        AwaitingVerificationTaskCount > 0;

    [JsonIgnore]
    public bool HasCompletionCheck =>
        LastCompletionCheckAt.HasValue
        && !string.IsNullOrWhiteSpace(
            LastCompletionCheckSummary);

    [JsonIgnore]
    public bool ShouldShowCompletionCheckPanel =>
        HasTasksAwaitingVerification
        || HasCompletionCheck;

    [JsonIgnore]
    public bool HasRestartRequirement =>
        Tasks.Any(
            task =>
                task.HasRestartRequirement);

    [JsonIgnore]
    public string AvailabilityText
    {
        get
        {
            if (!IsAvailable)
            {
                return
                    "In diesem historischen Checkup ist "
                    + "keine Aufgabenliste enthalten.";
            }

            if (!HasTasks)
            {
                return
                    "Aus den Befunden dieses Checkups wurden "
                    + "keine Aufgaben abgeleitet.";
            }

            return OpenTaskCount == 1
                ? "Eine Aufgabe ist noch offen."
                : $"{OpenTaskCount} Aufgaben sind noch offen.";
        }
    }

    [JsonIgnore]
    public string ProgressText
    {
        get
        {
            if (!IsAvailable)
            {
                return
                    "Aufgabenfortschritt nicht verfügbar";
            }

            if (!HasTasks)
            {
                return
                    "Keine Aufgaben erforderlich";
            }

            return
                $"{DocumentedTaskCount} von "
                + $"{TotalTaskCount} Aufgaben "
                + "abschließend dokumentiert";
        }
    }

    [JsonIgnore]
    public string ActionSummaryText
    {
        get
        {
            if (!IsAvailable)
            {
                return
                    "Aktionshistorie nicht verfügbar";
            }

            if (!HasActionResults)
            {
                return
                    "Noch keine technische Aktion dokumentiert";
            }

            if (AwaitingVerificationTaskCount == 1)
            {
                return
                    "Für eine Aufgabe steht die "
                    + "Abschlusskontrolle aus";
            }

            if (AwaitingVerificationTaskCount > 1)
            {
                return
                    $"Für {AwaitingVerificationTaskCount} Aufgaben "
                    + "steht die Abschlusskontrolle aus";
            }

            return ActionResultCount == 1
                ? "Eine technische Aktion dokumentiert"
                : $"{ActionResultCount} technische Aktionen "
                  + "dokumentiert";
        }
    }

    [JsonIgnore]
    public string CompletionCheckStatusText
    {
        get
        {
            if (HasCompletionCheck)
            {
                return
                    LastCompletionCheckSummary
                    + Environment.NewLine
                    + "Kontrolliert am "
                    + LastCompletionCheckAt!
                        .Value
                        .ToLocalTime()
                        .ToString(
                            "dd.MM.yyyy HH:mm")
                    + " Uhr.";
            }

            if (HasTasksAwaitingVerification)
            {
                return
                    "Mindestens eine technische Aktion wurde "
                    + "erfolgreich ausgeführt. Ein neuer "
                    + "lesender Kontrollscan prüft, ob der "
                    + "zugrunde liegende Befund weiterhin besteht.";
            }

            return
                "Aktuell steht keine Abschlusskontrolle aus.";
        }
    }

    [JsonIgnore]
    public string CompletionCheckButtonText =>
        HasCompletionCheck
            ? "Abschlusskontrolle erneut starten"
            : "Abschlusskontrolle starten";

    [JsonIgnore]
    public string VersionText =>
        TaskListVersion > 0
            ? $"Aufgabenmodell Version {TaskListVersion}"
            : "Historischer Checkup ohne Aufgabenliste";

    public void ChangeTaskStatus(
        CheckupTask task,
        CheckupTaskStatus status,
        string statusReason,
        string technicianNote)
    {
        EnsureTaskBelongsToList(
            task);

        var previousStatus =
            task.Status;

        var previousStatusChangedAt =
            task.StatusChangedAt;

        var previousStatusReason =
            task.StatusReason;

        var previousTechnicianNote =
            task.TechnicianNote;

        task.ApplyStatus(
            status,
            statusReason,
            technicianNote);

        NotifySummaryChanged();

        try
        {
            RequestPersistence();
        }
        catch
        {
            task.RestoreStatus(
                previousStatus,
                previousStatusChangedAt,
                previousStatusReason,
                previousTechnicianNote);

            NotifySummaryChanged();

            throw;
        }
    }

    public void AddTaskActionResult(
        CheckupTask task,
        CheckupTaskActionResult actionResult)
    {
        EnsureTaskBelongsToList(
            task);

        ValidateActionResult(
            actionResult);

        if (Tasks
            .SelectMany(
                existingTask =>
                    existingTask.ActionResults)
            .Any(
                existingResult =>
                    existingResult.Id
                    == actionResult.Id))
        {
            throw new InvalidOperationException(
                "Das Aktionsergebnis ist bereits in "
                + "dieser Aufgabenliste enthalten.");
        }

        task.AddActionResult(
            actionResult);

        NotifyActionSummaryChanged();

        try
        {
            RequestPersistence();
        }
        catch
        {
            task.RemoveActionResult(
                actionResult.Id);

            NotifyActionSummaryChanged();

            throw;
        }
    }

    public void ApplyCompletionCheck(
        CheckupCompletionCheckResult completionCheck)
    {
        ArgumentNullException.ThrowIfNull(
            completionCheck);

        ValidateCompletionCheck(
            completionCheck);

        var taskMappings =
            completionCheck.TaskResults
                .Select(
                    result =>
                        new CompletionCheckTaskMapping(
                            GetCompletionCheckTask(
                                result),
                            result))
                .ToList();

        var taskSnapshots =
            taskMappings
                .Select(
                    mapping =>
                        new TaskStatusSnapshot(
                            mapping.Task,
                            mapping.Task.Status,
                            mapping.Task.StatusChangedAt,
                            mapping.Task.StatusReason,
                            mapping.Task.TechnicianNote))
                .ToList();

        var previousCompletionCheckAt =
            LastCompletionCheckAt;

        var previousCompletionCheckSummary =
            LastCompletionCheckSummary;

        var previousCompletionCheckResult =
            LastCompletionCheckResult;

        foreach (var mapping in taskMappings)
        {
            var status =
                mapping.Result.FindingStillPresent
                    ? CheckupTaskStatus.Open
                    : CheckupTaskStatus.Completed;

            mapping.Task.ApplyStatus(
                status,
                BuildCompletionCheckReason(
                    mapping.Result,
                    completionCheck
                        .VerificationScanDate),
                mapping.Task.TechnicianNote);
        }

        LastCompletionCheckAt =
            completionCheck.VerificationScanDate;

        LastCompletionCheckSummary =
            BuildCompletionCheckSummary(
                completionCheck);

        LastCompletionCheckResult =
            completionCheck;

        NotifySummaryChanged();
        NotifyCompletionCheckChanged();

        try
        {
            RequestPersistence();
        }
        catch
        {
            foreach (var snapshot
                     in taskSnapshots)
            {
                snapshot.Task.RestoreStatus(
                    snapshot.Status,
                    snapshot.StatusChangedAt,
                    snapshot.StatusReason,
                    snapshot.TechnicianNote);
            }

            LastCompletionCheckAt =
                previousCompletionCheckAt;

            LastCompletionCheckSummary =
                previousCompletionCheckSummary;

            LastCompletionCheckResult =
                previousCompletionCheckResult;

            NotifySummaryChanged();
            NotifyCompletionCheckChanged();

            throw;
        }
    }

    private void EnsureTaskBelongsToList(
        CheckupTask task)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        if (!Tasks.Any(
                existingTask =>
                    existingTask.Id
                    == task.Id))
        {
            throw new InvalidOperationException(
                "Die ausgewählte Aufgabe gehört nicht "
                + "zu dieser Aufgabenliste.");
        }
    }

    private CheckupTask GetCompletionCheckTask(
        CheckupTaskCompletionCheckResult result)
    {
        var task =
            Tasks.SingleOrDefault(
                existingTask =>
                    existingTask.Id
                    == result.TaskId);

        if (task is null)
        {
            throw new InvalidOperationException(
                "Eine geprüfte Aufgabe gehört nicht mehr "
                + "zur aktuellen Aufgabenliste.");
        }

        if (!string.Equals(
                task.TaskCode,
                result.TaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aufgabencode des Kontrollergebnisses "
                + "stimmt nicht mit der gespeicherten "
                + "Aufgabe überein.");
        }

        if (!task.HasSuccessfulActionAwaitingVerification)
        {
            throw new InvalidOperationException(
                "Der Status einer zu prüfenden Aufgabe "
                + "wurde während des Kontrollscans verändert.");
        }

        return task;
    }

    private void ValidateCompletionCheck(
        CheckupCompletionCheckResult completionCheck)
    {
        if (completionCheck.TaskResults.Count == 0)
        {
            throw new ArgumentException(
                "Die Abschlusskontrolle enthält kein "
                + "Aufgabenergebnis.",
                nameof(completionCheck));
        }

        if (completionCheck.FinishedAt
            < completionCheck.StartedAt)
        {
            throw new ArgumentException(
                "Der Abschlusszeitpunkt der Kontrolle "
                + "darf nicht vor dem Startzeitpunkt liegen.",
                nameof(completionCheck));
        }

        var duplicateTaskIds =
            completionCheck.TaskResults
                .GroupBy(
                    result =>
                        result.TaskId)
                .Any(
                    group =>
                        group.Count() > 1);

        if (duplicateTaskIds)
        {
            throw new ArgumentException(
                "Die Abschlusskontrolle enthält eine "
                + "Aufgabe mehrfach.",
                nameof(completionCheck));
        }

        var expectedTaskIds =
            Tasks
                .Where(
                    task =>
                        task
                            .HasSuccessfulActionAwaitingVerification)
                .Select(
                    task =>
                        task.Id)
                .ToHashSet();

        var receivedTaskIds =
            completionCheck.TaskResults
                .Select(
                    result =>
                        result.TaskId)
                .ToHashSet();

        if (!expectedTaskIds.SetEquals(
                receivedTaskIds))
        {
            throw new InvalidOperationException(
                "Die Aufgabenlage hat sich während des "
                + "Kontrollscans verändert. Es wurde kein "
                + "Status übernommen.");
        }
    }

    private static void ValidateActionResult(
        CheckupTaskActionResult actionResult)
    {
        ArgumentNullException.ThrowIfNull(
            actionResult);

        if (actionResult.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Das Aktionsergebnis benötigt eine "
                + "eindeutige Kennung.",
                nameof(actionResult));
        }

        if (string.IsNullOrWhiteSpace(
                actionResult.ActionCode))
        {
            throw new ArgumentException(
                "Das Aktionsergebnis benötigt einen "
                + "stabilen Aktionscode.",
                nameof(actionResult));
        }

        if (string.IsNullOrWhiteSpace(
                actionResult.ActionTitle))
        {
            throw new ArgumentException(
                "Das Aktionsergebnis benötigt eine "
                + "verständliche Bezeichnung.",
                nameof(actionResult));
        }

        if (string.IsNullOrWhiteSpace(
                actionResult.TargetDescription))
        {
            throw new ArgumentException(
                "Das Ziel der ausgeführten Aktion muss "
                + "dokumentiert werden.",
                nameof(actionResult));
        }

        if (actionResult.Status
            == CheckupTaskActionStatus.Unknown)
        {
            throw new ArgumentException(
                "Das technische Aktionsergebnis muss "
                + "eindeutig feststehen.",
                nameof(actionResult));
        }

        if (string.IsNullOrWhiteSpace(
                actionResult.Summary))
        {
            throw new ArgumentException(
                "Das Aktionsergebnis benötigt eine "
                + "Zusammenfassung.",
                nameof(actionResult));
        }

        if (!actionResult.StartedAt.HasValue)
        {
            throw new ArgumentException(
                "Der Startzeitpunkt der Aktion muss "
                + "dokumentiert werden.",
                nameof(actionResult));
        }

        if (!actionResult.FinishedAt.HasValue)
        {
            throw new ArgumentException(
                "Der Abschlusszeitpunkt der Aktion muss "
                + "dokumentiert werden.",
                nameof(actionResult));
        }

        if (actionResult.FinishedAt.Value
            < actionResult.StartedAt.Value)
        {
            throw new ArgumentException(
                "Der Abschlusszeitpunkt darf nicht vor "
                + "dem Startzeitpunkt liegen.",
                nameof(actionResult));
        }
    }

    private static string BuildCompletionCheckReason(
        CheckupTaskCompletionCheckResult taskResult,
        DateTimeOffset verificationScanDate)
    {
        var checkedAt =
            verificationScanDate
                .ToLocalTime()
                .ToString(
                    "dd.MM.yyyy HH:mm");

        if (taskResult.FindingStillPresent)
        {
            return
                "Automatische Abschlusskontrolle vom "
                + checkedAt
                + " Uhr: Der zugrunde liegende Befund "
                + "wurde im aktuellen Kontrollscan erneut "
                + "festgestellt. Die Aufgabe bleibt offen.";
        }

        return
            "Automatische Abschlusskontrolle vom "
            + checkedAt
            + " Uhr: Der zugrunde liegende Befund wurde "
            + "im aktuellen Kontrollscan nicht erneut "
            + "festgestellt. Die Aufgabe wurde abgeschlossen.";
    }

    private static string BuildCompletionCheckSummary(
        CheckupCompletionCheckResult completionCheck)
    {
        var resolvedText =
            completionCheck.ResolvedTaskCount switch
            {
                0 =>
                    "Keine geprüfte Aufgabe wurde abgeschlossen.",

                1 =>
                    "Eine geprüfte Aufgabe wurde abgeschlossen.",

                _ =>
                    $"{completionCheck.ResolvedTaskCount} "
                    + "geprüfte Aufgaben wurden abgeschlossen."
            };

        var remainingText =
            completionCheck.RemainingTaskCount switch
            {
                0 =>
                    "Keine geprüfte Aufgabe bleibt aufgrund "
                    + "des Kontrollscans offen.",

                1 =>
                    "Eine geprüfte Aufgabe bleibt aufgrund "
                    + "eines weiterhin vorhandenen Befunds offen.",

                _ =>
                    $"{completionCheck.RemainingTaskCount} "
                    + "geprüfte Aufgaben bleiben aufgrund "
                    + "weiterhin vorhandener Befunde offen."
            };

        var currentTaskText =
            completionCheck.CurrentTaskCount switch
            {
                0 =>
                    "Im Kontrollscan wurde aktuell keine "
                    + "Aufgabe abgeleitet.",

                1 =>
                    "Im Kontrollscan wurde insgesamt eine "
                    + "aktuelle Aufgabe abgeleitet.",

                _ =>
                    "Im Kontrollscan wurden insgesamt "
                    + $"{completionCheck.CurrentTaskCount} "
                    + "aktuelle Aufgaben abgeleitet."
            };

        return
            resolvedText
            + " "
            + remainingText
            + " "
            + currentTaskText;
    }

    private void RequestPersistence()
    {
        PersistenceRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(
            nameof(OpenTaskCount));

        OnPropertyChanged(
            nameof(CompletedTaskCount));

        OnPropertyChanged(
            nameof(SkippedTaskCount));

        OnPropertyChanged(
            nameof(NotFeasibleTaskCount));

        OnPropertyChanged(
            nameof(DocumentedTaskCount));

        OnPropertyChanged(
            nameof(RequiredOpenTaskCount));

        OnPropertyChanged(
            nameof(AwaitingVerificationTaskCount));

        OnPropertyChanged(
            nameof(HasTasksAwaitingVerification));

        OnPropertyChanged(
            nameof(ShouldShowCompletionCheckPanel));

        OnPropertyChanged(
            nameof(AvailabilityText));

        OnPropertyChanged(
            nameof(ProgressText));

        OnPropertyChanged(
            nameof(ActionSummaryText));

        OnPropertyChanged(
            nameof(CompletionCheckStatusText));

        OnPropertyChanged(
            nameof(CompletionCheckButtonText));
    }

    private void NotifyActionSummaryChanged()
    {
        OnPropertyChanged(
            nameof(ActionResultCount));

        OnPropertyChanged(
            nameof(HasActionResults));

        OnPropertyChanged(
            nameof(AwaitingVerificationTaskCount));

        OnPropertyChanged(
            nameof(HasTasksAwaitingVerification));

        OnPropertyChanged(
            nameof(ShouldShowCompletionCheckPanel));

        OnPropertyChanged(
            nameof(HasRestartRequirement));

        OnPropertyChanged(
            nameof(ActionSummaryText));

        OnPropertyChanged(
            nameof(CompletionCheckStatusText));

        OnPropertyChanged(
            nameof(CompletionCheckButtonText));
    }

    private void NotifyCompletionCheckChanged()
    {
        OnPropertyChanged(
            nameof(LastCompletionCheckAt));

        OnPropertyChanged(
            nameof(LastCompletionCheckSummary));

        OnPropertyChanged(
            nameof(LastCompletionCheckResult));

        OnPropertyChanged(
            nameof(HasCompletionCheck));

        OnPropertyChanged(
            nameof(ShouldShowCompletionCheckPanel));

        OnPropertyChanged(
            nameof(CompletionCheckStatusText));

        OnPropertyChanged(
            nameof(CompletionCheckButtonText));
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    private sealed record CompletionCheckTaskMapping(
        CheckupTask Task,
        CheckupTaskCompletionCheckResult Result);

    private sealed record TaskStatusSnapshot(
        CheckupTask Task,
        CheckupTaskStatus Status,
        DateTime? StatusChangedAt,
        string StatusReason,
        string TechnicianNote);
}