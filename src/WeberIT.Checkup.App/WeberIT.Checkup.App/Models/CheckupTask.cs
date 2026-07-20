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

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}