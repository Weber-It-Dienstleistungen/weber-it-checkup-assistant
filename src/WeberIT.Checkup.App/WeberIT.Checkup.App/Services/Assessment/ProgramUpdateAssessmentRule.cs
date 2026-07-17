using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class ProgramUpdateAssessmentRule :
    ICheckupAssessmentRule
{
    private const int MaximumDisplayedUpdates = 5;

    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var programUpdateInformation =
            checkupSession.ProgramUpdateInformation;

        if (programUpdateInformation.IsWingetAvailable == false)
        {
            return
            [
                new CheckupFinding
                {
                    Code =
                        "system.program-updates.winget-unavailable",

                    Title =
                        "WinGet nicht verfügbar",

                    Description =
                        "Der Windows-Paketmanager WinGet ist für den "
                        + "aktuell angemeldeten Benutzer nicht verfügbar. "
                        + "Deshalb konnten keine über WinGet erkannten "
                        + "Programmaktualisierungen geprüft werden. "
                        + programUpdateInformation.AnalysisDetails,

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.program-updates.data-quality"
                }
            ];
        }

        if (programUpdateInformation.IsWingetAvailable is null)
        {
            return
            [
                new CheckupFinding
                {
                    Code =
                        "system.program-updates.winget-not-evaluable",

                    Title =
                        "WinGet-Status nicht ermittelbar",

                    Description =
                        "Die Verfügbarkeit des Windows-Paketmanagers "
                        + "WinGet konnte nicht zuverlässig ermittelt werden. "
                        + programUpdateInformation.AnalysisDetails,

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.program-updates.data-quality"
                }
            ];
        }

        if (!programUpdateInformation.IsAnalysisPerformed)
        {
            return
            [
                new CheckupFinding
                {
                    Code =
                        "system.program-updates.analysis-not-performed",

                    Title =
                        "Programupdate-Analyse nicht durchgeführt",

                    Description =
                        "WinGet ist verfügbar, die Prüfung auf verfügbare "
                        + "Programmaktualisierungen wurde jedoch nicht "
                        + "durchgeführt.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.program-updates.data-quality"
                }
            ];
        }

        if (!programUpdateInformation.IsAnalysisSuccessful)
        {
            return
            [
                new CheckupFinding
                {
                    Code =
                        "system.program-updates.analysis-not-evaluable",

                    Title =
                        "Programupdate-Status nicht ermittelbar",

                    Description =
                        "WinGet konnte die verfügbaren "
                        + "Programmaktualisierungen aus der öffentlichen "
                        + "WinGet-Quelle nicht zuverlässig ermitteln. "
                        + programUpdateInformation.AnalysisDetails,

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.program-updates.data-quality"
                }
            ];
        }

        if (programUpdateInformation.AvailableUpdateCount == 0)
        {
            return
            [
                new CheckupFinding
                {
                    Code =
                        "system.program-updates.none-available",

                    Title =
                        "Keine WinGet-Programmaktualisierungen erkannt",

                    Description =
                        "WinGet hat in der öffentlichen WinGet-Quelle "
                        + "keine verfügbaren Aktualisierungen für erkannte "
                        + "Programme gemeldet. Microsoft-Store-Apps und "
                        + "Programme, die WinGet nicht zuordnen kann, sind "
                        + "in dieser Aussage nicht enthalten. Es wurden "
                        + "keine Quellenvereinbarungen automatisch akzeptiert.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.program-updates.available"
                }
            ];
        }

        return
        [
            new CheckupFinding
            {
                Code =
                    "system.program-updates.available",

                Title =
                    BuildAvailableUpdatesTitle(
                        programUpdateInformation.AvailableUpdateCount),

                Description =
                    BuildAvailableUpdatesDescription(
                        programUpdateInformation),

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.program-updates.available"
            }
        ];
    }

    private static string BuildAvailableUpdatesTitle(
        int availableUpdateCount)
    {
        return availableUpdateCount == 1
            ? "Eine Programmaktualisierung verfügbar"
            : $"{availableUpdateCount} Programmaktualisierungen verfügbar";
    }

    private static string BuildAvailableUpdatesDescription(
        ProgramUpdateInformation programUpdateInformation)
    {
        var displayedUpdates =
            programUpdateInformation.AvailableUpdates
                .Take(MaximumDisplayedUpdates)
                .ToList();

        var description =
            "WinGet hat in der öffentlichen WinGet-Quelle "
            + "Aktualisierungen für folgende Programme erkannt:";

        if (displayedUpdates.Count > 0)
        {
            description +=
                Environment.NewLine
                + Environment.NewLine
                + string.Join(
                    Environment.NewLine,
                    displayedUpdates.Select(
                        update =>
                            "• "
                            + update.Name
                            + ": "
                            + update.InstalledVersion
                            + " → "
                            + update.AvailableVersion));
        }

        var undisplayedUpdateCount =
            programUpdateInformation.AvailableUpdateCount
            - displayedUpdates.Count;

        if (undisplayedUpdateCount > 0)
        {
            description +=
                Environment.NewLine
                + $"• sowie {undisplayedUpdateCount} weitere";
        }

        description +=
            Environment.NewLine
            + Environment.NewLine
            + "Microsoft-Store-Apps und Programme, die WinGet nicht "
            + "zuordnen kann, sind in dieser Aussage nicht enthalten. "
            + "Es wurden weder Quellenvereinbarungen automatisch "
            + "akzeptiert noch Programme aktualisiert.";

        if (!string.IsNullOrWhiteSpace(
            programUpdateInformation.WingetVersion))
        {
            description +=
                " Verwendete WinGet-Version: "
                + programUpdateInformation.WingetVersion
                + ".";
        }

        return description;
    }
}