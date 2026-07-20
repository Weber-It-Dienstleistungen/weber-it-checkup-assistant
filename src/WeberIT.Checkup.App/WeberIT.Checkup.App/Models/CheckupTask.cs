using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTask : INotifyPropertyChanged
{
    private CheckupTaskStatus _status =
        CheckupTaskStatus.Open;

    private DateTime? _statusChangedAt;

    private string _statusReason =
        string.Empty;

    private string _technicianNote =
        string.Empty;

    private List<CheckupTaskActionResult> _actionResults =
        new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } =
        Guid.NewGuid();

    public string TaskCode { get; set; } =
        string.Empty;

    public List<string> SourceFindingCodes { get; set; } =
        new();

    public string SourceCauseGroup { get; set; } =
        string.Empty;

    public string Title { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public CheckupTaskPriority Priority { get; set; } =
        CheckupTaskPriority.Optional;

    public CheckupTaskCategory Category { get; set; } =
        CheckupTaskCategory.General;

    public CheckupTaskStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(IsDocumented));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(
                nameof(HasSuccessfulActionAwaitingVerification));
        }
    }

    public DateTime CreatedAt { get; set; } =
        DateTime.Now;

    public DateTime? StatusChangedAt
    {
        get => _statusChangedAt;
        set
        {
            if (_statusChangedAt == value)
            {
                return;
            }

            _statusChangedAt = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(StatusChangedAtText));
        }
    }

    public string StatusReason
    {
        get => _statusReason;
        set
        {
            var normalizedValue =
                value ?? string.Empty;

            if (_statusReason == normalizedValue)
            {
                return;
            }

            _statusReason = normalizedValue;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasStatusReason));
        }
    }

    public string TechnicianNote
    {
        get => _technicianNote;
        set
        {
            var normalizedValue =
                value ?? string.Empty;

            if (_technicianNote == normalizedValue)
            {
                return;
            }

            _technicianNote = normalizedValue;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasTechnicianNote));
        }
    }

    public List<CheckupTaskActionResult> ActionResults
    {
        get => _actionResults;
        set
        {
            var normalizedValue =
                value
                ?? new List<CheckupTaskActionResult>();

            if (ReferenceEquals(
                    _actionResults,
                    normalizedValue))
            {
                return;
            }

            _actionResults =
                normalizedValue;

            NotifyActionResultPropertiesChanged();
        }
    }

    [JsonIgnore]
    public bool IsOpen =>
        Status == CheckupTaskStatus.Open;

    [JsonIgnore]
    public bool IsDocumented =>
        Status != CheckupTaskStatus.Open;

    [JsonIgnore]
    public bool HasStatusReason =>
        !string.IsNullOrWhiteSpace(
            StatusReason);

    [JsonIgnore]
    public bool HasTechnicianNote =>
        !string.IsNullOrWhiteSpace(
            TechnicianNote);

    [JsonIgnore]
    public bool HasActionResults =>
        ActionResults.Count > 0;

    [JsonIgnore]
    public int ActionResultCount =>
        ActionResults.Count;

    [JsonIgnore]
    public CheckupTaskActionResult? LatestActionResult =>
        ActionResults
            .OrderByDescending(
                result =>
                    result.FinishedAt
                    ?? result.StartedAt
                    ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

    [JsonIgnore]
    public bool HasSuccessfulActionAwaitingVerification =>
        IsOpen
        && ActionResults.Any(
            result =>
                result.Status
                == CheckupTaskActionStatus.Successful);

    [JsonIgnore]
    public bool HasRestartRequirement =>
        ActionResults.Any(
            result =>
                result.RestartRequired);

    [JsonIgnore]
    public string ActionProgressText
    {
        get
        {
            if (!HasActionResults)
            {
                return
                    "Für diese Aufgabe wurde noch keine "
                    + "technische Aktion dokumentiert.";
            }

            if (HasSuccessfulActionAwaitingVerification)
            {
                return
                    "Aktion ausgeführt – Abschlusskontrolle "
                    + "ausstehend.";
            }

            return ActionResultCount == 1
                ? "Eine technische Aktion wurde dokumentiert."
                : $"{ActionResultCount} technische Aktionen "
                  + "wurden dokumentiert.";
        }
    }

    [JsonIgnore]
    public string PriorityText =>
        Priority switch
        {
            CheckupTaskPriority.Required =>
                "Pflicht",

            CheckupTaskPriority.Recommended =>
                "Empfohlen",

            _ =>
                "Optional"
        };

    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            CheckupTaskStatus.Completed =>
                "Erledigt",

            CheckupTaskStatus.Skipped =>
                "Übersprungen",

            CheckupTaskStatus.NotFeasible =>
                "Nicht durchführbar",

            _ =>
                "Offen"
        };

    [JsonIgnore]
    public string CategoryText =>
        Category switch
        {
            CheckupTaskCategory.OperatingSystem =>
                "Betriebssystem",

            CheckupTaskCategory.Security =>
                "Sicherheit",

            CheckupTaskCategory.WindowsUpdate =>
                "Windows Update",

            CheckupTaskCategory.ProgramUpdates =>
                "Programmupdates",

            CheckupTaskCategory.Restart =>
                "Neustart",

            CheckupTaskCategory.Storage =>
                "Speicher und Datenträger",

            CheckupTaskCategory.Performance =>
                "Leistung und Autostart",

            CheckupTaskCategory.DevicesAndDrivers =>
                "Geräte und Treiber",

            CheckupTaskCategory.Hardware =>
                "Hardware",

            _ =>
                "Allgemein"
        };

    [JsonIgnore]
    public string StatusChangedAtText =>
        StatusChangedAt.HasValue
            ? StatusChangedAt.Value.ToString(
                "dd.MM.yyyy HH:mm")
              + " Uhr"
            : string.Empty;

    public void ApplyStatus(
        CheckupTaskStatus status,
        string statusReason,
        string technicianNote)
    {
        if (string.IsNullOrWhiteSpace(
                statusReason))
        {
            throw new ArgumentException(
                "Für eine Statusänderung ist eine "
                + "Begründung erforderlich.",
                nameof(statusReason));
        }

        Status =
            status;

        StatusChangedAt =
            DateTime.Now;

        StatusReason =
            statusReason.Trim();

        TechnicianNote =
            technicianNote?.Trim()
            ?? string.Empty;
    }

    internal void AddActionResult(
        CheckupTaskActionResult actionResult)
    {
        ArgumentNullException.ThrowIfNull(
            actionResult);

        ActionResults.Add(
            actionResult);

        NotifyActionResultPropertiesChanged();
    }

    internal bool RemoveActionResult(
        Guid actionResultId)
    {
        var actionResult =
            ActionResults.FirstOrDefault(
                existingResult =>
                    existingResult.Id
                    == actionResultId);

        if (actionResult is null)
        {
            return false;
        }

        var wasRemoved =
            ActionResults.Remove(
                actionResult);

        if (wasRemoved)
        {
            NotifyActionResultPropertiesChanged();
        }

        return wasRemoved;
    }

    internal void RestoreStatus(
        CheckupTaskStatus status,
        DateTime? statusChangedAt,
        string statusReason,
        string technicianNote)
    {
        Status =
            status;

        StatusChangedAt =
            statusChangedAt;

        StatusReason =
            statusReason;

        TechnicianNote =
            technicianNote;
    }

    private void NotifyActionResultPropertiesChanged()
    {
        OnPropertyChanged(
            nameof(ActionResults));

        OnPropertyChanged(
            nameof(HasActionResults));

        OnPropertyChanged(
            nameof(ActionResultCount));

        OnPropertyChanged(
            nameof(LatestActionResult));

        OnPropertyChanged(
            nameof(HasSuccessfulActionAwaitingVerification));

        OnPropertyChanged(
            nameof(HasRestartRequirement));

        OnPropertyChanged(
            nameof(ActionProgressText));
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