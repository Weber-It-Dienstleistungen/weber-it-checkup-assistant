using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class RestartAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var restartInformation =
            checkupSession.RestartInformation;

        if (restartInformation.IsAnalysisPerformed)
        {
            return
            [
                CreateCentralRestartFinding(
                    restartInformation)
            ];
        }

        return
        [
            CreateLegacyRestartFinding(
                checkupSession.WindowsUpdateInformation)
        ];
    }

    private static CheckupFinding CreateCentralRestartFinding(
        RestartInformation restartInformation)
    {
        if (restartInformation.IsRestartRequired == true)
        {
            var detectedSources =
                restartInformation.Sources
                    .Where(source =>
                        IsAuthoritativeSource(source)
                        && source.IsCheckSuccessful
                        && source.IsRestartRequired == true)
                    .Select(source =>
                        source.DisplayName)
                    .Where(displayName =>
                        !string.IsNullOrWhiteSpace(displayName))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var sourceText =
                detectedSources.Count > 0
                    ? string.Join(
                        ", ",
                        detectedSources)
                    : "Windows-Neustartindikatoren";

            return new CheckupFinding
            {
                Code =
                    "system.restart.required",

                Title =
                    "Windows-Neustart erforderlich",

                Description =
                    "Mindestens eine bestätigende "
                    + "Windows-Quelle meldet einen "
                    + "ausstehenden Neustart. Vor weiteren "
                    + "Wartungs- oder Reparaturmaßnahmen "
                    + "sollte der Computer kontrolliert "
                    + "neu gestartet werden. Erkannte "
                    + $"Quelle: {sourceText}.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        var advisorySources =
            restartInformation.Sources
                .Where(source =>
                    !IsAuthoritativeSource(source)
                    && source.IsCheckSuccessful
                    && source.IsRestartRequired == true)
                .Select(source =>
                    source.DisplayName)
                .Where(displayName =>
                    !string.IsNullOrWhiteSpace(displayName))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (advisorySources.Count > 0)
        {
            return new CheckupFinding
            {
                Code =
                    "system.restart.advisory",

                Title =
                    "Möglicher Neustartbedarf erkannt",

                Description =
                    "Windows hat Zustände vorgemerkt, "
                    + "die bei einem kommenden Systemstart "
                    + "verarbeitet werden können. Daraus "
                    + "lässt sich allein kein sicher "
                    + "erforderlicher Neustart ableiten. "
                    + "Wenn Wartungsarbeiten, Installationen "
                    + "oder ungewöhnliches Systemverhalten "
                    + "vorliegen, kann ein kontrollierter "
                    + "Neustart sinnvoll sein. Hinweisgebende "
                    + "Quelle: "
                    + string.Join(
                        ", ",
                        advisorySources)
                    + ".",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        if (restartInformation.IsAnalysisConclusive
            && restartInformation.IsRestartRequired == false)
        {
            return new CheckupFinding
            {
                Code =
                    "system.restart.none-required",

                Title =
                    "Kein ausstehender Windows-Neustart erkannt",

                Description =
                    "Die geprüften Windows-, Komponentenwartungs-, "
                    + "Dateioperations- und Computername-Indikatoren "
                    + "melden derzeit keinen erforderlichen "
                    + "Neustart. Die Aussage bezieht sich auf "
                    + "die von diesem Checkup geprüften bekannten "
                    + "Windows-Quellen.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        var failedSources =
            restartInformation.Sources
                .Where(source =>
                    !source.IsCheckSuccessful)
                .Select(source =>
                    source.DisplayName)
                .Where(displayName =>
                    !string.IsNullOrWhiteSpace(displayName))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var failedSourceText =
            failedSources.Count > 0
                ? " Nicht vollständig auswertbar: "
                  + string.Join(
                      ", ",
                      failedSources)
                  + "."
                : string.Empty;

        return new CheckupFinding
        {
            Code =
                "system.restart.not-fully-evaluable",

            Title =
                "Windows-Neustartbedarf nicht vollständig auswertbar",

            Description =
                "Die bekannten lokalen Windows-Indikatoren "
                + "für einen erforderlichen Neustart konnten "
                + "nicht vollständig ausgewertet werden. "
                + "Deshalb wird nicht behauptet, dass sicher "
                + "kein Neustart erforderlich ist."
                + failedSourceText,

            Category =
                FindingCategory.OperatingSystem,

            Severity =
                FindingSeverity.Information,

            AssessmentTarget =
                FindingAssessmentTarget.InformationOnly,

            CauseGroup =
                "system.restart.data-quality"
        };
    }

    private static CheckupFinding CreateLegacyRestartFinding(
        WindowsUpdateInformation updateInformation)
    {
        if (updateInformation.IsRestartRequired == true)
        {
            var reasons =
                updateInformation.RestartReasons.Count > 0
                    ? string.Join(
                        ", ",
                        updateInformation.RestartReasons)
                    : "Windows-Komponenten";

            return new CheckupFinding
            {
                Code =
                    "system.restart.legacy-required",

                Title =
                    "Windows-Neustart erforderlich",

                Description =
                    "Der ältere gespeicherte Checkup enthält "
                    + "einen erkannten Neustartbedarf. Vor "
                    + "weiteren Wartungs- oder "
                    + "Reparaturmaßnahmen sollte der Computer "
                    + "kontrolliert neu gestartet werden. "
                    + $"Technische Quelle: {reasons}.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        if (updateInformation.IsRestartRequired == false)
        {
            return new CheckupFinding
            {
                Code =
                    "system.restart.legacy-none-required",

                Title =
                    "Kein ausstehender Windows-Neustart erkannt",

                Description =
                    "Im älteren gespeicherten Checkup haben "
                    + "die damals geprüften Windows- und "
                    + "Komponentenwartungsindikatoren keinen "
                    + "erforderlichen Neustart gemeldet.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        return new CheckupFinding
        {
            Code =
                "system.restart.legacy-not-evaluable",

            Title =
                "Windows-Neustartbedarf nicht auswertbar",

            Description =
                "Der ältere gespeicherte Checkup enthält "
                + "keine zuverlässige Aussage über einen "
                + "erforderlichen Windows-Neustart. Ein neuer "
                + "Systemscan kann den aktuellen Zustand mit "
                + "der erweiterten Neustartanalyse prüfen.",

            Category =
                FindingCategory.OperatingSystem,

            Severity =
                FindingSeverity.Information,

            AssessmentTarget =
                FindingAssessmentTarget.InformationOnly,

            CauseGroup =
                "system.restart.data-quality"
        };
    }

    private static bool IsAuthoritativeSource(
        RestartSourceResult source)
    {
        return source.SourceType
            is RestartSourceType.WindowsUpdate
            or RestartSourceType.ComponentBasedServicing
            or RestartSourceType.PendingComputerRename;
    }
}