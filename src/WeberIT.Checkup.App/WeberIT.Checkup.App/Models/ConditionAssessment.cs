
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

    /*
     * SCHRITT 49.1:
     *
     * Die Anzahl auswertbarer Bereiche allein
     * reicht für einen zuverlässigen Vergleich
     * zweier Bewertungen nicht aus.
     *
     * Unterschiedliche Bereiche können
     * unterschiedliche Gewichtungen besitzen.
     *
     * Deshalb dokumentiert jede neue Bewertung
     * künftig zusätzlich die stabilen Kennungen
     * ihrer Bewertungsbereiche.
     *
     * EvaluatedAreaCodes:
     * Alle Bereiche, die das Bewertungsmodell
     * grundsätzlich berücksichtigt.
     *
     * AvailableAreaCodes:
     * Ausschließlich die Bereiche, deren Daten
     * im betreffenden Scan auswertbar waren.
     *
     * Die Kennungen müssen unabhängig von
     * sichtbaren Bezeichnungen und deren
     * späteren Textänderungen stabil bleiben.
     *
     * Beide Listen werden mit dem Checkup
     * dauerhaft serialisiert.
     *
     * Ältere gespeicherte Bewertungen besitzen
     * diese Informationen noch nicht.
     *
     * Leere Listen werden deshalb NICHT als
     * Beweis dafür interpretiert, dass keine
     * Bewertungsbereiche vorhanden waren.
     *
     * Die Vergleichslogik wird in einem
     * gesonderten Entwicklungsschritt
     * entsprechend erweitert.
     */

    public List<string> EvaluatedAreaCodes { get; set; } =
        new();

    public List<string> AvailableAreaCodes { get; set; } =
        new();

    /*
     * Diese Eigenschaft dient ausschließlich
     * der Diagnose und zukünftigen
     * Vergleichsprüfung.
     *
     * Eine vollständige Dokumentation setzt
     * voraus, dass die gespeicherten Kennungen
     * zu den ebenfalls gespeicherten
     * Bereichszahlen passen.
     *
     * Die endgültige Prüfung auf doppelte,
     * ungültige oder widersprüchliche Kennungen
     * erfolgt in der Vergleichslogik.
     */
    [JsonIgnore]
    public bool HasAreaCoverageDetails =>
        EvaluatedAreaCodes is not null
        && AvailableAreaCodes is not null
        && EvaluatedAreaCount > 0
        && EvaluatedAreaCodes.Count == EvaluatedAreaCount
        && AvailableAreaCodes.Count == AvailableAreaCount;

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
