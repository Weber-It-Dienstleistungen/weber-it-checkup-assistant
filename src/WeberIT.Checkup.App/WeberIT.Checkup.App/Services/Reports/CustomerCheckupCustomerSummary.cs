
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Erstellt eine verständliche Kundenzusammenfassung
/// auf Basis des gespeicherten Vorher-/Nachher-Vergleichs.
///
/// Die Klasse verändert keine Scan- oder Bewertungsdaten.
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
            OpenFindingCount = findings.Count(finding =>
                finding.Status is
                    CustomerCheckupFindingComparisonStatus.StillOpen
                    or CustomerCheckupFindingComparisonStatus.NewlyDetected
                    or CustomerCheckupFindingComparisonStatus
                        .NotReevaluatable),

            ResolvedFindingCount = findings.Count(finding =>
                finding.Status ==
                CustomerCheckupFindingComparisonStatus.Resolved),

            CompletedTaskCount = tasks.Count(task =>
                task.WorkStatus == CheckupTaskStatus.Completed),

            SuccessfulActionCount =
                comparison.SuccessfulActionCount
        };

        summary.Headline =
            BuildHeadline(comparison, findings);

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
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings)
    {
        var hasCriticalFindings = findings.Any(finding =>
            IsCurrentlyOpen(finding)
            && (finding.AfterSeverity ?? finding.BeforeSeverity)
                == FindingSeverity.Critical);

        var hasWarnings = findings.Any(finding =>
            IsCurrentlyOpen(finding)
            && (finding.AfterSeverity ?? finding.BeforeSeverity)
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

        return "Bei der Abschlusskontrolle wurden "
               + "keine offenen technischen Probleme festgestellt.";
    }

    private static string BuildConditionText(
        CustomerCheckupComparison comparison)
    {
        var systemScore = comparison.SystemScore;
        var hardwareScore = comparison.HardwareScore;

        var parts = new List<string>();

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

        if (systemScore.IsComparable)
        {
            parts.Add(
                CustomerCheckupReportWording
                    .GetScoreChangeText(systemScore));
        }
        else
        {
            parts.Add(
                "Eine zuverlässige Aussage über eine "
                + "Verbesserung oder Verschlechterung "
                + "ist anhand der beiden Scans nicht möglich.");
        }

        return string.Join(" ", parts);
    }

    private static string BuildWorkResultText(
        CustomerCheckupCustomerSummary summary)
    {
        var parts = new List<string>();

        if (summary.CompletedTaskCount > 0)
        {
            parts.Add(
                summary.CompletedTaskCount == 1
                    ? "Eine Aufgabe wurde als abgeschlossen dokumentiert."
                    : $"{summary.CompletedTaskCount} Aufgaben wurden "
                      + "als abgeschlossen dokumentiert.");
        }
        else
        {
            parts.Add(
                "Es wurden keine Aufgaben als abgeschlossen "
                + "dokumentiert.");
        }

        if (summary.SuccessfulActionCount > 0)
        {
            parts.Add(
                summary.SuccessfulActionCount == 1
                    ? "Eine technische Aktion wurde erfolgreich ausgeführt."
                    : $"{summary.SuccessfulActionCount} technische "
                      + "Aktionen wurden erfolgreich ausgeführt.");
        }

        if (summary.ResolvedFindingCount > 0)
        {
            parts.Add(
                summary.ResolvedFindingCount == 1
                    ? "Ein zuvor festgestellter technischer Befund "
                      + "war nach der Bearbeitung nicht mehr vorhanden."
                    : $"{summary.ResolvedFindingCount} zuvor "
                      + "festgestellte technische Befunde waren "
                      + "nach der Bearbeitung nicht mehr vorhanden.");
        }
        else
        {
            parts.Add(
                "Im Vorher-/Nachher-Vergleich wurde kein "
                + "behobener technischer Befund festgestellt.");
        }

        return string.Join(" ", parts);
    }

    private static string BuildRecommendationText(
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings)
    {
        var openFindings = findings
            .Where(IsCurrentlyOpen)
            .ToList();

        if (openFindings.Count == 0)
        {
            return "Aus dem technischen Vergleich ergeben "
                   + "sich derzeit keine offenen Befundempfehlungen.";
        }

        var urgentCount = openFindings.Count(finding =>
            (finding.AfterSeverity ?? finding.BeforeSeverity)
                == FindingSeverity.Critical);

        var warningCount = openFindings.Count(finding =>
            (finding.AfterSeverity ?? finding.BeforeSeverity)
                == FindingSeverity.Warning);

        var recommendationCount = openFindings.Count(finding =>
            (finding.AfterSeverity ?? finding.BeforeSeverity)
                == FindingSeverity.Recommendation);

        var uncertainCount = openFindings.Count(finding =>
            finding.Status ==
                CustomerCheckupFindingComparisonStatus.NotReevaluatable);

        var parts = new List<string>();

        if (urgentCount > 0)
        {
            parts.Add(
                urgentCount == 1
                    ? "Ein Punkt sollte dringend überprüft werden."
                    : $"{urgentCount} Punkte sollten dringend "
                      + "überprüft werden.");
        }

        if (warningCount > 0)
        {
            parts.Add(
                warningCount == 1
                    ? "Ein weiterer Punkt ist als wichtig eingestuft."
                    : $"{warningCount} Punkte sind als wichtig eingestuft.");
        }

        if (recommendationCount > 0)
        {
            parts.Add(
                recommendationCount == 1
                    ? "Eine weitere Verbesserung wird empfohlen."
                    : $"{recommendationCount} weitere Verbesserungen "
                      + "werden empfohlen.");
        }

        if (uncertainCount > 0)
        {
            parts.Add(
                "Bei einzelnen Punkten ist eine erneute "
                + "technische Kontrolle sinnvoll.");
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Es bestehen noch technische Hinweise, "
              + "die im weiteren Vorgehen berücksichtigt "
              + "werden sollten.";
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
              + "miteinander verglichen werden. Das bedeutet "
              + "nicht automatisch, dass dort ein Fehler vorliegt."
            : $"{notComparable.Count} Prüfbereiche konnten "
              + "nicht vollständig miteinander verglichen werden. "
              + "Das bedeutet nicht automatisch, dass dort "
              + "Fehler vorliegen.";
    }

    private static bool IsCurrentlyOpen(
        CustomerCheckupFindingComparison finding)
    {
        return finding.Status is
            CustomerCheckupFindingComparisonStatus.StillOpen
            or CustomerCheckupFindingComparisonStatus.NewlyDetected
            or CustomerCheckupFindingComparisonStatus.NotReevaluatable;
    }
}
