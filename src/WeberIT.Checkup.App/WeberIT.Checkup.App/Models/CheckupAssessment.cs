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
}