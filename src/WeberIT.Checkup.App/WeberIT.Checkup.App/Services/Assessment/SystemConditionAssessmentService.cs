using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class SystemConditionAssessmentService :
    ISystemConditionAssessmentService
{
    private const int MinimumAvailableWeightForScore =
        60;

    private static readonly IReadOnlyList<SystemAssessmentArea>
        AssessmentAreas =
        [
            new(
                "Betriebssystem",
                "system.operating-system.",
                15,
                IsOperatingSystemAvailable),

            new(
                "Sicherheitskonfiguration",
                "system.security.",
                25,
                IsSecurityInformationAvailable),

            new(
                "Windows Update",
                "system.windows-update.",
                15,
                IsWindowsUpdateInformationAvailable),

            new(
                "Programmupdates",
                "system.program-updates.",
                10,
                IsProgramUpdateInformationAvailable),

            new(
                "Neustartstatus",
                "system.restart.",
                5,
                IsRestartInformationAvailable),

            new(
                "Geräte und Treiber",
                "system.devices.",
                10,
                IsDeviceDriverInformationAvailable),

            new(
                "Logischer Speicherzustand",
                "system.storage.",
                10,
                IsLogicalStorageInformationAvailable),

            new(
                "Autostart",
                "system.startup.",
                5,
                IsStartupInformationAvailable),

            new(
                "Bereinigungspotenzial",
                "system.cleanup.",
                5,
                IsCleanupInformationAvailable)
        ];

    public ConditionAssessment Assess(
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
            return new ConditionAssessment
            {
                Score = null,

                Rating =
                    ConditionRating.NotAvailable,

                DataQuality =
                    dataQuality,

                Summary =
                    "Für eine belastbare Bewertung des "
                    + "Systemzustands stehen nicht genügend "
                    + "auswertbare Analysebereiche zur Verfügung.",

                EvaluatedAreaCount =
                    AssessmentAreas.Count,

                AvailableAreaCount =
                    availableAreas.Count
            };
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
                                    .SystemCondition
                            && finding.Code.StartsWith(
                                area.CodePrefix,
                                StringComparison.OrdinalIgnoreCase))
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

        if (findings.Any(
                finding =>
                    finding.AssessmentTarget
                        == FindingAssessmentTarget.SystemCondition
                    && finding.Severity
                        == FindingSeverity.Critical))
        {
            score =
                Math.Min(
                    score,
                    50);
        }

        var rating =
            DetermineRating(score);

        return new ConditionAssessment
        {
            Score =
                score,

            Rating =
                rating,

            DataQuality =
                dataQuality,

            Summary =
                BuildSummary(
                    rating,
                    dataQuality),

            EvaluatedAreaCount =
                AssessmentAreas.Count,

            AvailableAreaCount =
                availableAreas.Count
        };
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
                3 => 20,
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
        if (availableAreaCount >= 8)
        {
            return AssessmentDataQuality.Good;
        }

        if (availableAreaCount >= 6)
        {
            return AssessmentDataQuality.Sufficient;
        }

        if (availableAreaCount > 0)
        {
            return AssessmentDataQuality.Limited;
        }

        return AssessmentDataQuality.NotAvailable;
    }

    private static string BuildSummary(
        ConditionRating rating,
        AssessmentDataQuality dataQuality)
    {
        var conditionText =
            rating switch
            {
                ConditionRating.VeryGood =>
                    "Der analysierte Systemzustand ist insgesamt sehr gut.",

                ConditionRating.Good =>
                    "Der analysierte Systemzustand ist insgesamt gut.",

                ConditionRating.NeedsAttention =>
                    "Der Systemzustand weist relevanten Prüfungs- "
                    + "oder Wartungsbedarf auf.",

                ConditionRating.Critical =>
                    "Der Systemzustand enthält mindestens einen "
                    + "besonders schwerwiegenden oder mehrere "
                    + "deutliche Befunde.",

                _ =>
                    "Der Systemzustand konnte nicht belastbar "
                    + "bewertet werden."
            };

        if (dataQuality == AssessmentDataQuality.Good)
        {
            return conditionText;
        }

        return conditionText
               + " Die Aussage basiert auf einer "
               + "eingeschränkten Zahl auswertbarer Bereiche.";
    }

    private static bool IsOperatingSystemAvailable(
        CheckupSession checkupSession)
    {
        var operatingSystemName =
            checkupSession
                .OperatingSystemInformation
                .Name;

        return !string.IsNullOrWhiteSpace(
                   operatingSystemName)
               && !operatingSystemName.Equals(
                   "Unbekannt",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecurityInformationAvailable(
        CheckupSession checkupSession)
    {
        var information =
            checkupSession.SecurityInformation;

        return information.AntivirusProducts.Count > 0
               || information.AntivirusStatus
                   != SecurityState.Unknown
               || information.FirewallProfiles.Count > 0
               || information.UserAccountControlStatus
                   != SecurityState.Unknown
               || information.WindowsSecurityCenterStatus
                   != SecurityState.Unknown
               || information.SecureBootStatus
                   != SecurityState.Unknown
               || information.SystemDriveEncryption
                   .ProtectionState
                   != SecurityState.Unknown;
    }

    private static bool IsWindowsUpdateInformationAvailable(
        CheckupSession checkupSession)
    {
        var information =
            checkupSession.WindowsUpdateInformation;

        return information.ServiceState
                   != WindowsUpdateServiceState.Unknown
               || information.IsUpdateSearchSuccessful
               || information.LastSuccessfulInstallationDate
                   .HasValue
               || information.RecentFailures.Count > 0;
    }

    private static bool IsProgramUpdateInformationAvailable(
        CheckupSession checkupSession)
    {
        var information =
            checkupSession.ProgramUpdateInformation;

        return information.IsWingetAvailable == true
               && information.IsAnalysisPerformed
               && information.IsAnalysisSuccessful;
    }

    private static bool IsRestartInformationAvailable(
        CheckupSession checkupSession)
    {
        var information =
            checkupSession.RestartInformation;

        return information.IsAnalysisPerformed
               && information.IsAnalysisConclusive
               && information.IsRestartRequired.HasValue;
    }

    private static bool IsDeviceDriverInformationAvailable(
        CheckupSession checkupSession)
    {
        var information =
            checkupSession.DeviceDriverInformation;

        return information.HasAnalysis
               && !information.HasFailedOrIncompleteAnalysis;
    }

    private static bool IsLogicalStorageInformationAvailable(
        CheckupSession checkupSession)
    {
        return checkupSession
            .StorageInformation
            .Volumes
            .Any(
                volume =>
                    volume.IsReady
                    && volume.TotalSizeBytes.HasValue
                    && volume.TotalSizeBytes.Value > 0
                    && volume.FreeSpaceBytes.HasValue
                    && volume.FreeSpacePercent.HasValue);
    }

    private static bool IsStartupInformationAvailable(
        CheckupSession checkupSession)
    {
        return checkupSession
            .StartupInformation
            .AnalysisStatus
            == StartupAnalysisStatus.Analyzed;
    }

    private static bool IsCleanupInformationAvailable(
        CheckupSession checkupSession)
    {
        return checkupSession
            .CleanupPotentialInformation
            .AnalysisStatus
            == CleanupMeasurementStatus.Measured;
    }

    private sealed record SystemAssessmentArea(
        string Name,
        string CodePrefix,
        int Weight,
        Func<CheckupSession, bool> IsAvailable);
}