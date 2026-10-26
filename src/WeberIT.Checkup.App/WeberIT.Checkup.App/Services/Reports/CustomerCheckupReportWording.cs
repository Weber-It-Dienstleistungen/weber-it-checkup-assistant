
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Einheitliche, kundenverständliche Bezeichnungen
/// für den Weber-IT-Kundencheckup-Bericht.
///
/// Die Formulierungen unterscheiden ausdrücklich
/// zwischen technischen Befunden, ausgeführten
/// Arbeiten und der Zuverlässigkeit einer Kontrolle.
///
/// Technische Statuswerte und Bewertungen werden
/// durch diese Klasse nicht verändert.
/// </summary>
public static class CustomerCheckupReportWording
{
    public static string GetFindingStatusText(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.Resolved =>
                "Nicht mehr festgestellt",

            CustomerCheckupFindingComparisonStatus.StillOpen =>
                "Weiterhin vorhanden",

            CustomerCheckupFindingComparisonStatus.NewlyDetected =>
                "Neu festgestellt",

            CustomerCheckupFindingComparisonStatus.NotReevaluatable =>
                "Nicht erneut auswertbar",

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
                "Keine Auffälligkeiten erkannt",

            CustomerCheckupAreaComparisonStatus.Improved =>
                "Technisch verbessert",

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    "Verbessert, weitere Auffälligkeiten vorhanden",

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    "Weiterhin auffällig",

            CustomerCheckupAreaComparisonStatus.Worsened =>
                "Technisch verschlechtert",

            CustomerCheckupAreaComparisonStatus.NewlyNeedsAttention =>
                "Neue Auffälligkeit",

            /*
             * SCHRITT 53.1:
             *
             * Vergleichsmodell V3 kann positive
             * und negative Veränderungen
             * innerhalb desselben technischen
             * Bereichs feststellen.
             *
             * Diese Veränderungen sind
             * auswertbar, besitzen aber keine
             * eindeutige Gesamtrichtung.
             *
             * Sie dürfen deshalb nicht als
             * "nicht vergleichbar" bezeichnet
             * werden.
             */
            CustomerCheckupAreaComparisonStatus.MixedChanges =>
                "Teils verbessert, teils verschlechtert",

            CustomerCheckupAreaComparisonStatus.NotComparable =>
                "Nicht zuverlässig vergleichbar",

            _ =>
                "Keine eindeutige Bewertung"
        };
    }

    /// <summary>
    /// Erklärt den fachlichen Unterschied
    /// zwischen einer gemischten Entwicklung
    /// und einer fehlenden Vergleichbarkeit.
    ///
    /// Diese Erklärung ist ausschließlich
    /// für die Berichtspräsentation bestimmt.
    /// </summary>
    public static string GetAreaStatusExplanation(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.MixedChanges =>
                "In diesem Prüfbereich wurden sowohl "
                + "Verbesserungen als auch Verschlechterungen "
                + "festgestellt. Eine insgesamt positive "
                + "oder negative Entwicklung lässt sich "
                + "daraus nicht eindeutig ableiten.",

            CustomerCheckupAreaComparisonStatus.NotComparable =>
                "Dieser Prüfbereich konnte zwischen "
                + "Eingangs- und Abschlusskontrolle "
                + "nicht ausreichend zuverlässig "
                + "verglichen werden. Daraus ergibt "
                + "sich keine gesicherte Aussage "
                + "über eine Verbesserung oder "
                + "Verschlechterung.",

            _ => string.Empty
        };
    }

    public static string GetTaskStatusText(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus.Completed =>
                "Als erledigt dokumentiert",

            CustomerCheckupTaskComparisonStatus.CompletedButStillDetected =>
                "Bearbeitet, Befund weiterhin vorhanden",

            CustomerCheckupTaskComparisonStatus.StillOpen =>
                "Aufgabe noch offen",

            CustomerCheckupTaskComparisonStatus.NoLongerDetected =>
                "Befund nicht mehr erkannt",

            CustomerCheckupTaskComparisonStatus.Skipped =>
                "Nicht durchgeführt",

            CustomerCheckupTaskComparisonStatus.NotFeasible =>
                "Nicht durchführbar",

            CustomerCheckupTaskComparisonStatus.NewlyDetected =>
                "Neu hinzugekommen",

            CustomerCheckupTaskComparisonStatus.NotReevaluatable =>
                "Ergebnis nicht erneut auswertbar",

            _ =>
                "Nicht eindeutig bewertet"
        };
    }

    public static string GetScoreChangeText(
        CustomerCheckupScoreComparison score)
    {
        ArgumentNullException.ThrowIfNull(score);

        /*
         * Ein direkter Vergleich ist nur zulässig,
         * wenn das Vergleichsmodell diesen ausdrücklich
         * als belastbar einstuft.
         *
         * Gleiche oder unterschiedliche Punktwerte
         * begründen andernfalls keine Aussage über
         * die Veränderung des technischen Zustands.
         */
        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return "Die Bewertungen lassen sich nicht "
                   + "zuverlässig miteinander vergleichen. "
                   + "Aus den Punktwerten kann daher "
                   + "keine gesicherte Verbesserung oder "
                   + "Verschlechterung abgeleitet werden.";
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
            0 =>
                "Keine weiteren erfassten Hinweise",

            1 =>
                "1 Hinweis für das weitere Vorgehen",

            _ =>
                $"{count} Hinweise für das weitere Vorgehen"
        };
    }
}
