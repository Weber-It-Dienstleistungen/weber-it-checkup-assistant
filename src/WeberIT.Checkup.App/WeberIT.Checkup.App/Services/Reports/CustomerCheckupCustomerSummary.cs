
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Erstellt eine verständliche Kundenzusammenfassung
/// auf Basis des gespeicherten Vorher-/Nachher-Vergleichs.
///
/// Scan-, Bewertungs- und Vergleichsdaten werden
/// dabei nicht verändert.
///
/// Erfolgreiche Wartungsaktionen, erledigte Aufgaben
/// und technisch nachgewiesene Verbesserungen
/// werden ausdrücklich getrennt dargestellt.
/// </summary>
public sealed class CustomerCheckupCustomerSummary
{
    public string Headline { get; private set; } =
        string.Empty;

    public string ConditionText { get; private set; } =
        string.Empty;

    public string WorkResultText { get; private set; } =
        string.Empty;

    public string RecommendationText { get; private set; } =
        string.Empty;

    public string CheckupCoverageText { get; private set; } =
        string.Empty;

    public int OpenFindingCount { get; private set; }

    public int ResolvedFindingCount { get; private set; }

    public int CompletedTaskCount { get; private set; }

    public int SuccessfulActionCount { get; private set; }

    public int FailedActionCount { get; private set; }

    public int CancelledActionCount { get; private set; }

    public bool HasOpenFindings =>
        OpenFindingCount > 0;

    public bool HasResolvedFindings =>
        ResolvedFindingCount > 0;

    public static CustomerCheckupCustomerSummary Create(
        CustomerCheckupComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var findings =
            comparison.Findings
            ?? new List<CustomerCheckupFindingComparison>();

        var tasks =
            comparison.Tasks
            ?? new List<CustomerCheckupTaskComparison>();

        var areas =
            comparison.Areas
            ?? new List<CustomerCheckupAreaComparison>();

        var summary = new CustomerCheckupCustomerSummary
        {
            OpenFindingCount = findings.Count(
                IsCurrentlyOpen),

            ResolvedFindingCount = findings.Count(finding =>
                finding.Status ==
                CustomerCheckupFindingComparisonStatus.Resolved),

            CompletedTaskCount = tasks.Count(task =>
                task.WorkStatus == CheckupTaskStatus.Completed),

            SuccessfulActionCount =
                comparison.SuccessfulActionCount,

            FailedActionCount =
                comparison.FailedActionCount,

            CancelledActionCount =
                comparison.CancelledActionCount
        };

        summary.Headline =
            BuildHeadline(
                comparison,
                findings,
                areas);

        summary.ConditionText =
            BuildConditionText(comparison);

        summary.WorkResultText =
            BuildWorkResultText(summary);

        summary.RecommendationText =
            BuildRecommendationText(findings);

        summary.CheckupCoverageText =
            BuildCoverageText(areas);

        return summary;
    }

    private static string BuildHeadline(
        CustomerCheckupComparison comparison,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings,
        IReadOnlyCollection<CustomerCheckupAreaComparison> areas)
    {
        var hasCriticalFindings = findings.Any(finding =>
            IsConfirmedOpen(finding)
            && GetFindingSeverity(finding)
                == FindingSeverity.Critical);

        var hasWarnings = findings.Any(finding =>
            IsConfirmedOpen(finding)
            && GetFindingSeverity(finding)
                == FindingSeverity.Warning);

        if (hasCriticalFindings)
        {
            return "Bei der Prüfung wurden dringende "
                   + "Auffälligkeiten festgestellt.";
        }

        if (hasWarnings)
        {
            return "Der Rechner wurde geprüft. "
                   + "Einige Punkte sollten beachtet werden.";
        }

        if (comparison.NotReevaluatableFindingCount > 0)
        {
            return "Die Prüfung wurde durchgeführt. "
                   + "Einzelne Ergebnisse konnten nicht "
                   + "abschließend bestätigt werden.";
        }

        if (comparison.StillOpenFindingCount > 0
            || comparison.NewlyDetectedFindingCount > 0)
        {
            return "Der Rechner wurde geprüft. "
                   + "Es bestehen noch Empfehlungen.";
        }

        /*
         * Wenn keine offenen Befunde vorhanden sind,
         * einzelne Prüfbereiche aber nicht verglichen
         * werden konnten, behaupten wir keine
         * uneingeschränkt vollständige Kontrolle.
         */
        if (areas.Any(area =>
                area.Status ==
                CustomerCheckupAreaComparisonStatus.NotComparable))
        {
            return "In den auswertbaren Prüfbereichen "
                   + "wurden keine offenen Probleme "
                   + "festgestellt. Einzelne Bereiche "
                   + "konnten nicht vollständig "
                   + "verglichen werden.";
        }

        return "Bei der Abschlusskontrolle wurden "
               + "keine offenen technischen Probleme festgestellt.";
    }

