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
            nameof(AvailabilityText));

        OnPropertyChanged(
            nameof(ProgressText));

        OnPropertyChanged(
            nameof(ActionSummaryText));
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
            nameof(HasRestartRequirement));

        OnPropertyChanged(
            nameof(ActionSummaryText));
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}