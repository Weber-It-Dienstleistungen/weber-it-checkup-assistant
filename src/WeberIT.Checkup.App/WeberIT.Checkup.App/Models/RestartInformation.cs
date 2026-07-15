namespace WeberIT.Checkup.App.Models;

public class RestartInformation
{
    public bool IsAnalysisPerformed { get; set; }

    public bool IsAnalysisConclusive { get; set; }

    public bool? IsRestartRequired { get; set; }

    public DateTime? AnalysisDate { get; set; }

    public List<RestartSourceResult> Sources { get; set; } =
        new();
}