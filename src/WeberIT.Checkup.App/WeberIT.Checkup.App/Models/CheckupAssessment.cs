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
    public IReadOnlyList<CheckupFinding>
        ActionableFindings =>
            GetAvailableFindings()
                .Where(
                    finding =>
                        finding.Severity
                        != FindingSeverity.Information)
                .OrderByDescending(
                    finding =>
                        GetSeverityPriority(
                            finding.Severity))
                .ToList();

    [JsonIgnore]
    public IReadOnlyList<CheckupFinding>
        InformationalFindings =>
            GetAvailableFindings()
                .Where(
                    finding =>
                        finding.Severity
                        == FindingSeverity.Information)
                .ToList();

    [JsonIgnore]
    public bool HasActionableFindings =>
        ActionableFindings.Count > 0;

    [JsonIgnore]
    public bool HasInformationalFindings =>
        InformationalFindings.Count > 0;

    [JsonIgnore]
    public string FindingsOverviewText
    {
        get
        {
            var actionableCount =
                ActionableFindings.Count;

            var informationalCount =
                InformationalFindings.Count;

            var actionableText =
                actionableCount switch
                {
                    0 =>
                        "Keine handlungsrelevanten Befunde",

                    1 =>
                        "1 handlungsrelevanter Befund",

                    _ =>
                        $"{actionableCount} "
                        + "handlungsrelevante Befunde"
                };

            if (informationalCount == 0)
            {
                return actionableText;
            }

            var informationalText =
                informationalCount == 1
                    ? "1 weiterer Informationsbefund eingeklappt"
                    : $"{informationalCount} weitere "
                      + "Informationsbefunde eingeklappt";

            return actionableText
                   + " · "
                   + informationalText;
        }
    }

    [JsonIgnore]
    public string InformationalFindingsHeaderText
    {
        get
        {
            var informationalCount =
                InformationalFindings.Count;

            return informationalCount == 1
                ? "Weitere Systeminformation (1)"
                : $"Weitere Systeminformationen "
                  + $"({informationalCount})";
        }
    }

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

    private IEnumerable<CheckupFinding>
        GetAvailableFindings()
    {
        return Findings
               ?? Enumerable.Empty<CheckupFinding>();
    }

    private static int GetSeverityPriority(
        FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                3,

            FindingSeverity.Warning =>
                2,

            FindingSeverity.Recommendation =>
                1,

            _ =>
                0
        };
    }
}