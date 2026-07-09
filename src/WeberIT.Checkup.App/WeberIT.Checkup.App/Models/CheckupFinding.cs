namespace WeberIT.Checkup.App.Models;

public class CheckupFinding
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public FindingCategory Category { get; set; }

    public FindingSeverity Severity { get; set; }
}