
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Comparison;

/// <summary>
/// Bestimmt die fachliche Entwicklung eines
/// technischen Prüfbereichs zwischen Eingangs-
/// und Abschlusskontrolle.
///
/// Bewertet:
/// - Nachweislich behobene Befunde
/// - Neu erkannte Befunde
/// - Veränderungen des Schweregrades
/// - Veränderungen der Befundhäufigkeit
/// - Gleichzeitig positive und negative Entwicklungen
/// - Fehlende oder unzuverlässige Vergleichsdaten
///
/// Die Regel verändert keine gespeicherten Daten
/// und berechnet keine neuen System- oder Hardwarescores.
/// </summary>
public static class CustomerCheckupAreaStatusPolicy
{
    public static CustomerCheckupAreaComparisonStatus Determine(
        string areaCode,
        bool wasBeforeEvaluable,
        bool isAfterEvaluable,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaCode);
        ArgumentNullException.ThrowIfNull(findings);

        /*
         * SCHRITT 52.2:
         *
         * Eine technische Veränderung darf nur
         * behauptet werden, wenn der betreffende
         * Bereich sowohl vorher als auch nachher
         * zuverlässig ausgewertet werden konnte.
         */
        if (!wasBeforeEvaluable || !isAfterEvaluable)
        {
            return CustomerCheckupAreaComparisonStatus.NotComparable;
        }

