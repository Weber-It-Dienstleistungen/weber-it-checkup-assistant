namespace WeberIT.Checkup.App.Models;

public class CheckupFinding
{
    public string Code { get; set; } =
        string.Empty;

    public string Title { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public FindingCategory Category { get; set; }

    public FindingSeverity Severity { get; set; }

    public FindingAssessmentTarget AssessmentTarget
    {
        get;
        set;
    } = FindingAssessmentTarget.InformationOnly;

    public string CauseGroup { get; set; } =
        string.Empty;
}