using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupAssessment
{
    public int ScoringVersion { get; set; }

    public DateTime? AssessmentCreatedAt { get; set; }

    public List<CheckupFinding> Findings { get; set; } =
        new();

    public ConditionAssessment SystemCondition { get; set; } =
        new();

    public ConditionAssessment HardwareCondition { get; set; } =
        new();

    public HardwarePlanningHorizon HardwarePlanningHorizon
    {
        get;
        set;
    } = HardwarePlanningHorizon.NotAvailable;

    public string HardwarePlanningSummary { get; set; } =
        string.Empty;

    [JsonIgnore]
    public string HardwarePlanningHorizonText =>
        HardwarePlanningHorizon switch
        {
            HardwarePlanningHorizon.LongTermSuitable =>
                "Langfristig geeignet",

            HardwarePlanningHorizon.MediumTermUsable =>
                "Mittelfristig weiter nutzbar",

            HardwarePlanningHorizon.ConsiderUpgrade =>
                "Aufrüstung prüfen",

            HardwarePlanningHorizon
                .ConsiderReplacementPlanning =>
                    "Ersatzplanung prüfen",

            HardwarePlanningHorizon
                .ConsiderPromptReplacement =>
                    "Zeitnahen Ersatz prüfen",

            _ =>
                "Keine belastbare Planungsaussage"
        };

    [JsonIgnore]
    public string HardwarePlanningSummaryText =>
        string.IsNullOrWhiteSpace(
            HardwarePlanningSummary)
                ? "Aus den vorhandenen Hardwareinformationen "
                  + "kann derzeit keine belastbare "
                  + "Planungsaussage abgeleitet werden."
                : HardwarePlanningSummary;

    [JsonIgnore]
    public string ScoringVersionText =>
        ScoringVersion > 0
            ? $"Bewertungsmodell Version {ScoringVersion}"
            : "Historischer Checkup ohne getrennte Bewertung";
}