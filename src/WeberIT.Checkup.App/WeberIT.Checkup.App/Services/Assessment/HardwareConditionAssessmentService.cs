using System.Globalization;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class HardwareConditionAssessmentService :
    IHardwareConditionAssessmentService
{
    private const int MinimumAvailableWeightForScore =
        55;

    private static readonly IReadOnlyList<HardwareAssessmentArea>
        AssessmentAreas =
        [
            new(
                "Arbeitsspeicher",
                25,
                IsMemoryInformationAvailable,
                finding =>
                    finding.Code.StartsWith(
                        "hardware.memory.",
                        StringComparison.OrdinalIgnoreCase)),

            new(
                "Physischer Datenträgerzustand",
                35,
                IsPhysicalStorageHealthAvailable,
                finding =>
                    finding.Code.StartsWith(
                        "hardware.storage.health-",
                        StringComparison.OrdinalIgnoreCase)),

            new(
                "Datenträgertechnik",
                15,
                IsStorageTechnologyAvailable,
                finding =>
                    finding.Code
                        is "hardware.storage.relevant-hdd"
                        or "hardware.storage.nvme-ssd"
                        or "hardware.storage.ssd"),

            new(
                "TPM-Hardware",
                10,
                IsTpmCapabilityAvailable,
                finding =>
                    finding.Code.StartsWith(
                        "hardware.tpm.",
                        StringComparison.OrdinalIgnoreCase)),

            new(
                "Prozessor und Plattform",
                15,
                IsPlatformCapabilityAvailable,
                finding =>
                    finding.Code.StartsWith(
                        "hardware.platform.",
                        StringComparison.OrdinalIgnoreCase))
        ];

    public (
        ConditionAssessment Condition,
        HardwarePlanningHorizon PlanningHorizon,
        string PlanningSummary
    ) Assess(
        CheckupSession checkupSession,
        IReadOnlyCollection<CheckupFinding> findings)
    {
        var availableAreas =
            AssessmentAreas
                .Where(
                    area =>
                        area.IsAvailable(checkupSession))
                .ToList();

        var availableWeight =
            availableAreas.Sum(
                area => area.Weight);

        var dataQuality =
            DetermineDataQuality(
                availableAreas.Count);

        if (availableWeight
            < MinimumAvailableWeightForScore)
        {
            return (
                new ConditionAssessment
                {
                    Score = null,

                    Rating =
                        ConditionRating.NotAvailable,

                    DataQuality =
                        dataQuality,

                    Summary =
                        "Für eine belastbare Bewertung des "
                        + "Hardwarezustands stehen nicht genügend "
                        + "auswertbare Hardwarebereiche zur Verfügung.",

                    EvaluatedAreaCount =
                        AssessmentAreas.Count,

                    AvailableAreaCount =
                        availableAreas.Count
                },

                HardwarePlanningHorizon.NotAvailable,

                "Die vorhandenen Hardwareinformationen reichen "
                + "nicht für eine belastbare Planungsaussage aus."
            );
        }

        double weightedScoreTotal =
            0;

        foreach (var area in availableAreas)
        {
            var areaFindings =
                findings
                    .Where(
                        finding =>
                            finding.AssessmentTarget
                                == FindingAssessmentTarget
                                    .HardwareCondition
                            && area.MatchesFinding(finding))
                    .ToList();

            var areaScore =
                CalculateAreaScore(
                    areaFindings);

            weightedScoreTotal +=
                areaScore * area.Weight;
        }

        var normalizedScore =
            weightedScoreTotal
            / availableWeight;

        var score =
            RoundToFive(
                normalizedScore);

        var hasCriticalPhysicalStorageFinding =
            findings.Any(
                finding =>
                    finding.AssessmentTarget
                        == FindingAssessmentTarget.HardwareCondition
                    && finding.Code.Equals(
                        "hardware.storage.health-critical",
                        StringComparison.OrdinalIgnoreCase));

        if (hasCriticalPhysicalStorageFinding)
        {
            score =
                Math.Min(
                    score,
                    30);
        }
        else if (findings.Any(
                     finding =>
                         finding.AssessmentTarget
                             == FindingAssessmentTarget.HardwareCondition
                         && finding.Severity
                             == FindingSeverity.Critical))
        {
            score =
                Math.Min(
                    score,
                    40);
        }

        var rating =
            DetermineRating(score);

        var condition =
            new ConditionAssessment
            {
                Score =
                    score,

                Rating =
                    rating,

                DataQuality =
                    dataQuality,

                Summary =
                    BuildConditionSummary(
                        rating,
                        dataQuality),

                EvaluatedAreaCount =
                    AssessmentAreas.Count,

                AvailableAreaCount =
                    availableAreas.Count
            };

        var planning =
            DeterminePlanning(
                findings,
                condition,
                hasCriticalPhysicalStorageFinding);

        return (
            condition,
            planning.Horizon,
            planning.Summary
        );
    }

    private static int CalculateAreaScore(
        IReadOnlyCollection<CheckupFinding> findings)
    {
        var relevantCauseGroups =
            findings
                .Where(
                    finding =>
                        finding.Severity
                            is FindingSeverity.Recommendation
                            or FindingSeverity.Warning
                            or FindingSeverity.Critical)
                .GroupBy(
                    finding =>
                        string.IsNullOrWhiteSpace(
                            finding.CauseGroup)
                                ? finding.Code
                                : finding.CauseGroup,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                finding =>
                                    GetSeverityPriority(
                                        finding.Severity))
                            .First())
                .ToList();

        if (relevantCauseGroups.Count == 0)
        {
            return 100;
        }

        var strongestSeverity =
            relevantCauseGroups
                .Max(
                    finding =>
                        GetSeverityPriority(
                            finding.Severity));

        var baseScore =
            strongestSeverity switch
            {
                3 => 15,
                2 => 55,
                1 => 80,
                _ => 100
            };

        var additionalCausePenalty =
            Math.Min(
                Math.Max(
                    0,
                    relevantCauseGroups.Count - 1)
                * 5,
                15);

        return Math.Clamp(
            baseScore - additionalCausePenalty,
            0,
            100);
    }

    private static int GetSeverityPriority(
        FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => 3,
            FindingSeverity.Warning => 2,
            FindingSeverity.Recommendation => 1,
            _ => 0
        };
    }

    private static int RoundToFive(
        double score)
    {
        var roundedScore =
            (int)Math.Round(
                score / 5d,
                MidpointRounding.AwayFromZero)
            * 5;

        return Math.Clamp(
            roundedScore,
            0,
            100);
    }

    private static ConditionRating DetermineRating(
        int score)
    {
        if (score >= 90)
        {
            return ConditionRating.VeryGood;
        }

        if (score >= 75)
        {
            return ConditionRating.Good;
        }

        if (score >= 50)
        {
            return ConditionRating.NeedsAttention;
        }

        return ConditionRating.Critical;
    }

    private static AssessmentDataQuality DetermineDataQuality(
        int availableAreaCount)
    {
        if (availableAreaCount >= 5)
        {
            return AssessmentDataQuality.Good;
        }

        if (availableAreaCount >= 4)
        {
            return AssessmentDataQuality.Sufficient;
        }

        if (availableAreaCount > 0)
        {
            return AssessmentDataQuality.Limited;
        }

        return AssessmentDataQuality.NotAvailable;
    }

    private static string BuildConditionSummary(
        ConditionRating rating,
        AssessmentDataQuality dataQuality)
    {
        var conditionText =
            rating switch
            {
                ConditionRating.VeryGood =>
                    "Die auswertbaren Hardwarebereiche zeigen "
                    + "insgesamt einen sehr guten Zustand.",

                ConditionRating.Good =>
                    "Die auswertbaren Hardwarebereiche zeigen "
                    + "insgesamt einen guten Zustand.",

                ConditionRating.NeedsAttention =>
                    "Mindestens ein auswertbarer Hardwarebereich "
                    + "sollte geprüft oder aufgerüstet werden.",

                ConditionRating.Critical =>
                    "Mindestens ein belastbarer Hardwarebefund "
                    + "erfordert besondere Aufmerksamkeit.",

                _ =>
                    "Der Hardwarezustand konnte nicht belastbar "
                    + "bewertet werden."
            };

        if (dataQuality == AssessmentDataQuality.Good)
        {
            return conditionText;
        }

        return conditionText
               + " Prozessor- und Plattformfähigkeit konnten "
               + "noch nicht vollständig berücksichtigt werden.";
    }

    private static (
        HardwarePlanningHorizon Horizon,
        string Summary
    ) DeterminePlanning(
        IReadOnlyCollection<CheckupFinding> findings,
        ConditionAssessment condition,
        bool hasCriticalPhysicalStorageFinding)
    {
        if (!condition.Score.HasValue)
        {
            return (
                HardwarePlanningHorizon.NotAvailable,

                "Die vorhandenen Hardwareinformationen reichen "
                + "nicht für eine belastbare Planungsaussage aus."
            );
        }

        if (hasCriticalPhysicalStorageFinding)
        {
            return (
                HardwarePlanningHorizon
                    .ConsiderPromptReplacement,

                "Für mindestens einen physischen Datenträger "
                + "wurde ein kritischer Zustand gemeldet. "
                + "Datensicherung und zeitnaher Austausch des "
                + "betroffenen Datenträgers sollten geprüft werden. "
                + "Daraus folgt nicht automatisch, dass der gesamte "
                + "Computer ersetzt werden muss."
            );
        }

        var hasUpgradeRecommendation =
            findings.Any(
                finding =>
                    finding.AssessmentTarget
                        == FindingAssessmentTarget.HardwareCondition
                    && finding.Code
                        is "hardware.memory.upgrade-recommended"
                        or "hardware.storage.relevant-hdd");

        if (hasUpgradeRecommendation)
        {
            return (
                HardwarePlanningHorizon.ConsiderUpgrade,

                "Die Hardware ist grundsätzlich weiter nutzbar. "
                + "Eine gezielte Aufrüstung bei Arbeitsspeicher "
                + "oder Datenträger kann die Alltagstauglichkeit "
                + "verbessern."
            );
        }

        if (condition.DataQuality
                == AssessmentDataQuality.Good
            && condition.Score.Value >= 90)
        {
            return (
                HardwarePlanningHorizon.LongTermSuitable,

                "Die vollständig auswertbaren Hardwarebereiche "
                + "sprechen für eine langfristig geeignete "
                + "Ausstattung. Eine Restlebensdauer wird daraus "
                + "nicht abgeleitet."
            );
        }

        if (condition.DataQuality
                is AssessmentDataQuality.Sufficient
                or AssessmentDataQuality.Good
            && condition.Score.Value >= 75)
        {
            return (
                HardwarePlanningHorizon.MediumTermUsable,

                "Die auswertbaren Hardwarebereiche sprechen für "
                + "eine mittelfristige Weiternutzung. Da Prozessor- "
                + "und Plattformfähigkeit noch nicht vollständig "
                + "bewertet werden können, wird keine langfristige "
                + "Eignung behauptet."
            );
        }

        return (
            HardwarePlanningHorizon.NotAvailable,

            "Aus dem Hardwarewert allein lässt sich derzeit kein "
            + "belastbarer Ersatzzeitpunkt ableiten. Die einzelnen "
            + "Hardwarebefunde sollten fachlich geprüft werden."
        );
    }

    private static bool IsMemoryInformationAvailable(
        CheckupSession checkupSession)
    {
        return TryParseMemoryGigabytes(
            checkupSession
                .HardwareInformation
                .InstalledMemory,
            out _);
    }

    private static bool IsPhysicalStorageHealthAvailable(
        CheckupSession checkupSession)
    {
        return GetAssessedPhysicalDrives(
                checkupSession)
            .Any(
                drive =>
                    drive.HealthStatus
                        is StorageHealthStatus.Healthy
                        or StorageHealthStatus.Warning
                        or StorageHealthStatus.Critical);
    }

    private static bool IsStorageTechnologyAvailable(
        CheckupSession checkupSession)
    {
        return GetAssessedPhysicalDrives(
                checkupSession)
            .Any(
                drive =>
                    drive.MediaType
                        is StorageMediaType.Hdd
                        or StorageMediaType.Ssd
                    || drive.BusType
                        == StorageBusType.Nvme);
    }

    private static bool IsTpmCapabilityAvailable(
        CheckupSession checkupSession)
    {
        var hardwareInformation =
            checkupSession.HardwareInformation;

        var tpmStatus =
            hardwareInformation.TpmStatus;

        var tpmVersion =
            hardwareInformation.TpmVersion;

        var hasRecognizedTpm =
            !string.IsNullOrWhiteSpace(tpmStatus)
            && (tpmStatus.Contains(
                    "Aktiv",
                    StringComparison.OrdinalIgnoreCase)
                || tpmStatus.Contains(
                    "Vorhanden",
                    StringComparison.OrdinalIgnoreCase));

        var hasRecognizedVersion =
            !string.IsNullOrWhiteSpace(tpmVersion)
            && !tpmVersion.Equals(
                "Unbekannt",
                StringComparison.OrdinalIgnoreCase);

        return hasRecognizedTpm
               && hasRecognizedVersion;
    }

    private static bool IsPlatformCapabilityAvailable(
        CheckupSession checkupSession)
    {
        return false;
    }

    private static IEnumerable<PhysicalDriveInformation>
        GetAssessedPhysicalDrives(
            CheckupSession checkupSession)
    {
        return checkupSession
            .StorageInformation
            .PhysicalDrives
            .Where(
                drive =>
                    !drive.IsExcludedFromAssessment);
    }

    private static bool TryParseMemoryGigabytes(
        string? installedMemory,
        out double memoryInGigabytes)
    {
        memoryInGigabytes =
            0;

        if (string.IsNullOrWhiteSpace(installedMemory))
        {
            return false;
        }

        var normalizedValue =
            installedMemory
                .Replace(
                    "GB",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Trim()
                .Replace(',', '.');

        return double.TryParse(
                   normalizedValue,
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out memoryInGigabytes)
               && memoryInGigabytes > 0;
    }

    private sealed record HardwareAssessmentArea(
        string Name,
        int Weight,
        Func<CheckupSession, bool> IsAvailable,
        Func<CheckupFinding, bool> MatchesFinding);
}