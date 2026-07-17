using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class DeviceDriverAssessmentRule :
    ICheckupAssessmentRule
{
    private static readonly HashSet<int>
        InformationalOrTemporaryDeviceCodes =
            new()
            {
                21,
                45,
                47
            };

    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var findings =
            new List<CheckupFinding>();

        var information =
            checkupSession.DeviceDriverInformation;

        if (!information.HasAnalysis)
        {
            return findings;
        }

        AddAnalysisStatusFinding(
            information,
            findings);

        AddMissingDriverFinding(
            information,
            findings);

        AddWindowsProblemFinding(
            information,
            findings);

        AddRestartRelatedFinding(
            information,
            findings);

        AddUnsignedDriverFinding(
            information,
            findings);

        return findings;
    }

    private static void AddAnalysisStatusFinding(
        DeviceDriverInformation information,
        ICollection<CheckupFinding> findings)
    {
        if (!information.HasFailedOrIncompleteAnalysis)
        {
            return;
        }

        var description =
            information.AnalysisStatus switch
            {
                DeviceDriverAnalysisStatus.TimedOut =>
                    "Das Zeitlimit wurde erreicht. Die bis dahin "
                    + "ermittelten Geräteinformationen werden verwendet, "
                    + "das Ergebnis kann jedoch unvollständig sein.",

                DeviceDriverAnalysisStatus.PartiallyAnalyzed =>
                    "Die lokalen Geräte wurden ermittelt, mindestens "
                    + "eine unterstützte Treiberquelle konnte jedoch "
                    + "nicht vollständig ausgewertet werden.",

                DeviceDriverAnalysisStatus.NotEvaluable =>
                    "Die lokalen Geräte- und Treiberinformationen "
                    + "konnten nicht zuverlässig ausgewertet werden.",

                _ =>
                    "Die Geräte- und Treiberanalyse konnte nicht "
                    + "vollständig ausgeführt werden."
            };

        findings.Add(
            new CheckupFinding
            {
                Title =
                    "Geräte- und Treiberanalyse unvollständig",

                Description =
                    description,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information
            });
    }

    private static void AddMissingDriverFinding(
        DeviceDriverInformation information,
        ICollection<CheckupFinding> findings)
    {
        var missingDriverEntries =
            information.Entries
                .Where(
                    entry =>
                        entry.DriverAssignmentState
                            == DriverAssignmentState.Missing
                        || entry.ConfigManagerErrorCode == 28)
                .ToList();

        if (missingDriverEntries.Count == 0)
        {
            return;
        }

        var description =
            missingDriverEntries.Count == 1
                ? "Windows meldet für ein Gerät, dass der benötigte "
                  + "Treiber fehlt. Das Gerät sollte im Geräte-Manager "
                  + "geprüft und der passende Treiber kontrolliert "
                  + "ermittelt werden."
                : $"Windows meldet für {missingDriverEntries.Count} "
                  + "Geräte, dass benötigte Treiber fehlen. Die Geräte "
                  + "sollten im Geräte-Manager geprüft und die passenden "
                  + "Treiber kontrolliert ermittelt werden.";

        findings.Add(
            new CheckupFinding
            {
                Title =
                    missingDriverEntries.Count == 1
                        ? "Fehlenden Gerätetreiber prüfen"
                        : "Fehlende Gerätetreiber prüfen",

                Description =
                    description,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning
            });
    }

    private static void AddWindowsProblemFinding(
        DeviceDriverInformation information,
        ICollection<CheckupFinding> findings)
    {
        var problemEntries =
            information.Entries
                .Where(
                    entry =>
                        entry.OperationalState
                            == DeviceOperationalState.Problem
                        && entry.ConfigManagerErrorCode != 28
                        && entry.ConfigManagerErrorCode != 14
                        && !IsInformationalOrTemporaryCode(
                            entry.ConfigManagerErrorCode))
                .ToList();

        if (problemEntries.Count == 0)
        {
            return;
        }

        var description =
            problemEntries.Count == 1
                ? "Windows meldet für ein aktives Gerät einen konkreten "
                  + "Fehlercode. Die Ursache kann im Treiber, in der "
                  + "Konfiguration oder in der Hardware liegen und sollte "
                  + "im Geräte-Manager geprüft werden."
                : $"Windows meldet für {problemEntries.Count} aktive "
                  + "Geräte konkrete Fehlercodes. Die Ursachen können "
                  + "in Treibern, Konfigurationen oder der Hardware "
                  + "liegen und sollten im Geräte-Manager geprüft werden.";

        findings.Add(
            new CheckupFinding
            {
                Title =
                    problemEntries.Count == 1
                        ? "Windows-Geräteproblem prüfen"
                        : "Windows-Geräteprobleme prüfen",

                Description =
                    description,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning
            });
    }

    private static void AddRestartRelatedFinding(
        DeviceDriverInformation information,
        ICollection<CheckupFinding> findings)
    {
        var restartRelatedEntries =
            information.Entries
                .Where(
                    entry =>
                        entry.ConfigManagerErrorCode == 14)
                .ToList();

        if (restartRelatedEntries.Count == 0)
        {
            return;
        }

        var description =
            restartRelatedEntries.Count == 1
                ? "Windows meldet für ein Gerät, dass ein Neustart zur "
                  + "vollständigen Einrichtung erforderlich sein kann. "
                  + "Diese Quelle ist ein Hinweis und keine automatische "
                  + "Neustartaufforderung."
                : $"Windows meldet für {restartRelatedEntries.Count} "
                  + "Geräte, dass ein Neustart zur vollständigen "
                  + "Einrichtung erforderlich sein kann. Diese Quellen "
                  + "sind Hinweise und keine automatische "
                  + "Neustartaufforderung.";

        findings.Add(
            new CheckupFinding
            {
                Title =
                    "Gerätebezogenen Neustarthinweis prüfen",

                Description =
                    description,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Recommendation
            });
    }

    private static void AddUnsignedDriverFinding(
        DeviceDriverInformation information,
        ICollection<CheckupFinding> findings)
    {
        var unsignedDriverEntries =
            information.Entries
                .Where(
                    entry =>
                        entry.IsSigned == false
                        && entry.Classification
                            == DeviceDriverClassification.UnsignedDriver)
                .ToList();

        if (unsignedDriverEntries.Count == 0)
        {
            return;
        }

        var description =
            unsignedDriverEntries.Count == 1
                ? "Windows kennzeichnet den zugeordneten Treiber eines "
                  + "relevanten Geräts nicht als signiert. Das ist kein "
                  + "automatischer Schadsoftware- oder Defektnachweis, "
                  + "sollte aber manuell geprüft werden."
                : $"Windows kennzeichnet die zugeordneten Treiber von "
                  + $"{unsignedDriverEntries.Count} relevanten Geräten "
                  + "nicht als signiert. Das ist kein automatischer "
                  + "Schadsoftware- oder Defektnachweis, sollte aber "
                  + "manuell geprüft werden.";

        findings.Add(
            new CheckupFinding
            {
                Title =
                    unsignedDriverEntries.Count == 1
                        ? "Nicht signierten Gerätetreiber prüfen"
                        : "Nicht signierte Gerätetreiber prüfen",

                Description =
                    description,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Recommendation
            });
    }

    private static bool IsInformationalOrTemporaryCode(
        int? configManagerErrorCode)
    {
        return configManagerErrorCode.HasValue
               && InformationalOrTemporaryDeviceCodes.Contains(
                   configManagerErrorCode.Value);
    }
}