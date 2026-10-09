
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Comparison;

/// <summary>
/// Bestimmt den Status eines Befundes im Vorher-/Nachher-Vergleich.
///
/// Informationsbefunde werden unabhängig vom AssessmentTarget
/// als neutral betrachtet. Das AssessmentTarget beschreibt,
/// in welche Bewertung ein Befund einfließen kann, nicht,
/// ob eine technische Maßnahme erforderlich ist.
/// </summary>
public static class CustomerCheckupFindingStatusPolicy
{
    public static CustomerCheckupFindingComparisonStatus Determine(
        IReadOnlyCollection<CheckupFinding> beforeFindings,
        IReadOnlyCollection<CheckupFinding> afterFindings,
        bool isAfterEvaluable)
    {
        ArgumentNullException.ThrowIfNull(beforeFindings);
        ArgumentNullException.ThrowIfNull(afterFindings);

        // Nur Befunde oberhalb der Informationsstufe
        // stellen einen möglichen Handlungsbedarf dar.
        var beforeActionableCount = beforeFindings.Count(
            IsActionableFinding);

        var afterActionableCount = afterFindings.Count(
            IsActionableFinding);

        // Weder vorher noch nachher bestand ein
        // handlungsrelevanter Befund.
        //
        // Das gilt beispielsweise für:
        // - Secure Boot aktiv
        // - TPM 2.0 aktiv
        // - Virenschutz aktiv
        // - SSD erkannt
        if (beforeActionableCount == 0
            && afterActionableCount == 0)
        {
            return CustomerCheckupFindingComparisonStatus
                .Informational;
        }

        // Ein tatsächlicher Handlungsbedarf ist in beiden
        // Scans nachweisbar.
        if (beforeActionableCount > 0
            && afterActionableCount > 0)
        {
            return CustomerCheckupFindingComparisonStatus
                .StillOpen;
        }

        // Vorher bestand Handlungsbedarf, nachher nicht mehr.
        //
        // Eine positive Informationsmeldung im Nachher-Scan
        // darf nicht dazu führen, dass der ursprüngliche
        // Mangel weiterhin als offen gilt.
        if (beforeActionableCount > 0)
        {
            return isAfterEvaluable
                ? CustomerCheckupFindingComparisonStatus.Resolved
                : CustomerCheckupFindingComparisonStatus.NotReevaluatable;
        }

        // Vorher lag höchstens eine neutrale Information vor,
        // nachher wird ein handlungsrelevanter Befund erkannt.
        //
        // Ohne belastbare Nachher-Auswertung ist keine
        // sichere Neueinstufung möglich.
        return isAfterEvaluable
            ? CustomerCheckupFindingComparisonStatus.NewlyDetected
            : CustomerCheckupFindingComparisonStatus.NotReevaluatable;
    }

    private static bool IsActionableFinding(
        CheckupFinding finding)
    {
        return finding.Severity
            != FindingSeverity.Information;
    }
}
