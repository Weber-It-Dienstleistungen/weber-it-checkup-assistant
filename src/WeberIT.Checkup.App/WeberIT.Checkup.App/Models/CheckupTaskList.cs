using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTaskList
{
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
}