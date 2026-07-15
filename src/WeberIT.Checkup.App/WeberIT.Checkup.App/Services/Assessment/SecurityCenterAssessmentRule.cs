using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class SecurityCenterAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var status =
            checkupSession
                .SecurityInformation
                .WindowsSecurityCenterStatus;

        return status switch
        {
            SecurityState.Enabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Windows-Sicherheitscenter aktiv",

                        Description =
                            "Der Windows-Sicherheitscenter-Dienst "
                            + "läuft und kann die registrierten "
                            + "Sicherheitszustände überwachen.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information
                    }
                },

            SecurityState.Disabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Windows-Sicherheitscenter nicht aktiv",

                        Description =
                            "Der Windows-Sicherheitscenter-Dienst "
                            + "läuft nicht. Dadurch können "
                            + "Virenschutz- und andere "
                            + "Sicherheitszustände unvollständig "
                            + "oder falsch gemeldet werden. Der "
                            + "Dienststatus sollte geprüft werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Warning
                    }
                },

            _ =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Windows-Sicherheitscenter nicht auswertbar",

                        Description =
                            "Der Status des "
                            + "Windows-Sicherheitscenter-Dienstes "
                            + "konnte nicht zuverlässig ermittelt "
                            + "werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information
                    }
                }
        };
    }
}