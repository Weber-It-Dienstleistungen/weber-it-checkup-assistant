namespace WeberIT.Checkup.App.Models;

public class RestartSourceResult
{
    public RestartSourceType SourceType { get; set; } =
        RestartSourceType.Unknown;

    public string DisplayName { get; set; } =
        string.Empty;

    public bool IsCheckSuccessful { get; set; }

    public bool? IsRestartRequired { get; set; }

    public string Details { get; set; } =
        string.Empty;
}