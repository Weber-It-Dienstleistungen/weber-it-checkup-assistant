namespace WeberIT.Checkup.App.Models;

public class ProgramUpdateInformation
{
    public bool? IsWingetAvailable { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string WingetAvailabilityText => IsWingetAvailable switch
    {
        true => "Ja", false => "Nein", null => "Nicht zuverlässig ermittelt"
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public string EmptyUpdateListText => IsWingetAvailable == true && IsAnalysisPerformed
        && IsAnalysisSuccessful && AvailableUpdateCount == 0 && !IsResultTruncated
        ? "Keine ausstehenden Programmaktualisierungen erkannt."
        : "Keine auswertbare Programmliste verfügbar.";

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