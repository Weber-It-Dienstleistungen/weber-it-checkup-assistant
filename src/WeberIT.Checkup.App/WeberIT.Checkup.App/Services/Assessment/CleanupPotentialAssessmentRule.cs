using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class CleanupPotentialAssessmentRule :
    ICheckupAssessmentRule
{
    private const ulong FiveHundredMegabytes =
        500UL * 1024UL * 1024UL;

    private const ulong OneGigabyte =
        1024UL * 1024UL * 1024UL;

    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        return Evaluate(checkupSession,
            checkupSession.CleanupPotentialInformation.SafePotentialBytes);
    }

    internal IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession,
        ulong scoreRelevantSafePotentialBytes)
    {
        var findings =
            new List<CheckupFinding>();

        var cleanupInformation =
            checkupSession.CleanupPotentialInformation;

        if (!cleanupInformation.HasAnalysis)
        {
            return findings;
        }

        AddAnalysisStatusFinding(
            cleanupInformation,
            findings);

        var effectiveBytes = Math.Min(
            scoreRelevantSafePotentialBytes,
            cleanupInformation.SafePotentialBytes);

        AddSafePotentialFinding(
            checkupSession,
            effectiveBytes,
            findings);

        if (effectiveBytes < cleanupInformation.SafePotentialBytes)
        {
            findings.Add(new CheckupFinding
            {
                Code = "system.cleanup.work-completed",
                Title = "Bereinigung nachvollziehbar abgeschlossen",
                Description =
                    $"Gemessener Restbestand: {FormatBytes(cleanupInformation.SafePotentialBytes)}. "
                    + $"Davon weiterhin bewertungsrelevant: {FormatBytes(effectiveBytes)}. "
                    + "Vollstaendig bearbeitete Kategorien werden in diesem "
                    + "Kundencheckup nicht erneut als Bereinigungsbedarf gewertet. "
                    + "Dateigroessen und freier Speicher bleiben unveraendert dokumentiert.",
                Category = FindingCategory.Storage,
                Severity = FindingSeverity.Information,
                AssessmentTarget = FindingAssessmentTarget.InformationOnly,
                CauseGroup = "system.cleanup.work-completed"
            });
        }

        AddManualReviewFinding(
            cleanupInformation,
            findings);

        return findings;
    }

    private static void AddAnalysisStatusFinding(
        CleanupPotentialInformation cleanupInformation,
        ICollection<CheckupFinding> findings)
    {
        if (cleanupInformation.AnalysisStatus
            != CleanupMeasurementStatus.TimedOut)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.cleanup.analysis-incomplete",

                Title =
                    "Bereinigungsanalyse unvollständig",

                Description =
                    "Das Zeitlimit wurde erreicht. "
                    + "Unvollständige Größenwerte werden "
                    + "angezeigt, fließen aber nicht in die "
                    + "zusammengefassten Potenzialwerte ein.",

                Category =
                    FindingCategory.Storage,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.InformationOnly,

                CauseGroup =
                    "system.cleanup.data-quality"
            });
    }

    private static void AddSafePotentialFinding(
        CheckupSession checkupSession,
        ulong safePotentialBytes,
        ICollection<CheckupFinding> findings)
    {

        if (safePotentialBytes
            < FiveHundredMegabytes)
        {
            return;
        }

        var systemVolume =
            checkupSession
                .StorageInformation
                .Volumes
                .FirstOrDefault(
                    volume =>
                        volume.IsSystemVolume
                        && volume.IsReady);

        if (systemVolume is not null
            && IsSystemVolumeLow(
                systemVolume))
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.cleanup.potential-with-low-system-space",

                    Title =
                        "Bereinigungspotenzial bei knappem Systemlaufwerk",

                    Description =
                        $"Auf dem Systemlaufwerk sind weiterhin "
                        + $"{FormatBytes(safePotentialBytes)} "
                        + "voraussichtlich unkritisches "
                        + "Bereinigungspotenzial bewertungsrelevant. "
                        + "Da der freie Speicher knapp ist, sollte "
                        + "eine kontrollierte Bereinigung geprüft werden.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.storage.system-volume-capacity"
                });

            return;
        }

        if (safePotentialBytes
            < OneGigabyte)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.cleanup.large-safe-potential",

                Title =
                    "Größeres Bereinigungspotenzial erkannt",

                Description =
                    $"{FormatBytes(safePotentialBytes)} "
                    + "voraussichtlich unkritisches "
                    + "Bereinigungspotenzial sind bewertungsrelevant. "
                    + "Die Kategorien sollten vor einer "
                    + "späteren Bereinigung einzeln geprüft werden.",

                Category =
                    FindingCategory.Storage,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.cleanup.safe-potential"
            });
    }

    private static void AddManualReviewFinding(
        CleanupPotentialInformation cleanupInformation,
        ICollection<CheckupFinding> findings)
    {
        var manualReviewBytes =
            cleanupInformation
                .ManualReviewPotentialBytes;

        if (manualReviewBytes
            < OneGigabyte)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.cleanup.manual-review-potential",

                Title =
                    "Größere Speicherbereiche manuell prüfen",

                Description =
                    $"{FormatBytes(manualReviewBytes)} wurden in "
                    + "vollständig gemessenen Kategorien erkannt, "
                    + "die vor einer Bereinigung manuell geprüft "
                    + "werden müssen. Dazu können beispielsweise "
                    + "Papierkorbinhalte, Update-Dateien oder "
                    + "Diagnosedaten gehören.",

                Category =
                    FindingCategory.Storage,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.InformationOnly,

                CauseGroup =
                    "system.cleanup.manual-review"
            });
    }

    private static bool IsSystemVolumeLow(
        VolumeInformation systemVolume)
    {
        const ulong TwentyGigabytes =
            20UL * 1024UL * 1024UL * 1024UL;

        var freeSpacePercent =
            systemVolume.FreeSpacePercent;

        return !systemVolume.FreeSpaceBytes.HasValue
               || systemVolume.FreeSpaceBytes.Value
                   < TwentyGigabytes
               || freeSpacePercent.HasValue
               && freeSpacePercent.Value < 10d;
    }

    private static string FormatBytes(
        ulong sizeBytes)
    {
        const double OneMegabyte =
            1024d * 1024d;

        const double OneGigabyte =
            OneMegabyte * 1024d;

        if (sizeBytes >= OneGigabyte)
        {
            return $"{sizeBytes / OneGigabyte:0.##} GB";
        }

        return $"{sizeBytes / OneMegabyte:0.##} MB";
    }
}