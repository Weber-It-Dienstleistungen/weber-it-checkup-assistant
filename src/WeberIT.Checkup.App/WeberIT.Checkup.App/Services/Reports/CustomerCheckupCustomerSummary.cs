
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

        var actions =
            comparison.Actions
            ?? new List<CustomerCheckupActionSummary>();

        var summary = new CustomerCheckupCustomerSummary
        {
            OpenFindingCount = findings.Count(
                IsCurrentlyOpen),

            ResolvedFindingCount = findings.Count(finding =>
                finding.Status ==
                CustomerCheckupFindingComparisonStatus.Resolved),

            CompletedTaskCount = tasks.Count(task =>
                task.WorkStatus == CheckupTaskStatus.Completed),

            SuccessfulActionCount = actions.Count(action =>
                action.Status == CheckupTaskActionStatus.Successful),

            FailedActionCount = actions.Count(action =>
                action.Status == CheckupTaskActionStatus.Failed),

            CancelledActionCount = actions.Count(action =>
                action.Status == CheckupTaskActionStatus.Cancelled)
        };

        summary.Headline =
            BuildHeadline(
                comparison,
                findings,
                areas);

        summary.ConditionText =
            BuildConditionText(comparison);

        summary.WorkResultText =
            BuildWorkResultText(
                summary,
                actions);

        summary.RecommendationText =
            BuildRecommendationText(findings);

        summary.CheckupCoverageText =
            BuildCoverageText(areas);

        return summary;
    }

    // =====================================================
    // SCHRITT 54.1 – KUNDENVERSTÄNDLICHES GESAMTERGEBNIS
    // =====================================================

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
            return "Bei der Abschlusskontrolle wurden "
                   + "noch dringende Probleme festgestellt.";
        }

        if (hasWarnings)
        {
            return "Der Rechner wurde überprüft. "
                   + "Einige wichtige Punkte sind "
                   + "noch offen.";
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
            return "Der Rechner wurde überprüft. "
                   + "Einige Empfehlungen sind "
                   + "noch offen.";
        }

        /*
         * Keine uneingeschränkt positive Aussage,
         * wenn einzelne technische Bereiche
         * nicht zuverlässig verglichen
         * werden konnten.
         */
        if (areas.Any(area =>
                area.Status ==
                CustomerCheckupAreaComparisonStatus.NotComparable))
        {
            return "In den überprüfbaren Bereichen "
                   + "wurden keine offenen Probleme "
                   + "festgestellt. Einzelne Prüfungen "
                   + "konnten jedoch nicht vollständig "
                   + "abgeschlossen werden.";
        }

        return "Bei der Abschlusskontrolle wurden "
               + "keine offenen technischen Probleme festgestellt.";
    }

    // =====================================================
    // SYSTEMZUSTAND UND HARDWARE
    // =====================================================

    private static string BuildConditionText(
        CustomerCheckupComparison comparison)
    {
        var systemScore = comparison.SystemScore;
        var hardwareScore = comparison.HardwareScore;

        var parts = new List<string>();

        /*
         * Die aktuellen Punktwerte dürfen genannt
         * werden, auch wenn kein direkter Vergleich
         * mit dem Eingangsscan möglich ist.
         *
         * Eine Veränderung wird aber nur dann
         * beschrieben, wenn die Vergleichslogik
         * sie ausdrücklich bestätigt.
         *
         * Der Text richtet sich an Privatkunden.
         * Fachbegriffe wie "Scoring-Version",
         * "Bewertungsbereiche" und
         * "Datengrundlagenkompatibilität"
         * gehören nicht in diesen Abschnitt.
         */

        if (systemScore.AfterScore.HasValue)
        {
            parts.Add(
                "Der allgemeine Systemzustand erreicht "
                + $"{systemScore.AfterScore.Value} "
                + "von 100 Punkten.");
        }
        else
        {
            parts.Add(
                "Für den allgemeinen Systemzustand "
                + "liegt keine vollständige "
                + "Punktbewertung vor.");
        }

        if (hardwareScore.AfterScore.HasValue)
        {
            parts.Add(
                "Die Hardware erreicht "
                + $"{hardwareScore.AfterScore.Value} "
                + "von 100 Punkten.");
        }
        else
        {
            parts.Add(
                "Für die Hardware liegt keine "
                + "vollständige Punktbewertung vor.");
        }

        parts.Add(
            BuildScoreChangeText(
                systemScore,
                "Beim Systemzustand"));

        parts.Add(
            BuildScoreChangeText(
                hardwareScore,
                "Bei der Hardware"));

        /*
         * Eine gute Punktzahl bedeutet nicht,
         * dass alle technischen Hinweise
         * automatisch erledigt sind.
         */
        parts.Add(
            "Die Punktzahlen sind nur ein Teil "
            + "der Prüfung. Noch offene Hinweise "
            + "finden Sie unter unseren Empfehlungen.");

        return string.Join(" ", parts);
    }

    private static string BuildScoreChangeText(
        CustomerCheckupScoreComparison score,
        string label)
    {
        /*
         * Ohne belastbaren Vorher-/Nachher-
         * Vergleich dürfen wir keine
         * Verbesserung, Verschlechterung
         * oder unveränderte Entwicklung
         * behaupten.
         */
        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return label
                   + " lässt sich die Veränderung "
                   + "gegenüber dem Eingangsscan "
                   + "nicht zuverlässig bestimmen.";
        }

        var difference = score.Difference.Value;

        if (difference > 0)
        {
            return label
                   + " ist die Punktzahl gegenüber "
                   + "dem Eingangsscan um "
                   + difference
                   + " Punkte gestiegen.";
        }

        if (difference < 0)
        {
            return label
                   + " ist die Punktzahl gegenüber "
                   + "dem Eingangsscan um "
                   + Math.Abs(difference)
                   + " Punkte gesunken.";
        }

        return label
               + " ist die Punktzahl gegenüber "
               + "dem Eingangsscan unverändert geblieben.";
    }

    // =====================================================
    // WAS WURDE TATSÄCHLICH ERREICHT?
    // =====================================================

    private static string BuildWorkResultText(
        CustomerCheckupCustomerSummary summary,
        IReadOnlyCollection<CustomerCheckupActionSummary> actions)
    {
        var parts = new List<string>();

        /*
         * Fachliche Trennung:
         *
         * 1. Erledigte Aufgaben dokumentieren
         *    den Bearbeitungsstand.
         *
         * 2. Erfolgreiche Aktionen dokumentieren
         *    die tatsächlichen Ausführungsergebnisse.
         *
         * 3. Behobene Befunde setzen einen
         *    technischen Nachweis aus dem
         *    Vorher-/Nachher-Vergleich voraus.
         *
         * Die drei Kategorien dürfen nicht
         * miteinander verwechselt werden.
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
            var successfulActions =
                actions
                    .Where(action =>
                        action.Status ==
                        CheckupTaskActionStatus.Successful)
                    .ToList();

            var actionOverview =
                BuildSuccessfulActionOverview(
                    successfulActions);

            var actionCountText =
                summary.SuccessfulActionCount == 1
                    ? "Eine Wartungsaktion wurde erfolgreich "
                      + "protokolliert"
                    : $"{summary.SuccessfulActionCount} "
                      + "Wartungsaktionen wurden erfolgreich "
                      + "protokolliert";

            parts.Add(
                string.IsNullOrWhiteSpace(actionOverview)
                    ? actionCountText + "."
                    : actionCountText
                      + ": "
                      + actionOverview
                      + ".");
        }

        /*
         * Fehlgeschlagene und abgebrochene Aktionen
         * dürfen nicht durch positive Ergebnisse
         * verdeckt werden.
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

        /*
         * Eine durchgeführte Wartungsaktion
         * ist keine automatisch bestätigte
         * Reparatur.
         *
         * Hier verzichten wir bewusst auf
         * den Begriff "Bewertungsbefund".
         */

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
        else if (summary.SuccessfulActionCount > 0)
        {
            parts.Add(
                "Ob dadurch auch zuvor festgestellte "
                + "Probleme behoben wurden, konnte "
                + "bei der Abschlusskontrolle nicht "
                + "eindeutig bestätigt werden. "
                + "Die erfolgreich ausgeführten "
                + "Wartungsarbeiten sind unabhängig "
                + "davon dokumentiert.");
        }
        else
        {
            parts.Add(
                "Bei der Abschlusskontrolle ließ sich "
                + "keine zuvor festgestellte technische "
                + "Auffälligkeit eindeutig als behoben "
                + "bestätigen.");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Verdichtet erfolgreiche Wartungsaktionen
    /// zu einer kurzen kundenverständlichen
    /// Darstellung.
    ///
    /// Ausschließlich Aktionen mit dem Status
    /// Successful werden berücksichtigt.
    ///
    /// Mehrere erfolgreiche Ausführungen
    /// derselben Aktion werden zusammengefasst,
    /// jedoch nicht zu einer einzigen
    /// Ausführung umgedeutet.
    ///
    /// Die Darstellung wird auf höchstens drei
    /// unterschiedliche Maßnahmenarten begrenzt.
    /// Weitere Aktionen bleiben im technischen
    /// Anhang vollständig dokumentiert.
    /// </summary>
    private static string BuildSuccessfulActionOverview(
        IReadOnlyCollection<CustomerCheckupActionSummary> actions)
    {
        if (actions.Count == 0)
        {
            return string.Empty;
        }

        var groupedActions =
            actions
                .OrderBy(action =>
                    action.StartedAt ?? DateTimeOffset.MaxValue)
                .Select(GetCustomerFriendlyActionName)
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name))
                .GroupBy(
                    name => name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Name = group.Key,
                    Count = group.Count()
                })
                .ToList();

        if (groupedActions.Count == 0)
        {
            return string.Empty;
        }

        const int MaximumDisplayedActionTypes = 3;

        var descriptions =
            groupedActions
                .Take(MaximumDisplayedActionTypes)
                .Select(group =>
                    group.Count == 1
                        ? group.Name
                        : $"{group.Name} "
                          + $"({group.Count} Vorgänge)")
                .ToList();

        var remainingActionCount =
            groupedActions
                .Skip(MaximumDisplayedActionTypes)
                .Sum(group => group.Count);

        if (remainingActionCount > 0)
        {
            descriptions.Add(
                remainingActionCount == 1
                    ? "eine weitere Maßnahme"
                    : $"{remainingActionCount} weitere Maßnahmen");
        }

        return JoinNaturalLanguage(descriptions);
    }

    /// <summary>
    /// Übersetzt bekannte technische Aktionscodes
    /// in kurze, verständliche Tätigkeitsnamen.
    ///
    /// Die Zuordnung erfolgt über stabile
    /// Aktionscodes und nicht über eine
    /// unsichere Textanalyse der Ergebnisse.
    ///
    /// Insbesondere behauptet die Bezeichnung
    /// einer Systemdateiprüfung nicht, dass
    /// dabei tatsächlich Dateien repariert
    /// wurden. Das konkrete Ergebnis steht
    /// weiterhin in der Aktionsdokumentation.
    ///
    /// Neue oder unbekannte Aktionen behalten
    /// ihren dokumentierten Aktionsnamen.
    /// </summary>
    private static string GetCustomerFriendlyActionName(
        CustomerCheckupActionSummary action)
    {
        var actionCode =
            action.ActionCode?.Trim()
            ?? string.Empty;

        if (actionCode.Equals(
                "action.program-updates.selected-upgrades",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Programmaktualisierungen";
        }

        if (actionCode.Equals(
                "action.cleanup.selected-safe-categories",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Bereinigung ausgewählter Dateien";
        }

        if (actionCode.Equals(
                "action.maintenance.system-file-check",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Prüfung der Windows-Systemdateien";
        }

        if (actionCode.Equals(
                "action.maintenance.windows-image-repair",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Wartung des Windows-Komponentenspeichers";
        }

        if (!string.IsNullOrWhiteSpace(action.ActionTitle))
        {
            return action.ActionTitle.Trim();
        }

        return "weitere technische Wartung";
    }

    // =====================================================
    // EMPFEHLUNGEN UND OFFENE PUNKTE
    // =====================================================

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
                "Nach der Abschlusskontrolle "
                + "sind noch folgende Punkte offen: "
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

    // =====================================================
    // GEMEINSAME TEXTHILFSMETHODEN
    // =====================================================

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
            ? "Ein Prüfbereich konnte zwischen "
              + "Eingangskontrolle und Abschlusskontrolle "
              + "nicht vollständig verglichen werden. "
              + "Das bedeutet nicht automatisch, "
              + "dass dort ein Problem besteht."
            : $"{notComparable.Count} Prüfbereiche konnten "
              + "zwischen Eingangskontrolle und "
              + "Abschlusskontrolle nicht vollständig "
              + "verglichen werden. Das bedeutet nicht "
              + "automatisch, dass dort Probleme bestehen.";
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
