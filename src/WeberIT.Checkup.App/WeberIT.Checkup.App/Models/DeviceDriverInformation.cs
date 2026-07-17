using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class DeviceDriverInformation
{
    public DateTime? AnalysisDate { get; set; }

    public long? AnalysisDurationMilliseconds { get; set; }

    public DeviceDriverAnalysisStatus AnalysisStatus
    {
        get;
        set;
    } = DeviceDriverAnalysisStatus.NotAnalyzed;

    public string AnalysisMessage { get; set; } =
        string.Empty;

    public int EvaluatedDeviceCount { get; set; }

    public int AggregatedDeviceCount { get; set; }

    public int ProblemDeviceCount { get; set; }

    public int MissingDriverCount { get; set; }

    public int DisabledDeviceCount { get; set; }

    public int NotEvaluableDeviceCount { get; set; }

    public List<DeviceDriverEntryInformation> Entries
    {
        get;
        set;
    } = new();

    [JsonIgnore]
    public bool HasAnalysis =>
        AnalysisStatus != DeviceDriverAnalysisStatus.NotAnalyzed;

    [JsonIgnore]
    public int DetailedDeviceCount =>
        Entries.Count;

    [JsonIgnore]
    public List<DeviceDriverEntryInformation> ReviewEntries =>
        Entries
            .Where(
                entry =>
                    entry.Classification
                        is DeviceDriverClassification.MissingDriver
                        or DeviceDriverClassification.WindowsProblem
                        or DeviceDriverClassification.UnsignedDriver
                        or DeviceDriverClassification.Disabled
                        or DeviceDriverClassification.NotEvaluable)
            .ToList();

    [JsonIgnore]
    public int ReviewEntryCount =>
        ReviewEntries.Count;

    [JsonIgnore]
    public bool HasReviewEntries =>
        ReviewEntryCount > 0;

    [JsonIgnore]
    public bool HasNoReviewEntries =>
        !HasReviewEntries;

    [JsonIgnore]
    public bool HasProblems =>
        ProblemDeviceCount > 0
        || MissingDriverCount > 0;

    [JsonIgnore]
    public bool HasFailedOrIncompleteAnalysis =>
        AnalysisStatus
            is DeviceDriverAnalysisStatus.PartiallyAnalyzed
            or DeviceDriverAnalysisStatus.NotEvaluable
            or DeviceDriverAnalysisStatus.TimedOut;

    [JsonIgnore]
    public string AnalysisStatusText =>
        AnalysisStatus switch
        {
            DeviceDriverAnalysisStatus.NotAnalyzed =>
                "Nicht in diesem Checkup enthalten",

            DeviceDriverAnalysisStatus.Analyzed =>
                "Vollständig analysiert",

            DeviceDriverAnalysisStatus.PartiallyAnalyzed =>
                "Teilweise analysiert",

            DeviceDriverAnalysisStatus.NotEvaluable =>
                "Nicht auswertbar",

            DeviceDriverAnalysisStatus.TimedOut =>
                "Zeitlimit erreicht",

            _ =>
                "Unbekannter Analysestatus"
        };

    [JsonIgnore]
    public string AnalysisMessageText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AnalysisMessage))
            {
                return AnalysisMessage;
            }

            return HasAnalysis
                ? "Es sind keine weiteren Angaben zur Geräte- und Treiberanalyse verfügbar."
                : "Dieser gespeicherte Checkup wurde vor Einführung der Geräte- und Treiberanalyse erstellt.";
        }
    }

    [JsonIgnore]
    public string AnalysisDateText =>
        AnalysisDate.HasValue
            ? AnalysisDate.Value.ToString("dd.MM.yyyy HH:mm")
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