        var relevantFindings =
            findings
                .Where(finding =>
                    string.Equals(
                        finding.AreaCode,
                        areaCode,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        var hasImprovement = false;
        var hasWorsening = false;

        var hadActionableFindingsBefore = false;
        var hasActionableFindingsAfter = false;

        foreach (var finding in relevantFindings)
        {
            /*
             * Neutrale Informationsbefunde
             * beeinflussen die Bewertung nicht.
             *
             * Beispiele:
             * - Virenschutz aktiv
             * - Secure Boot aktiv
             * - SSD erkannt
             */
            if (finding.Status ==
                CustomerCheckupFindingComparisonStatus.Informational)
            {
                continue;
            }

            /*
             * Falls die für diesen konkreten Befund
             * benötigten Daten nicht in beiden Scans
             * auswertbar waren, ist eine belastbare
             * Gesamtaussage nicht möglich.
             *
             * Diese Prüfung ergänzt die allgemeinere
             * Auswertbarkeit des technischen Bereichs.
             */
            if (!finding.WasBeforeAreaEvaluable
                || !finding.IsAfterAreaEvaluable)
            {
                return CustomerCheckupAreaComparisonStatus.NotComparable;
            }

            if (finding.BeforeOccurrenceCount < 0
                || finding.AfterOccurrenceCount < 0)
            {
                return CustomerCheckupAreaComparisonStatus.NotComparable;
            }

            var beforeSeverity =
                GetSeverityRank(finding.BeforeSeverity);

            var afterSeverity =
                GetSeverityRank(finding.AfterSeverity);

            switch (finding.Status)
            {
                case CustomerCheckupFindingComparisonStatus.Resolved:
                    {
                        /*
                         * Ein zuvor bestehender Befund
                         * wurde bei zuverlässiger
                         * Abschlusskontrolle nicht
                         * mehr als Mangel erkannt.
                         *
                         * Nachher kann trotzdem eine
                         * neutrale Information über
                         * dieselbe Ursache vorhanden sein.
                         */
                        if (beforeSeverity == 0
                            || finding.BeforeOccurrenceCount == 0
                            || afterSeverity > 0)
                        {
                            return CustomerCheckupAreaComparisonStatus
                                .NotComparable;
                        }

                        hadActionableFindingsBefore = true;
                        hasImprovement = true;
                        break;
                    }

                case CustomerCheckupFindingComparisonStatus.NewlyDetected:
                    {
                        /*
                         * Ein neuer Handlungsbedarf
                         * wurde bei der Abschlusskontrolle
                         * erkannt.
                         */
                        if (afterSeverity == 0
                            || finding.AfterOccurrenceCount == 0
                            || beforeSeverity > 0)
                        {
                            return CustomerCheckupAreaComparisonStatus
                                .NotComparable;
                        }

                        hasActionableFindingsAfter = true;
                        hasWorsening = true;
                        break;
                    }

                case CustomerCheckupFindingComparisonStatus.StillOpen:
                    {
                        /*
                         * Derselbe Handlungsbedarf ist
                         * in beiden Scans vorhanden.
                         *
                         * Deshalb vergleichen wir
                         * zunächst die Schweregrade
                         * und anschließend die Anzahl
                         * der Vorkommen.
                         */
                        if (beforeSeverity == 0
                            || afterSeverity == 0
                            || finding.BeforeOccurrenceCount == 0
                            || finding.AfterOccurrenceCount == 0)
                        {
                            return CustomerCheckupAreaComparisonStatus
                                .NotComparable;
                        }

                        hadActionableFindingsBefore = true;
                        hasActionableFindingsAfter = true;

                        if (afterSeverity < beforeSeverity)
                        {
                            hasImprovement = true;
                        }
                        else if (afterSeverity > beforeSeverity)
                        {
                            hasWorsening = true;
                        }

                        /*
                         * Eine geringere Häufigkeit
                         * ist eine positive Teilentwicklung.
                         *
                         * Eine größere Häufigkeit ist
                         * eine negative Teilentwicklung.
                         *
                         * Ändern sich Schweregrad und
                         * Häufigkeit in entgegengesetzte
                         * Richtungen, wird die Entwicklung
                         * als gemischt bewertet.
                         */
                        if (finding.AfterOccurrenceCount
                            < finding.BeforeOccurrenceCount)
                        {
                            hasImprovement = true;
                        }
                        else if (finding.AfterOccurrenceCount
                                 > finding.BeforeOccurrenceCount)
                        {
                            hasWorsening = true;
                        }

                        break;
                    }

                case CustomerCheckupFindingComparisonStatus.NotReevaluatable:
                    {
                        /*
                         * Ein nicht erneut überprüfbarer
                         * Handlungsbedarf darf nicht
                         * stillschweigend als behoben
                         * oder unverändert gelten.
                         */
                        return CustomerCheckupAreaComparisonStatus
                            .NotComparable;
                    }

                default:
                    {
                        /*
                         * Unbekannte zukünftige Statuswerte
                         * werden aus Sicherheitsgründen
                         * nicht als Verbesserung interpretiert.
                         */
                        return CustomerCheckupAreaComparisonStatus
                            .NotComparable;
                    }
            }
        }

        /*
         * Positive und negative Veränderungen
         * können gleichzeitig auftreten.
         *
         * Eine reine Betrachtung der Befundanzahl
         * würde diese Situation nicht zuverlässig
         * erkennen.
         *
         * Deshalb hat MixedChanges Vorrang
         * vor einer pauschalen Verbesserung
         * oder Verschlechterung.
         */
        if (hasImprovement && hasWorsening)
        {
            return CustomerCheckupAreaComparisonStatus.MixedChanges;
        }

        if (hasImprovement)
        {
            return hasActionableFindingsAfter
                ? CustomerCheckupAreaComparisonStatus
                    .ImprovedButStillNeedsAttention
                : CustomerCheckupAreaComparisonStatus.Improved;
        }

        if (hasWorsening)
        {
            return hadActionableFindingsBefore
                ? CustomerCheckupAreaComparisonStatus.Worsened
                : CustomerCheckupAreaComparisonStatus
                    .NewlyNeedsAttention;
        }

        /*
         * Es wurden keine Veränderungen erkannt.
         *
         * Nun unterscheiden wir zwischen
         * unverändert vorhandenem Handlungsbedarf
         * und einem unverändert unauffälligen Zustand.
         */
        if (hadActionableFindingsBefore
            || hasActionableFindingsAfter)
        {
            return CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention;
        }

        return CustomerCheckupAreaComparisonStatus.UnchangedHealthy;
    }

    /// <summary>
    /// Liefert die fachliche Priorität eines
    /// Handlungsbedarfs.
    ///
    /// Informationsbefunde und fehlende Angaben
    /// besitzen keine technische Fehlerpriorität.
    /// </summary>
    private static int GetSeverityRank(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Recommendation => 1,
            FindingSeverity.Warning => 2,
            FindingSeverity.Critical => 3,
            _ => 0
        };
    }
}
