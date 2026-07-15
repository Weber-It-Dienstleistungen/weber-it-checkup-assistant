using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class RestartInformation
{
    public bool IsAnalysisPerformed { get; set; }

    public bool IsAnalysisConclusive { get; set; }

    public bool? IsRestartRequired { get; set; }

    public DateTime? AnalysisDate { get; set; }

    public List<RestartSourceResult> Sources { get; set; } =
        new();

    [JsonIgnore]
    public bool HasAdvisoryRestartHint =>
        IsRestartRequired != true
        && Sources.Any(source =>
            source.SourceType
                == RestartSourceType
                    .PendingFileRenameOperations
            && source.IsCheckSuccessful
            && source.IsRestartRequired == true);

    [JsonIgnore]
    public bool HasFailedSources =>
        Sources.Any(source =>
            !source.IsCheckSuccessful);
}