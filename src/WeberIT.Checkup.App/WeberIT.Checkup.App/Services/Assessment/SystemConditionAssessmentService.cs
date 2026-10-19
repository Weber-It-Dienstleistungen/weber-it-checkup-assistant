
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class SystemConditionAssessmentService :
    ISystemConditionAssessmentService
{
    private const int MinimumAvailableWeightForScore =
        60;

    private const int MaximumAdditionalUpdatePenalty =
        20;

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
        ArgumentNullException.ThrowIfNull(
            checkupSession);

        ArgumentNullException.ThrowIfNull(
            findings);

        var updateInformation =
            checkupSession.WindowsUpdateInformation;

        var programUpdateInformation =
            checkupSession.ProgramUpdateInformation;

        var availableAreas =
            AssessmentAreas
                .Where(area =>
                    area.IsAvailable(checkupSession))
                .ToList();

        var availableWeight =
            availableAreas.Sum(area => area.Weight);

        var dataQuality =
            DetermineDataQuality(
                availableAreas.Count);

        var updateSearchIncomplete =
            !HasSuccessfulUpdateSearch(
                updateInformation);

        var updateDatesIncomplete =
            HasUnknownUpdateDates(
                updateInformation);

        if ((updateSearchIncomplete
             || updateDatesIncomplete)
            && dataQuality == AssessmentDataQuality.Good)
        {
            dataQuality =
                AssessmentDataQuality.Sufficient;
        }

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
                    + "auswertbare Analysebereiche zur Verfügung."
                    + BuildUpdateDataQualityNote(
                        updateSearchIncomplete,
                        updateDatesIncomplete),

                EvaluatedAreaCount =
                    AssessmentAreas.Count,

                AvailableAreaCount =
                    availableAreas.Count
            };
        }

        double weightedScoreTotal = 0;

        foreach (var area in availableAreas)
        {
            var areaFindings =
                findings
                    .Where(finding =>
                        finding.AssessmentTarget
                            == FindingAssessmentTarget
                                .SystemCondition
                        && finding.Code.StartsWith(
                            area.CodePrefix,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            int areaScore;

            if (area.CodePrefix.Equals(
                    "system.windows-update.",
                    StringComparison.OrdinalIgnoreCase))
            {
                areaScore =
                    CalculateWindowsUpdateAreaScore(
                        updateInformation,
                        areaFindings);
            }
            else if (area.CodePrefix.Equals(
                         "system.program-updates.",
                         StringComparison.OrdinalIgnoreCase))
            {
                areaScore =
                    CalculateProgramUpdatesAreaScore(
                        programUpdateInformation);
            }
            else
            {
                areaScore =
                    CalculateAreaScore(
                        areaFindings);
            }

            weightedScoreTotal +=
                areaScore * area.Weight;
        }

        var normalizedScore =
            weightedScoreTotal / availableWeight;

        var score =
            RoundToWholeNumber(
                normalizedScore);

        // Bestehende globale Begrenzung
        // für kritische Systembefunde.
        if (findings.Any(finding =>
                finding.AssessmentTarget
                    == FindingAssessmentTarget.SystemCondition
                && finding.Severity
                    == FindingSeverity.Critical))
        {
            score =
                Math.Min(score, 50);
        }

        // Obergrenzen durch besonders kritische
        // Windows-Sicherheitsupdates.
        var updateScoreCap =
            DetermineWindowsUpdateScoreCap(
                updateInformation);

        if (updateScoreCap.HasValue)
        {
            score =
                Math.Min(
                    score,
                    updateScoreCap.Value);
        }

        // Obergrenzen durch die Anzahl zuverlässig
        // erkannter ausstehender Programmupdates.
        var programUpdateScoreCap =
            DetermineProgramUpdateScoreCap(
                programUpdateInformation);

        if (programUpdateScoreCap.HasValue)
        {
            score =
                Math.Min(
                    score,
                    programUpdateScoreCap.Value);
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
                    dataQuality)
                + BuildUpdateDataQualityNote(
                    updateSearchIncomplete,
                    updateDatesIncomplete),

            EvaluatedAreaCount =
                AssessmentAreas.Count,

            AvailableAreaCount =
                availableAreas.Count
        };
    }

    private static int CalculateProgramUpdatesAreaScore(
        ProgramUpdateInformation information)
    {
        if (!HasSuccessfulProgramUpdateAnalysis(
                information))
        {
            // Dieser Fall wird über die
            // Verfügbarkeitsprüfung ausgeschlossen.
            // Eine fehlgeschlagene Analyse erhält
            // niemals automatisch 100 Punkte.
            return 0;
        }

        var count =
            information.AvailableUpdateCount;

        return count switch
        {
            0 => 100,
            <= 2 => 90,
            <= 5 => 75,
            <= 10 => 55,
            <= 20 => 30,
            <= 50 => 10,
            _ => 0
        };
    }

    private static int? DetermineProgramUpdateScoreCap(
        ProgramUpdateInformation information)
    {
        if (!HasSuccessfulProgramUpdateAnalysis(
                information))
        {
            return null;
        }

        var count =
            information.AvailableUpdateCount;

        return count switch
        {
            <= 5 => null,
            <= 10 => 90,
            <= 20 => 80,
            <= 50 => 60,
            _ => 45
        };
    }

    private static bool HasSuccessfulProgramUpdateAnalysis(
        ProgramUpdateInformation information)
    {
        return information.IsWingetAvailable == true
               && information.IsAnalysisPerformed
               && information.IsAnalysisSuccessful
               && information.AvailableUpdateCount >= 0;
    }

    private static int CalculateWindowsUpdateAreaScore(
        WindowsUpdateInformation information,
        IReadOnlyCollection<CheckupFinding> findings)
    {
        var otherFindings =
            findings
                .Where(finding =>
                    !finding.Code.Equals(
                        "system.windows-update.pending-updates",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        var configurationScore =
            CalculateAreaScore(otherFindings);

        if (!HasSuccessfulUpdateSearch(information))
        {
            return configurationScore;
        }

        var pendingUpdates =
            GetRelevantPendingUpdates(information);

        if (pendingUpdates.Count == 0)
        {
            return configurationScore;
        }

        var individualScores =
            pendingUpdates
                .Select(CalculatePendingUpdateScore)
                .ToList();

        var lowestUpdateScore =
            individualScores.Min();

        var penalizedUpdateCount =
            individualScores.Count(score => score < 100);

        var additionalPenalty =
            Math.Min(
                Math.Max(
                    0,
                    penalizedUpdateCount - 1) * 5,
                MaximumAdditionalUpdatePenalty);

        var updateScore =
            Math.Clamp(
                lowestUpdateScore - additionalPenalty,
                0,
                100);

        return Math.Min(
            configurationScore,
            updateScore);
    }

    private static int CalculatePendingUpdateScore(
        PendingWindowsUpdate update)
    {
        var ageDays =
            GetUpdateAgeDays(update);

        var isCriticalSecurityUpdate =
            IsMsrcCriticalSecurityUpdate(update);

        if (isCriticalSecurityUpdate)
        {
            if (!ageDays.HasValue)
            {
                return 40;
            }

            return ageDays.Value switch
            {
                <= 7 => 40,
                <= 14 => 25,
                <= 30 => 15,
                _ => 0
            };
        }

        if (update.Category
            == WindowsUpdateCategory.Security)
        {
            if (!ageDays.HasValue)
            {
                return 90;
            }

            return ageDays.Value switch
            {
                <= 3 => 90,
                <= 14 => 75,
                <= 30 => 55,
                <= 90 => 25,
                _ => 10
            };
        }

        if (update.Category
            == WindowsUpdateCategory.Quality)
        {
            if (!ageDays.HasValue)
            {
                return 90;
            }

            return ageDays.Value switch
            {
                <= 7 => 100,
                <= 30 => 90,
                <= 90 => 75,
                _ => 50
            };
        }

        if (update.Category
            == WindowsUpdateCategory.Definition)
        {
            if (!ageDays.HasValue)
            {
                return 90;
            }

            return ageDays.Value switch
            {
                <= 1 => 95,
                <= 3 => 85,
                <= 14 => 75,
                _ => 55
            };
        }

        if (!ageDays.HasValue)
        {
            return 90;
        }

        return ageDays.Value switch
        {
            <= 7 => 90,
            <= 30 => 75,
            _ => 55
        };
    }

    private static int? DetermineWindowsUpdateScoreCap(
        WindowsUpdateInformation information)
    {
        if (!HasSuccessfulUpdateSearch(information))
        {
            return null;
        }

        var criticalUpdates =
            GetRelevantPendingUpdates(information)
                .Where(IsMsrcCriticalSecurityUpdate)
                .ToList();

        if (criticalUpdates.Count == 0)
        {
            return null;
        }

        if (criticalUpdates.Count >= 2)
        {
            return 50;
        }

        if (criticalUpdates.Any(update =>
                GetUpdateAgeDays(update) is > 30))
        {
            return 50;
        }

        return 70;
    }

    private static bool IsMsrcCriticalSecurityUpdate(
        PendingWindowsUpdate update)
    {
        return update.Category
                   == WindowsUpdateCategory.Security
               && string.Equals(
                   update.MsrcSeverity?.Trim(),
                   "Critical",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int? GetUpdateAgeDays(
        PendingWindowsUpdate update)
    {
        if (!update.LastDeploymentChangeTime.HasValue)
        {
            return null;
        }

        var deploymentDate =
            update.LastDeploymentChangeTime.Value;

        if (deploymentDate.Year < 2000
            || deploymentDate.Date
                > DateTime.UtcNow.Date.AddDays(7))
        {
            return null;
        }

        var age =
            (DateTime.UtcNow.Date
             - deploymentDate.Date).Days;

        return Math.Max(0, age);
    }

    private static List<PendingWindowsUpdate>
        GetRelevantPendingUpdates(
            WindowsUpdateInformation information)
    {
        return (information.PendingUpdates
                ?? new List<PendingWindowsUpdate>())
            .Where(update =>
                update is not null
                && !update.IsHidden
                && !update.IsOptional
                && update.Category
                    is not WindowsUpdateCategory.Driver
                    and not WindowsUpdateCategory.Feature
                    and not WindowsUpdateCategory.Optional)
            .ToList();
    }

    private static bool HasSuccessfulUpdateSearch(
        WindowsUpdateInformation information)
    {
        return information.IsUpdateSearchPerformed
               && information.IsUpdateSearchSuccessful;
    }

    private static bool HasUnknownUpdateDates(
        WindowsUpdateInformation information)
    {
        if (!HasSuccessfulUpdateSearch(information))
        {
            return false;
        }

        return GetRelevantPendingUpdates(information)
            .Any(update =>
                !GetUpdateAgeDays(update).HasValue);
    }

    private static string BuildUpdateDataQualityNote(
        bool searchIncomplete,
        bool datesIncomplete)
    {
        if (searchIncomplete)
        {
            return " Der Bestand ausstehender Windows-Updates "
                   + "konnte nicht zuverlässig online "
                   + "geprüft werden und wurde deshalb "
                   + "nicht als vollständig bewerteter "
                   + "Analysebereich berücksichtigt.";
        }

        if (datesIncomplete)
        {
            return " Für mindestens ein ausstehendes "
                   + "Windows-Update liegt kein belastbares "
                   + "Bereitstellungsdatum vor. "
                   + "Die altersabhängige Einstufung "
                   + "ist deshalb eingeschränkt.";
        }

        return string.Empty;
    }

    private static int CalculateAreaScore(
        IReadOnlyCollection<CheckupFinding> findings)
    {
        var relevantCauseGroups =
            findings
                .Where(finding =>
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
                .Select(group =>
                    group
                        .OrderByDescending(finding =>
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
                .Max(finding =>
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
                    relevantCauseGroups.Count - 1) * 5,
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

    private static int RoundToWholeNumber(
        double score)
    {
        return Math.Clamp(
            (int)Math.Round(
                score,
                MidpointRounding.AwayFromZero),
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
        return HasSuccessfulUpdateSearch(
            checkupSession.WindowsUpdateInformation);
    }

    private static bool IsProgramUpdateInformationAvailable(
        CheckupSession checkupSession)
    {
        return HasSuccessfulProgramUpdateAnalysis(
            checkupSession.ProgramUpdateInformation);
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
            .Any(volume =>
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