    private static string BuildConditionText(
        CustomerCheckupComparison comparison)
    {
        var systemScore = comparison.SystemScore;
        var hardwareScore = comparison.HardwareScore;

        var parts = new List<string>();

        /*
         * Die aktuellen Punktwerte dürfen genannt
         * werden, auch wenn ein direkter Vergleich
         * zwischen den Scans nicht möglich ist.
         *
         * Die Vergleichbarkeit entscheidet jedoch,
         * ob wir eine Verbesserung oder
         * Verschlechterung behaupten dürfen.
         */

        if (systemScore.AfterScore.HasValue)
        {
            parts.Add(
                "Der allgemeine Systemzustand erreicht "
                + $"{systemScore.AfterScore.Value} von 100 Punkten.");
        }
        else
        {
            parts.Add(
                "Für den allgemeinen Systemzustand liegt "
                + "keine vollständige Punktbewertung vor.");
        }

        if (hardwareScore.AfterScore.HasValue)
        {
            parts.Add(
                "Die Hardware erreicht "
                + $"{hardwareScore.AfterScore.Value} von 100 Punkten.");
        }
        else
        {
            parts.Add(
                "Für die Hardware liegt keine vollständige "
                + "Punktbewertung vor.");
        }

        parts.Add(
            BuildScoreChangeText(
                systemScore,
                "Die Systembewertung"));

        parts.Add(
            BuildScoreChangeText(
                hardwareScore,
                "Die Hardwarebewertung"));

        return string.Join(" ", parts);
    }

    private static string BuildScoreChangeText(
        CustomerCheckupScoreComparison score,
        string label)
    {
        /*
         * Unterschiedliche Punktzahlen sind für sich
         * genommen kein Beleg einer Verbesserung,
         * wenn die Datengrundlagen nicht vergleichbar
         * sind.
         *
         * Das gilt ebenso für identische Punktzahlen:
         * Ohne Vergleichbarkeit ist auch "unverändert"
         * keine belastbare Aussage.
         */
        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return label
                   + " kann nicht zuverlässig mit "
                   + "dem Eingangsscan verglichen werden. "
                   + "Eine Verbesserung oder "
                   + "Verschlechterung lässt sich daraus "
                   + "nicht sicher ableiten.";
        }

        var difference = score.Difference.Value;

        if (difference > 0)
        {
            return label
                   + " hat sich um "
                   + difference
                   + " Punkte verbessert.";
        }

        if (difference < 0)
        {
            return label
                   + " hat sich um "
                   + Math.Abs(difference)
                   + " Punkte verschlechtert.";
        }

