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

    /*
     * Die Arbeitsansicht ist bewusst keine zweite
     * Aufgabenwahrheit.
     *
     * Sie ist ausschließlich eine Projektion der bereits
     * gespeicherten Aufgaben-, Status- und Aktionsergebnisse.
     *
     * Offen + noch nicht erfolgreich bearbeitet:
     *   in der Arbeitsliste sichtbar.
     *
     * Erfolgreiche technische Aktion seit dem letzten
     * Kontrollscan:
     *   vorläufig aus der Arbeitsliste ausgeblendet.
     *
     * Kontrollscan erkennt den Befund erneut:
     *   Aufgabe ist weiterhin Open und wird wieder sichtbar.
     *
     * Completed:
     *   aus der Arbeitsliste ausgeblendet.
     *
     * Skipped / NotFeasible:
     *   aus der Arbeitsliste ausgeblendet, aber separat
     *   als dokumentiertes Ergebnis angezeigt.
     */

    [JsonIgnore]
    public IReadOnlyList<CheckupTask> ActiveTasks =>
        Tasks
            .Where(
                IsTaskVisibleInWorkList)
            .ToList();

    [JsonIgnore]
    public bool HasActiveTasks =>
        ActiveTaskCount > 0;

    [JsonIgnore]
    public int ActiveTaskCount =>
        Tasks.Count(
            IsTaskVisibleInWorkList);

    [JsonIgnore]
    public int RequiredActiveTaskCount =>
        Tasks.Count(
            task =>
                task.Priority
                    == CheckupTaskPriority.Required
                && IsTaskVisibleInWorkList(
                    task));

    [JsonIgnore]
    public IReadOnlyList<CheckupTask>
        TasksAwaitingVerification =>
            Tasks
                .Where(
                    IsTaskAwaitingVerificationCore)
                .ToList();

    [JsonIgnore]
    public int AwaitingVerificationTaskCount =>
        Tasks.Count(
            IsTaskAwaitingVerificationCore);

    [JsonIgnore]
    public bool HasTasksAwaitingVerification =>
        AwaitingVerificationTaskCount > 0;

    [JsonIgnore]
    public IReadOnlyList<CheckupTask>
        DocumentedExceptionTasks =>
            Tasks
                .Where(
                    task =>
                        task.Status
                            is CheckupTaskStatus.Skipped
                            or CheckupTaskStatus.NotFeasible)
                .ToList();

    [JsonIgnore]
    public bool HasDocumentedExceptionTasks =>
        DocumentedExceptionTaskCount > 0;

    [JsonIgnore]
    public int DocumentedExceptionTaskCount =>
        Tasks.Count(
            task =>
                task.Status
                    is CheckupTaskStatus.Skipped
                    or CheckupTaskStatus.NotFeasible);

    [JsonIgnore]
    public int ProcessedTaskCount =>
        Math.Max(
            0,
            TotalTaskCount
            - ActiveTaskCount);

    [JsonIgnore]
    public string WorkListSummaryText
    {
        get
        {
            if (!HasTasks)
            {
                return
                    "Keine Aufgaben vorhanden.";
            }

            if (ActiveTaskCount == 0
                && AwaitingVerificationTaskCount > 0)
            {
                return AwaitingVerificationTaskCount == 1
                    ? "Alle aktuell bearbeitbaren Aufgaben wurden "
                      + "bearbeitet. Eine Aufgabe wartet auf einen "
                      + "erneuten Kontrollscan."
                    : "Alle aktuell bearbeitbaren Aufgaben wurden "
                      + "bearbeitet. "
                      + $"{AwaitingVerificationTaskCount} Aufgaben "
                      + "warten auf einen erneuten Kontrollscan.";
            }

            if (ActiveTaskCount == 0)
            {
                return
                    "Aktuell ist keine weitere Aufgabe zu bearbeiten.";
            }

            return ActiveTaskCount == 1
                ? "Noch eine Aufgabe ist aktiv zu bearbeiten."
                : $"Noch {ActiveTaskCount} Aufgaben sind "
                  + "aktiv zu bearbeiten.";
        }
    }

    [JsonIgnore]
    public string ProcessedTaskSummaryText
    {
        get
        {
            var parts =
                new List<string>();

            if (AwaitingVerificationTaskCount > 0)
            {
                parts.Add(
                    AwaitingVerificationTaskCount == 1
                        ? "1 wartet auf Kontrolle"
                        : $"{AwaitingVerificationTaskCount} "
                          + "warten auf Kontrolle");
            }

            if (CompletedTaskCount > 0)
            {
                parts.Add(
                    CompletedTaskCount == 1
                        ? "1 erledigt"
                        : $"{CompletedTaskCount} erledigt");
            }

            if (NotFeasibleTaskCount > 0)
            {
                parts.Add(
                    NotFeasibleTaskCount == 1
                        ? "1 nicht durchführbar"
                        : $"{NotFeasibleTaskCount} "
                          + "nicht durchführbar");
            }

            if (SkippedTaskCount > 0)
            {
                parts.Add(
                    SkippedTaskCount == 1
                        ? "1 übersprungen"
                        : $"{SkippedTaskCount} übersprungen");
            }

            return parts.Count == 0
                ? "Noch keine Aufgabe bearbeitet."
                : string.Join(
                    " · ",
                    parts);
        }
    }

    [JsonIgnore]
    public int ActionResultCount =>
        Tasks.Sum(
            task =>
                task.ActionResultCount);

    [JsonIgnore]
    public bool HasActionResults =>
        ActionResultCount > 0;

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

            return WorkListSummaryText;
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
                $"{ProcessedTaskCount} von "
                + $"{TotalTaskCount} Aufgaben "
                + "bearbeitet oder dokumentiert";
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
            if (HasTasksAwaitingVerification)
            {
                return AwaitingVerificationTaskCount == 1
                    ? "Eine erfolgreich ausgeführte technische "
                      + "Aktion wartet auf einen neuen lesenden "
                      + "Kontrollscan."
                    : $"{AwaitingVerificationTaskCount} erfolgreich "
                      + "bearbeitete Aufgaben warten auf einen "
                      + "neuen lesenden Kontrollscan.";
            }

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

    public bool IsTaskAwaitingVerification(
        CheckupTask task)
    {
        EnsureTaskBelongsToList(
            task);

        return IsTaskAwaitingVerificationCore(
            task);
    }

    public bool EnsureTask(
        CheckupTask task,
        int minimumTaskListVersion)
    {
        ValidateTaskForAddition(
            task);

        if (minimumTaskListVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumTaskListVersion),
                "Die Mindestversion der Aufgabenliste "
                + "muss größer als null sein.");
        }

        var existingTask =
            Tasks.FirstOrDefault(
                currentTask =>
                    string.Equals(
                        currentTask.TaskCode,
                        task.TaskCode,
                        StringComparison.Ordinal));

        if (existingTask is not null)
        {
            if (TaskListVersion
                >= minimumTaskListVersion)
            {
                return false;
            }

            var previousVersion =
                TaskListVersion;

            TaskListVersion =
                minimumTaskListVersion;

            NotifyTaskCollectionChanged();

            try
            {
                RequestPersistence();
            }
            catch
            {
                TaskListVersion =
                    previousVersion;

                NotifyTaskCollectionChanged();

                throw;
            }

            return false;
        }

        if (Tasks.Any(
                currentTask =>
                    currentTask.Id
                    == task.Id))
        {
            throw new InvalidOperationException(
                "Die neue Aufgabe verwendet eine bereits "
                + "vorhandene Aufgabenkennung.");
        }

        var previousTaskListVersion =
            TaskListVersion;

        Tasks.Add(
            task);

        TaskListVersion =
            Math.Max(
                TaskListVersion,
                minimumTaskListVersion);

        NotifyTaskCollectionChanged();
        NotifySummaryChanged();

        try
        {
            RequestPersistence();
        }
        catch
        {
            Tasks.Remove(
                task);

            TaskListVersion =
                previousTaskListVersion;

            NotifyTaskCollectionChanged();
            NotifySummaryChanged();

            throw;
        }

        return true;
    }

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

        EnsureActionResultIsUnique(
            actionResult);

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

    public void ApplyTaskActionOutcome(
        CheckupTask task,
        CheckupTaskActionResult actionResult,
        CheckupTaskStatus resultingStatus,
        string statusReason,
        CheckupTask? followUpTask = null)
    {
        EnsureTaskBelongsToList(
            task);

        ValidateActionResult(
            actionResult);

        EnsureActionResultIsUnique(
            actionResult);

        if (string.IsNullOrWhiteSpace(
                statusReason))
        {
            throw new ArgumentException(
                "Für den abschließenden Aufgabenstatus "
                + "ist eine Begründung erforderlich.",
                nameof(statusReason));
        }

        if (followUpTask is not null)
        {
            ValidateTaskForAddition(
                followUpTask);

            if (Tasks.Any(
                    existingTask =>
                        existingTask.Id
                        == followUpTask.Id
                        && !string.Equals(
                            existingTask.TaskCode,
                            followUpTask.TaskCode,
                            StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Die Folgeaufgabe verwendet eine bereits "
                    + "vorhandene Aufgabenkennung.");
            }
        }

        var previousStatus =
            task.Status;

        var previousStatusChangedAt =
            task.StatusChangedAt;

        var previousStatusReason =
            task.StatusReason;

        var previousTechnicianNote =
            task.TechnicianNote;

        var followUpTaskWasAdded =
            false;

        task.AddActionResult(
            actionResult);

        task.ApplyStatus(
            resultingStatus,
            statusReason,
            task.TechnicianNote);

        if (followUpTask is not null
            && !Tasks.Any(
                existingTask =>
                    string.Equals(
                        existingTask.TaskCode,
                        followUpTask.TaskCode,
                        StringComparison.Ordinal)))
        {
            Tasks.Add(
                followUpTask);

            followUpTaskWasAdded =
                true;
        }

        NotifyActionSummaryChanged();
        NotifySummaryChanged();

        if (followUpTaskWasAdded)
        {
            NotifyTaskCollectionChanged();
        }

        try
        {
            RequestPersistence();
        }
        catch
        {
            task.RemoveActionResult(
                actionResult.Id);

            task.RestoreStatus(
                previousStatus,
                previousStatusChangedAt,
                previousStatusReason,
                previousTechnicianNote);

            if (followUpTaskWasAdded
                && followUpTask is not null)
            {
                Tasks.Remove(
                    followUpTask);
            }

            NotifyActionSummaryChanged();
            NotifySummaryChanged();

            if (followUpTaskWasAdded)
            {
                NotifyTaskCollectionChanged();
            }

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

        foreach (var mapping
                 in taskMappings)
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

    private bool IsTaskVisibleInWorkList(
        CheckupTask task)
    {
        return task.Status
                   == CheckupTaskStatus.Open
               && !IsTaskAwaitingVerificationCore(
                   task);
    }

    private bool IsTaskAwaitingVerificationCore(
        CheckupTask task)
    {
        if (task.Status
            != CheckupTaskStatus.Open)
        {
            return false;
        }

        var successfulActions =
            task.ActionResults
                .Where(
                    result =>
                        result.Status
                        == CheckupTaskActionStatus.Successful)
                .ToList();

        if (successfulActions.Count == 0)
        {
            return false;
        }

        /*
         * Ein erfolgreiches Ergebnis ohne belastbaren
         * Abschlusszeitpunkt wird vorsorglich als noch nicht
         * verifiziert behandelt.
         */
        if (successfulActions.Any(
                action =>
                    !action.FinishedAt.HasValue
                    && !action.StartedAt.HasValue))
        {
            return true;
        }

        if (!LastCompletionCheckAt.HasValue)
        {
            return true;
        }

        var lastCompletionCheckAt =
            LastCompletionCheckAt.Value;

        return successfulActions.Any(
            action =>
            {
                var actionAt =
                    action.FinishedAt
                    ?? action.StartedAt;

                return !actionAt.HasValue
                       || actionAt.Value
                       > lastCompletionCheckAt;
            });
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

    private void EnsureActionResultIsUnique(
        CheckupTaskActionResult actionResult)
    {
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

        if (!IsTaskAwaitingVerificationCore(
                task))
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
            TasksAwaitingVerification
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

    private static void ValidateTaskForAddition(
        CheckupTask task)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        if (task.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Die Aufgabe benötigt eine eindeutige Kennung.",
                nameof(task));
        }

        if (string.IsNullOrWhiteSpace(
                task.TaskCode))
        {
            throw new ArgumentException(
                "Die Aufgabe benötigt einen stabilen Aufgabencode.",
                nameof(task));
        }

        if (string.IsNullOrWhiteSpace(
                task.Title))
        {
            throw new ArgumentException(
                "Die Aufgabe benötigt eine verständliche Bezeichnung.",
                nameof(task));
        }

        if (string.IsNullOrWhiteSpace(
                task.Description))
        {
            throw new ArgumentException(
                "Die Aufgabe benötigt eine technische Beschreibung.",
                nameof(task));
        }

        if (!Enum.IsDefined(
                typeof(CheckupTaskPriority),
                task.Priority))
        {
            throw new ArgumentException(
                "Die Aufgabenpriorität ist ungültig.",
                nameof(task));
        }

        if (!Enum.IsDefined(
                typeof(CheckupTaskCategory),
                task.Category))
        {
            throw new ArgumentException(
                "Die Aufgabenkategorie ist ungültig.",
                nameof(task));
        }

        if (!Enum.IsDefined(
                typeof(CheckupTaskStatus),
                task.Status))
        {
            throw new ArgumentException(
                "Der Aufgabenstatus ist ungültig.",
                nameof(task));
        }

        if (task.SourceFindingCodes is null)
        {
            throw new ArgumentException(
                "Die Liste der zugrunde liegenden Befunde "
                + "darf nicht fehlen.",
                nameof(task));
        }

        if (task.ActionResults is null)
        {
            throw new ArgumentException(
                "Die technische Aktionshistorie "
                + "darf nicht fehlen.",
                nameof(task));
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

    private void NotifyTaskCollectionChanged()
    {
        OnPropertyChanged(
            nameof(Tasks));

        OnPropertyChanged(
            nameof(TaskListVersion));

        OnPropertyChanged(
            nameof(VersionText));

        OnPropertyChanged(
            nameof(IsAvailable));

        OnPropertyChanged(
            nameof(HasTasks));

        OnPropertyChanged(
            nameof(TotalTaskCount));

        OnPropertyChanged(
            nameof(AvailabilityText));

        OnPropertyChanged(
            nameof(ProgressText));

        NotifyWorkListChanged();
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

        NotifyWorkListChanged();
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

        NotifyWorkListChanged();
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
            nameof(AwaitingVerificationTaskCount));

        OnPropertyChanged(
            nameof(HasTasksAwaitingVerification));

        OnPropertyChanged(
            nameof(ShouldShowCompletionCheckPanel));

        OnPropertyChanged(
            nameof(CompletionCheckStatusText));

        OnPropertyChanged(
            nameof(CompletionCheckButtonText));

        OnPropertyChanged(
            nameof(ActionSummaryText));

        NotifyWorkListChanged();
    }

    private void NotifyWorkListChanged()
    {
        OnPropertyChanged(
            nameof(ActiveTasks));

        OnPropertyChanged(
            nameof(HasActiveTasks));

        OnPropertyChanged(
            nameof(ActiveTaskCount));

        OnPropertyChanged(
            nameof(RequiredActiveTaskCount));

        OnPropertyChanged(
            nameof(TasksAwaitingVerification));

        OnPropertyChanged(
            nameof(AwaitingVerificationTaskCount));

        OnPropertyChanged(
            nameof(HasTasksAwaitingVerification));

        OnPropertyChanged(
            nameof(DocumentedExceptionTasks));

        OnPropertyChanged(
            nameof(HasDocumentedExceptionTasks));

        OnPropertyChanged(
            nameof(DocumentedExceptionTaskCount));

        OnPropertyChanged(
            nameof(ProcessedTaskCount));

        OnPropertyChanged(
            nameof(WorkListSummaryText));

        OnPropertyChanged(
            nameof(ProcessedTaskSummaryText));
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