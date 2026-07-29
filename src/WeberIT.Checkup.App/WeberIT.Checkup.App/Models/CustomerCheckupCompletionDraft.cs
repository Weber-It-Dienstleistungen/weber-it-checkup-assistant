namespace WeberIT.Checkup.App.Models;

public sealed class CustomerCheckupCompletionDraft
{
    public string TechnicianSummary { get; set; } =
        string.Empty;

    public string NextSteps { get; set; } =
        string.Empty;

    public DateTime? NextCheckupDate { get; set; }
}