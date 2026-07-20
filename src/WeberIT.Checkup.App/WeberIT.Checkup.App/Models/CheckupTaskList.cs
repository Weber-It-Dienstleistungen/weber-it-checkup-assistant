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
        if (!Tasks.Any(
                existingTask =>
                    existingTask.Id == task.Id))
        {
            throw new InvalidOperationException(
                "Die ausgewählte Aufgabe gehört nicht "
                + "zu dieser Aufgabenliste.");
        }

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
            PersistenceRequested?.Invoke(
                this,
                EventArgs.Empty);
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
            nameof(AvailabilityText));

        OnPropertyChanged(
            nameof(ProgressText));
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