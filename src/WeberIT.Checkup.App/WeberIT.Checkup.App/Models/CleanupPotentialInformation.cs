using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CleanupPotentialInformation
{
    public DateTime? AnalysisDate { get; set; }

    public long? AnalysisDurationMilliseconds { get; set; }

    public string TargetVolume { get; set; } =
        string.Empty;

    public string ScopeDescription { get; set; } =
        string.Empty;

    public CleanupMeasurementStatus AnalysisStatus
    {
        get;
        set;
    } = CleanupMeasurementStatus.NotAnalyzed;

    public string AnalysisMessage { get; set; } =
        string.Empty;

    public List<CleanupCategoryResult> Categories
    {
        get;
        set;
    } = new();

    [JsonIgnore]
    public ulong SafePotentialBytes =>
        SumCategorySizes(
            CleanupCategoryClassification.SafePotential);

    [JsonIgnore]
    public ulong ManualReviewPotentialBytes =>
        SumCategorySizes(
            CleanupCategoryClassification.ManualReview);

    [JsonIgnore]
    public bool HasAnalysis =>
        AnalysisStatus
            != CleanupMeasurementStatus.NotAnalyzed;

    [JsonIgnore]
    public string AnalysisStatusText =>
        AnalysisStatus switch
        {
            CleanupMeasurementStatus.NotAnalyzed =>
                "Nicht in diesem Checkup enthalten",

            CleanupMeasurementStatus.Measured =>
                "Vollständig analysiert",

            CleanupMeasurementStatus.PartiallyMeasured =>
                "Teilweise analysiert",

            CleanupMeasurementStatus.NotEvaluable =>
                "Nicht auswertbar",

            CleanupMeasurementStatus.Excluded =>
                "Von der Analyse ausgeschlossen",

            CleanupMeasurementStatus.TimedOut =>
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
                ? "Es sind keine weiteren Angaben "
                  + "zur Bereinigungsanalyse verfügbar."
                : "Dieser gespeicherte Checkup wurde vor "
                  + "Einführung der Bereinigungsanalyse erstellt.";
        }
    }

    [JsonIgnore]
    public string AnalysisDateText =>
        AnalysisDate.HasValue
            ? AnalysisDate.Value
                .ToString("dd.MM.yyyy HH:mm")
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

    [JsonIgnore]
    public string TargetVolumeText =>
        string.IsNullOrWhiteSpace(TargetVolume)
            ? "Nicht verfügbar"
            : TargetVolume;

    [JsonIgnore]
    public string ScopeDescriptionText =>
        string.IsNullOrWhiteSpace(
            ScopeDescription)
                ? "Keine Angaben zum Analyseumfang verfügbar."
                : ScopeDescription;

    [JsonIgnore]
    public string SafePotentialSizeText =>
        FormatBytes(
            SafePotentialBytes);

    [JsonIgnore]
    public string ManualReviewPotentialSizeText =>
        FormatBytes(
            ManualReviewPotentialBytes);

    private ulong SumCategorySizes(
        CleanupCategoryClassification classification)
    {
        ulong totalSize =
            0;

        foreach (var category in Categories)
        {
            if (category.Classification
                    != classification
                || !category.HasMeasuredSize
                || !category.SizeBytes.HasValue)
            {
                continue;
            }

            var sizeBytes =
                category.SizeBytes.Value;

            if (ulong.MaxValue - totalSize
                < sizeBytes)
            {
                return ulong.MaxValue;
            }

            totalSize += sizeBytes;
        }

        return totalSize;
    }

    private static string FormatBytes(
        ulong sizeBytes)
    {
        const double oneKilobyte =
            1024d;

        const double oneMegabyte =
            oneKilobyte * 1024d;

        const double oneGigabyte =
            oneMegabyte * 1024d;

        if (sizeBytes >= oneGigabyte)
        {
            return $"{sizeBytes / oneGigabyte:0.##} GB";
        }

        if (sizeBytes >= oneMegabyte)
        {
            return $"{sizeBytes / oneMegabyte:0.##} MB";
        }

        if (sizeBytes >= oneKilobyte)
        {
            return $"{sizeBytes / oneKilobyte:0.##} KB";
        }

        return $"{sizeBytes:N0} Byte";
    }
}