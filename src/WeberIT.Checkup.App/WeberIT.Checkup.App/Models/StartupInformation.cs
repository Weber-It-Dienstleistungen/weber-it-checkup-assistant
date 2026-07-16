using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class StartupInformation
{
    public DateTime? AnalysisDate { get; set; }

    public long? AnalysisDurationMilliseconds { get; set; }

    public StartupAnalysisStatus AnalysisStatus { get; set; } =
        StartupAnalysisStatus.NotAnalyzed;

    public string AnalysisMessage { get; set; } =
        string.Empty;

    public List<StartupEntryInformation> Entries { get; set; } =
        new();

    [JsonIgnore]
    public bool HasAnalysis =>
        AnalysisStatus != StartupAnalysisStatus.NotAnalyzed;

    [JsonIgnore]
    public int TotalEntryCount =>
        Entries.Count;

    [JsonIgnore]
    public int EnabledEntryCount =>
        Entries.Count(
            entry =>
                entry.State == StartupEntryState.Enabled);

    [JsonIgnore]
    public int DisabledEntryCount =>
        Entries.Count(
            entry =>
                entry.State == StartupEntryState.Disabled);

    [JsonIgnore]
    public int UnknownStateEntryCount =>
        Entries.Count(
            entry =>
                entry.State == StartupEntryState.Unknown);

    [JsonIgnore]
    public int OptionalReviewEntryCount =>
        Entries.Count(
            entry =>
                entry.State == StartupEntryState.Enabled
                && entry.Classification
                    == StartupClassification.OptionalReview);

    [JsonIgnore]
    public int ConspicuousEntryCount =>
        Entries.Count(
            entry =>
                entry.State == StartupEntryState.Enabled
                && entry.Classification
                    == StartupClassification.Conspicuous);

    [JsonIgnore]
    public bool HasFailedOrIncompleteAnalysis =>
        AnalysisStatus
            is StartupAnalysisStatus.PartiallyAnalyzed
            or StartupAnalysisStatus.NotEvaluable
            or StartupAnalysisStatus.TimedOut;

    [JsonIgnore]
    public string AnalysisStatusText =>
        AnalysisStatus switch
        {
            StartupAnalysisStatus.NotAnalyzed =>
                "Nicht in diesem Checkup enthalten",

            StartupAnalysisStatus.Analyzed =>
                "Vollständig analysiert",

            StartupAnalysisStatus.PartiallyAnalyzed =>
                "Teilweise analysiert",

            StartupAnalysisStatus.NotEvaluable =>
                "Nicht auswertbar",

            StartupAnalysisStatus.TimedOut =>
                "Zeitlimit erreicht",

            _ =>
                "Unbekannter Analysestatus"
        };

    [JsonIgnore]
    public string AnalysisMessageText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    AnalysisMessage))
            {
                return AnalysisMessage;
            }

            return HasAnalysis
                ? "Es sind keine weiteren Angaben zur Autostartanalyse verfügbar."
                : "Dieser gespeicherte Checkup wurde vor Einführung der Autostartanalyse erstellt.";
        }
    }

    [JsonIgnore]
    public string AnalysisDateText =>
        AnalysisDate.HasValue
            ? AnalysisDate.Value.ToString(
                "dd.MM.yyyy HH:mm")
            : "Nicht verfügbar";

    [JsonIgnore]
    public string AnalysisDurationText
    {
        get
        {
            if (!AnalysisDurationMilliseconds.HasValue)
            {
                return "Nicht verfügbar";
            }

            var duration =
                TimeSpan.FromMilliseconds(
                    AnalysisDurationMilliseconds.Value);

            return duration.TotalSeconds >= 1
                ? $"{duration.TotalSeconds:0.##} Sekunden"
                : $"{Math.Max(0, duration.TotalMilliseconds):0} ms";
        }
    }
}