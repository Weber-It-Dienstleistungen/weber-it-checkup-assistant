using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTask
{
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

    public CheckupTaskStatus Status { get; set; } =
        CheckupTaskStatus.Open;

    public DateTime CreatedAt { get; set; } =
        DateTime.Now;

    public DateTime? StatusChangedAt { get; set; }

    public string StatusReason { get; set; } =
        string.Empty;

    public string TechnicianNote { get; set; } =
        string.Empty;

    [JsonIgnore]
    public bool IsOpen =>
        Status == CheckupTaskStatus.Open;

    [JsonIgnore]
    public bool IsDocumented =>
        Status != CheckupTaskStatus.Open;

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
}