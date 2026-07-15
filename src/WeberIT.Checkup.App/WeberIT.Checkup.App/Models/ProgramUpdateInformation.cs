namespace WeberIT.Checkup.App.Models;

public class ProgramUpdateInformation
{
    public bool? IsWingetAvailable { get; set; }

    public string WingetVersion { get; set; } =
        string.Empty;

    public bool IsAnalysisPerformed { get; set; }

    public bool IsAnalysisSuccessful { get; set; }

    public DateTime? AnalysisDate { get; set; }

    public string AnalysisDetails { get; set; } =
        string.Empty;

    public int AvailableUpdateCount { get; set; }

    public bool IsResultTruncated { get; set; }

    public List<AvailableProgramUpdate> AvailableUpdates { get; set; } =
        new();
}