        return label
               + " ist unverändert geblieben.";
    }

    private static string BuildWorkResultText(
        CustomerCheckupCustomerSummary summary)
    {
        var parts = new List<string>();

        /*
         * 1. Erledigte Aufgaben beschreiben den
         *    dokumentierten Bearbeitungsstand.
         *
         * 2. Technische Aktionen beschreiben
         *    protokollierte Ausführungsergebnisse.
         *
         * 3. Behobene Befunde setzen einen
         *    entsprechenden technischen Nachweis
         *    aus dem gespeicherten Vergleich voraus.
         *
         * Keine dieser Kategorien ersetzt die andere.
         */

        if (summary.CompletedTaskCount > 0)
        {
            parts.Add(
                summary.CompletedTaskCount == 1
                    ? "Eine Aufgabe wurde als erledigt dokumentiert."
                    : $"{summary.CompletedTaskCount} Aufgaben wurden "
                      + "als erledigt dokumentiert.");
        }
        else
        {
            parts.Add(
                "Es wurden keine Aufgaben als erledigt "
                + "dokumentiert.");
        }

        if (summary.SuccessfulActionCount > 0)
        {
            parts.Add(
                summary.SuccessfulActionCount == 1
                    ? "Eine technische Wartungsaktion "
                      + "wurde erfolgreich ausgeführt."
                    : $"{summary.SuccessfulActionCount} technische "
                      + "Wartungsaktionen wurden erfolgreich "
                      + "ausgeführt.");
        }

        /*
         * Fehlgeschlagene und abgebrochene Aktionen
         * werden nicht verschwiegen, wenn gleichzeitig
         * erfolgreiche Aktionen protokolliert sind.
         */
        if (summary.FailedActionCount > 0)
        {
            parts.Add(
                summary.FailedActionCount == 1
                    ? "Eine technische Aktion ist fehlgeschlagen."
                    : $"{summary.FailedActionCount} technische "
                      + "Aktionen sind fehlgeschlagen.");
        }

        if (summary.CancelledActionCount > 0)
        {
            parts.Add(
                summary.CancelledActionCount == 1
                    ? "Eine technische Aktion wurde abgebrochen."
                    : $"{summary.CancelledActionCount} technische "
                      + "Aktionen wurden abgebrochen.");
        }

        if (summary.ResolvedFindingCount > 0)
        {
            parts.Add(
                summary.ResolvedFindingCount == 1
                    ? "Bei der Abschlusskontrolle war eine "
                      + "zuvor festgestellte technische "
                      + "Auffälligkeit nicht mehr nachweisbar."
                    : $"Bei der Abschlusskontrolle waren "
                      + $"{summary.ResolvedFindingCount} zuvor "
                      + "festgestellte technische Auffälligkeiten "
                      + "nicht mehr nachweisbar.");
        }
        else
        {
            /*
             * Wichtiges Ergebnis der Praxistests:
             *
             * Erfolgreiche Programmaktualisierungen,
             * Bereinigungen oder Systemdateireparaturen
             * können protokolliert sein, obwohl kein
             * einzelner Bewertungsbefund als behoben
             * bestätigt werden konnte.
             *
             * Deshalb wird aus "0 behobenen Befunden"
             * ausdrücklich kein Misserfolg abgeleitet.
             */
            parts.Add(
                "Bei der Abschlusskontrolle ließ sich "
                + "keine zuvor festgestellte technische "
                + "Auffälligkeit eindeutig als behoben "
                + "bestätigen.");

            if (summary.SuccessfulActionCount > 0
                || summary.CompletedTaskCount > 0)
            {
                parts.Add(
                    "Das bedeutet nicht, dass die "
                    + "dokumentierten Wartungsarbeiten "
                    + "erfolglos waren. Ausgeführte "
                    + "Maßnahmen und bestätigte "
                    + "Fehlerbehebungen werden getrennt "
                    + "bewertet.");
            }
        }

        return string.Join(" ", parts);
    }

    private static string BuildRecommendationText(
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings)
    {
        /*
         * Nur tatsächlich erneut erkannte offene
         * Befunde werden als gesichert offen gezählt.
         *
         * Nicht erneut auswertbare Befunde bleiben
         * eine separate Kategorie.
         */
        var openFindings = findings
            .Where(IsConfirmedOpen)
            .ToList();

        var uncertainCount = findings.Count(finding =>
            finding.Status ==
            CustomerCheckupFindingComparisonStatus.NotReevaluatable);

        if (openFindings.Count == 0
            && uncertainCount == 0)
        {
            return "Aus der Abschlusskontrolle ergeben "
                   + "sich derzeit keine offenen "
                   + "technischen Befundempfehlungen.";
        }

        var urgentCount = openFindings.Count(finding =>
            GetFindingSeverity(finding)
                == FindingSeverity.Critical);

        var warningCount = openFindings.Count(finding =>
            GetFindingSeverity(finding)
                == FindingSeverity.Warning);

        var recommendationCount = openFindings.Count(finding =>
            GetFindingSeverity(finding)
                == FindingSeverity.Recommendation);

        var categorizedCount =
            urgentCount
            + warningCount
            + recommendationCount;

        var otherCount =
            openFindings.Count - categorizedCount;

        var categories = new List<string>();

        if (urgentCount > 0)
        {
            categories.Add(
                urgentCount == 1
                    ? "1 dringender Hinweis"
                    : $"{urgentCount} dringende Hinweise");
        }

        if (warningCount > 0)
        {
            categories.Add(
                warningCount == 1
                    ? "1 wichtiger Hinweis"
                    : $"{warningCount} wichtige Hinweise");
        }

        if (recommendationCount > 0)
        {
            categories.Add(
                recommendationCount == 1
                    ? "1 Empfehlung"
                    : $"{recommendationCount} Empfehlungen");
        }

        if (otherCount > 0)
        {
            categories.Add(
                otherCount == 1
                    ? "1 weiterer technischer Hinweis"
                    : $"{otherCount} weitere technische Hinweise");
        }

        var parts = new List<string>();

        if (categories.Count > 0)
        {
            parts.Add(
                "Bei der Abschlusskontrolle blieben "
                + "folgende Punkte offen: "
                + JoinNaturalLanguage(categories)
                + ".");
        }

        if (uncertainCount > 0)
        {
            parts.Add(
                uncertainCount == 1
                    ? "Zusätzlich konnte 1 Befund bei "
                      + "der Abschlusskontrolle nicht "
                      + "zuverlässig überprüft werden."
                    : $"Zusätzlich konnten {uncertainCount} "
                      + "Befunde bei der Abschlusskontrolle "
                      + "nicht zuverlässig überprüft werden.");
        }

        return string.Join(" ", parts);
    }

    private static string JoinNaturalLanguage(
        IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        if (values.Count == 2)
        {
            return values[0] + " und " + values[1];
        }

        return string.Join(
                   ", ",
                   values.Take(values.Count - 1))
               + " und "
               + values[^1];
    }

    private static string BuildCoverageText(
        IReadOnlyCollection<CustomerCheckupAreaComparison> areas)
    {
        var notComparable = areas
            .Where(area =>
                area.Status ==
                CustomerCheckupAreaComparisonStatus.NotComparable)
            .ToList();

        if (notComparable.Count == 0)
        {
            return "Die dokumentierten Prüfbereiche konnten "
                   + "im Vorher-/Nachher-Vergleich ausgewertet werden.";
        }

        return notComparable.Count == 1
            ? "Ein Prüfbereich konnte nicht vollständig "
              + "zwischen Eingangsscan und Abschlusskontrolle "
              + "verglichen werden. Das bedeutet nicht "
              + "automatisch, dass dort ein Fehler vorliegt."
            : $"{notComparable.Count} Prüfbereiche konnten "
              + "nicht vollständig zwischen Eingangsscan "
              + "und Abschlusskontrolle verglichen werden. "
              + "Das bedeutet nicht automatisch, dass dort "
              + "Fehler vorliegen.";
    }

    private static FindingSeverity? GetFindingSeverity(
        CustomerCheckupFindingComparison finding)
    {
        return finding.AfterSeverity
               ?? finding.BeforeSeverity;
    }

    private static bool IsConfirmedOpen(
        CustomerCheckupFindingComparison finding)
    {
        return finding.Status is
            CustomerCheckupFindingComparisonStatus.StillOpen
            or CustomerCheckupFindingComparisonStatus.NewlyDetected;
    }

    private static bool IsCurrentlyOpen(
        CustomerCheckupFindingComparison finding)
    {
        return IsConfirmedOpen(finding)
               || finding.Status ==
                   CustomerCheckupFindingComparisonStatus.NotReevaluatable;
    }
}
