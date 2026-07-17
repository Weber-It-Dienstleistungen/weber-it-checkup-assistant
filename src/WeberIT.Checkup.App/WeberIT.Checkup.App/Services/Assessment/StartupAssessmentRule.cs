using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class StartupAssessmentRule :
    ICheckupAssessmentRule
{
    private const int ExtensiveStartupThreshold =
        15;

    private const int MultipleOptionalEntriesThreshold =
        3;

    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var findings =
            new List<CheckupFinding>();

        var startupInformation =
            checkupSession.StartupInformation;

        if (!startupInformation.HasAnalysis)
        {
            return findings;
        }

        AddAnalysisStatusFinding(
            startupInformation,
            findings);

        AddConspicuousEntriesFinding(
            startupInformation,
            findings);

        AddOptionalEntriesFinding(
            startupInformation,
            findings);

        AddExtensiveStartupFinding(
            startupInformation,
            findings);

        return findings;
    }

    private static void AddAnalysisStatusFinding(
        StartupInformation startupInformation,
        ICollection<CheckupFinding> findings)
    {
        if (!startupInformation
            .HasFailedOrIncompleteAnalysis)
        {
            return;
        }

        var description =
            startupInformation.AnalysisStatus switch
            {
                StartupAnalysisStatus.TimedOut =>
                    "Das Zeitlimit wurde erreicht. Die bis dahin "
                    + "erkannten Einträge werden angezeigt, die "
                    + "Autostartliste kann jedoch unvollständig sein.",

                StartupAnalysisStatus.PartiallyAnalyzed =>
                    "Mindestens eine unterstützte lokale "
                    + "Autostartquelle konnte nicht zuverlässig "
                    + "ausgewertet werden.",

                StartupAnalysisStatus.NotEvaluable =>
                    "Die unterstützten lokalen Autostartquellen "
                    + "konnten nicht zuverlässig ausgewertet werden.",

                _ =>
                    "Die Autostartanalyse konnte nicht vollständig "
                    + "ausgeführt werden."
            };

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.startup.analysis-incomplete",

                Title =
                    "Autostartanalyse unvollständig",

                Description =
                    description,

                Category =
                    FindingCategory.Performance,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.InformationOnly,

                CauseGroup =
                    "system.startup.data-quality"
            });
    }

    private static void AddConspicuousEntriesFinding(
        StartupInformation startupInformation,
        ICollection<CheckupFinding> findings)
    {
        var conspicuousEntries =
            startupInformation.Entries
                .Where(
                    entry =>
                        entry.State
                            == StartupEntryState.Enabled
                        && entry.Classification
                            == StartupClassification.Conspicuous)
                .ToList();

        if (conspicuousEntries.Count == 0)
        {
            return;
        }

        var description =
            conspicuousEntries.Count == 1
                ? "Ein aktiver Autostarteintrag verweist auf ein "
                  + "lokales Ziel, das nicht mehr gefunden wurde. "
                  + "Der Eintrag sollte manuell geprüft werden."
                : $"{conspicuousEntries.Count} aktive "
                  + "Autostarteinträge verweisen auf lokale Ziele, "
                  + "die nicht mehr gefunden wurden. Die Einträge "
                  + "sollten manuell geprüft werden.";

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.startup.conspicuous-entries",

                Title =
                    "Auffällige Autostarteinträge prüfen",

                Description =
                    description,

                Category =
                    FindingCategory.Performance,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.startup.invalid-targets"
            });
    }

    private static void AddOptionalEntriesFinding(
        StartupInformation startupInformation,
        ICollection<CheckupFinding> findings)
    {
        var optionalEntryCount =
            startupInformation
                .OptionalReviewEntryCount;

        if (optionalEntryCount
            < MultipleOptionalEntriesThreshold)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.startup.optional-entries",

                Title =
                    "Optionale Autostarts prüfen",

                Description =
                    $"{optionalEntryCount} aktive Autostarteinträge "
                    + "wurden als optional prüfbar eingeordnet. "
                    + "Ob sie benötigt werden, hängt von der "
                    + "tatsächlichen Nutzung der Programme ab.",

                Category =
                    FindingCategory.Performance,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.startup.background-load"
            });
    }

    private static void AddExtensiveStartupFinding(
        StartupInformation startupInformation,
        ICollection<CheckupFinding> findings)
    {
        var enabledEntryCount =
            startupInformation
                .EnabledEntryCount;

        if (enabledEntryCount
            < ExtensiveStartupThreshold)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.startup.extensive",

                Title =
                    "Umfangreichen Autostart überprüfen",

                Description =
                    $"{enabledEntryCount} aktive Autostarteinträge "
                    + "wurden erkannt. Eine hohe Anzahl ist nicht "
                    + "automatisch fehlerhaft, kann aber Anmeldung "
                    + "und Hintergrundlast beeinflussen. Die Einträge "
                    + "sollten bei Bedarf einzeln auf ihren praktischen "
                    + "Nutzen geprüft werden.",

                Category =
                    FindingCategory.Performance,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.startup.background-load"
            });
    }
}