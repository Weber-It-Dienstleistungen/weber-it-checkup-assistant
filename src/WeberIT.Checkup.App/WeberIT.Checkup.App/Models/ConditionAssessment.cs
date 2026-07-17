namespace WeberIT.Checkup.App.Models;

public class ConditionAssessment
{
    public int? Score { get; set; }

    public ConditionRating Rating { get; set; } =
        ConditionRating.NotAvailable;

    public AssessmentDataQuality DataQuality { get; set; } =
        AssessmentDataQuality.NotAvailable;

    public string Summary { get; set; } =
        string.Empty;

    public int EvaluatedAreaCount { get; set; }

    public int AvailableAreaCount { get; set; }
}