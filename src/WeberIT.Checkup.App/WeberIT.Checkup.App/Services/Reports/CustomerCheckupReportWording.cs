
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Einheitliche, kundenverständliche Bezeichnungen
/// für den Weber-IT-Kundencheckup-Bericht.
/// </summary>
public static class CustomerCheckupReportWording
{
    public static string GetFindingStatusText(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.Resolved =>
                "Behoben",

            CustomerCheckupFindingComparisonStatus.StillOpen =>
                "Weiterhin zu beachten",

            CustomerCheckupFindingComparisonStatus.NewlyDetected =>
                "Neu festgestellt",

            CustomerCheckupFindingComparisonStatus.NotReevaluatable =>
                "Kontrolle noch offen",

            CustomerCheckupFindingComparisonStatus.Informational =>
                "Information",

            _ =>
                "Nicht eindeutig bewertet"
        };
    }

    public static string GetFindingPriorityText(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                "Dringend",

            FindingSeverity.Warning =>
                "Wichtig",

            FindingSeverity.Recommendation =>
                "Empfohlen",

            FindingSeverity.Information =>
                "Zur Information",

            _ =>
                "Nicht eingestuft"
        };
    }

    public static string GetAreaStatusText(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy =>
                "Alles in Ordnung",

            CustomerCheckupAreaComparisonStatus.Improved =>
                "Verbessert",

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    "Verbessert, weitere Prüfung empfohlen",

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    "Weiterhin zu beachten",

            CustomerCheckupAreaComparisonStatus.Worsened =>
                "Verschlechtert",

            CustomerCheckupAreaComparisonStatus.NewlyNeedsAttention =>
                "Neuer Hinweis",

            CustomerCheckupAreaComparisonStatus.NotComparable =>
                "Nicht vollständig geprüft",

            _ =>
                "Keine eindeutige Bewertung"
        };
    }

    public static string GetTaskStatusText(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus.Completed =>
                "Erledigt",

            CustomerCheckupTaskComparisonStatus.CompletedButStillDetected =>
                "Bearbeitet, weitere Prüfung empfohlen",

            CustomerCheckupTaskComparisonStatus.StillOpen =>
                "Noch offen",

            CustomerCheckupTaskComparisonStatus.NoLongerDetected =>
                "Problem nicht mehr festgestellt",

            CustomerCheckupTaskComparisonStatus.Skipped =>
                "Nicht durchgeführt",

            CustomerCheckupTaskComparisonStatus.NotFeasible =>
                "Nicht durchführbar",

            CustomerCheckupTaskComparisonStatus.NewlyDetected =>
                "Neu hinzugekommen",

            CustomerCheckupTaskComparisonStatus.NotReevaluatable =>
                "Ergebnis noch nicht bestätigt",

            _ =>
                "Nicht eindeutig bewertet"
        };
    }

    public static string GetScoreChangeText(
        CustomerCheckupScoreComparison score)
    {
        ArgumentNullException.ThrowIfNull(score);

        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return "Die beiden Bewertungen lassen sich "
                   + "nicht zuverlässig miteinander vergleichen.";
        }

        return score.Difference.Value switch
        {
            > 0 =>
                "Die Bewertung hat sich verbessert.",

            < 0 =>
                "Die Bewertung hat sich verschlechtert.",

            _ =>
                "Die Bewertung ist unverändert geblieben."
        };
    }

    public static string GetFindingCountText(int count)
    {
        return count switch
        {
            0 => "Keine weiteren Auffälligkeiten",
            1 => "1 Hinweis für das weitere Vorgehen",
            _ => $"{count} Hinweise für das weitere Vorgehen"
        };
    }
}
