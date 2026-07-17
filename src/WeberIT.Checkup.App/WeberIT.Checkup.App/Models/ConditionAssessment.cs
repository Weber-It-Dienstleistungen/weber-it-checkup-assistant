using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class ConditionAssessment
{
    public int? Score { get; set; }

    public ConditionRating Rating { get; set; } =
        ConditionRating.NotAvailable;

    public AssessmentDataQuality DataQuality { get; set; } =
        AssessmentDataQuality.NotAvailable;

    public string Summary { get; set; } =
        string.Empty;

    public int EvaluatedAreaCount { get; set; }

    public int AvailableAreaCount { get; set; }

    [JsonIgnore]
    public bool HasScore =>
        Score.HasValue;

    [JsonIgnore]
    public string ScoreText =>
        Score.HasValue
            ? $"{Score.Value} von 100"
            : "Nicht verfügbar";

    [JsonIgnore]
    public string RatingText =>
        Rating switch
        {
            ConditionRating.VeryGood =>
                "Sehr guter Zustand",

            ConditionRating.Good =>
                "Guter Zustand",

            ConditionRating.NeedsAttention =>
                "Prüfungs- oder Wartungsbedarf",

            ConditionRating.Critical =>
                "Kritischer Zustand",

            _ =>
                "Nicht bewertbar"
        };

    [JsonIgnore]
    public string DataQualityText =>
        DataQuality switch
        {
            AssessmentDataQuality.Good =>
                "Gute Datengrundlage",

            AssessmentDataQuality.Sufficient =>
                "Ausreichende Datengrundlage",

            AssessmentDataQuality.Limited =>
                "Eingeschränkte Datengrundlage",

            _ =>
                "Keine ausreichende Datengrundlage"
        };

    [JsonIgnore]
    public string CoverageText
    {
        get
        {
            if (EvaluatedAreaCount <= 0)
            {
                return "Keine Bewertungsbereiche verfügbar";
            }

            return $"{AvailableAreaCount} von "
                   + $"{EvaluatedAreaCount} Bereichen auswertbar";
        }
    }

    [JsonIgnore]
    public string SummaryText =>
        string.IsNullOrWhiteSpace(Summary)
            ? "Es ist keine zusammenfassende Bewertung verfügbar."
            : Summary;
}