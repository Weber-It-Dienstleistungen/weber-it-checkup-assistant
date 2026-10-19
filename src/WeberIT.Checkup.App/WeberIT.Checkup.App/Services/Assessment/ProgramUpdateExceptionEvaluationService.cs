
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Assessment;

/// <summary>
/// Ermittelt, wie viele der tatsächlich erkannten
/// Programmupdates durch eine dokumentierte
/// Technikerentscheidung zurückgestellt wurden.
///
/// Der technische Scan und dessen Bewertung
/// werden dabei niemals verändert.
/// </summary>
public static class ProgramUpdateExceptionEvaluationService
{
    public static ProgramUpdateExceptionEvaluation Evaluate(
        ProgramUpdateInformation information,
        CustomerCheckupVisit visit)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(visit);

        var documentedExceptions =
            visit.ProgramUpdateExceptions
                .Where(exception =>
                    exception is not null
                    && exception.CheckupVisitId == visit.Id)
                .ToList();

        var hasReliableAnalysis =
            information.IsWingetAvailable == true
            && information.IsAnalysisPerformed
            && information.IsAnalysisSuccessful
            && information.AvailableUpdateCount >= 0;

        if (!hasReliableAnalysis)
        {
            return new ProgramUpdateExceptionEvaluation
            {
                IsAnalysisReliable = false,

                DocumentedExceptionCount =
                    documentedExceptions.Count,

                IsResultTruncated =
                    information.IsResultTruncated
            };
        }

        var detectedCount =
            information.AvailableUpdateCount;

        /*
         * Die WinGet-Ergebnisse werden anhand ihrer
         * vollständigen Paketidentität gruppiert.
         *
         * Dadurch kann ein versehentlich mehrfach
         * enthaltener Eintrag nicht mehrfach als
         * zurückgestelltes Update zählen.
         */
        var uniqueUpdates =
            information.AvailableUpdates
                .Where(update =>
                    update is not null
                    && !string.IsNullOrWhiteSpace(
                        update.PackageId)
                    && !string.IsNullOrWhiteSpace(
                        update.Source)
                    && !string.IsNullOrWhiteSpace(
                        update.InstalledVersion)
                    && !string.IsNullOrWhiteSpace(
                        update.AvailableVersion))
                .GroupBy(
                    BuildUpdateIdentity,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

        /*
         * Eine Ausnahme wird ausschließlich dann
         * berücksichtigt, wenn sie zur aktuellen
         * Vorgangs-ID und exakt zu den gespeicherten
         * Versionsständen passt.
         */
        var applicableExceptionCount =
            uniqueUpdates.Count(update =>
                documentedExceptions.Any(
                    exception =>
                        exception.AppliesTo(
                            visit.Id,
                            update)));

        /*
         * Die Zahl begründeter Ausnahmen darf
         * niemals größer sein als die Gesamtzahl
         * der zuverlässig erkannten Updates.
         */
        applicableExceptionCount =
            Math.Min(
                detectedCount,
                applicableExceptionCount);

        var withoutExceptionCount =
            Math.Max(
                0,
                detectedCount - applicableExceptionCount);

        return new ProgramUpdateExceptionEvaluation
        {
            IsAnalysisReliable = true,

            DetectedUpdateCount =
                detectedCount,

            ApplicableExceptionCount =
                applicableExceptionCount,

            WithoutExceptionCount =
                withoutExceptionCount,

            DocumentedExceptionCount =
                documentedExceptions.Count,

            IsResultTruncated =
                information.IsResultTruncated
        };
    }

    private static string BuildUpdateIdentity(
        AvailableProgramUpdate update)
    {
        return string.Join(
            "\u001F",
            update.PackageId.Trim(),
            update.Source.Trim(),
            update.InstalledVersion.Trim(),
            update.AvailableVersion.Trim());
    }
}

/// <summary>
/// Unveränderliche Auswertung des Verhältnisses
/// zwischen erkannten Programmaktualisierungen
/// und dokumentierten Technikerentscheidungen.
///
/// Ausnahmen bedeuten nicht, dass die
/// betreffenden Updates installiert wurden.
/// </summary>
public sealed class ProgramUpdateExceptionEvaluation
{
    public bool IsAnalysisReliable { get; init; }

    public int? DetectedUpdateCount { get; init; }

    public int? ApplicableExceptionCount { get; init; }

    public int? WithoutExceptionCount { get; init; }

    public int DocumentedExceptionCount { get; init; }

    public bool IsResultTruncated { get; init; }

    public string SummaryText
    {
        get
        {
            if (!IsAnalysisReliable
                || !DetectedUpdateCount.HasValue
                || !ApplicableExceptionCount.HasValue
                || !WithoutExceptionCount.HasValue)
            {
                return "Die Programmaktualisierungen "
                       + "konnten nicht zuverlässig "
                       + "ausgewertet werden. "
                       + "Dokumentierte Ausnahmen "
                       + "bleiben erhalten, können "
                       + "aber nicht mit einem "
                       + "aktuellen Scan abgeglichen werden.";
            }

            var summary =
                $"{DetectedUpdateCount.Value} "
                + "Programmaktualisierungen erkannt · "
                + $"{ApplicableExceptionCount.Value} "
                + "begründet zurückgestellt · "
                + $"{WithoutExceptionCount.Value} "
                + "ohne dokumentierte Ausnahme.";

            if (IsResultTruncated)
            {
                summary +=
                    " Die gespeicherte Programmliste "
                    + "ist unvollständig; nicht enthaltene "
                    + "Pakete konnten nicht einzeln "
                    + "abgeglichen werden.";
            }

            return summary;
        }
    }
}
