using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class UserAccountControlAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var status =
            checkupSession
                .SecurityInformation
                .UserAccountControlStatus;

        return status switch
        {
            SecurityState.Enabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.uac-enabled",

                        Title =
                            "Benutzerkontensteuerung aktiv",

                        Description =
                            "Die Windows-Benutzerkontensteuerung "
                            + "ist grundsätzlich aktiviert. "
                            + "Individuelle Benachrichtigungsstufen "
                            + "werden dabei nicht vorschnell als "
                            + "Fehler bewertet.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.uac-configuration"
                    }
                },

            SecurityState.Disabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.uac-disabled",

                        Title =
                            "Benutzerkontensteuerung deaktiviert",

                        Description =
                            "Die Windows-Benutzerkontensteuerung "
                            + "ist vollständig deaktiviert. Dadurch "
                            + "können Programme Änderungen mit "
                            + "weniger wirksamer Kontrolle "
                            + "durchführen. Die Aktivierung sollte "
                            + "zeitnah geprüft werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Warning,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.uac-configuration"
                    }
                },

            _ =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.uac-not-evaluable",

                        Title =
                            "Benutzerkontensteuerung nicht auswertbar",

                        Description =
                            "Der grundlegende Status der "
                            + "Windows-Benutzerkontensteuerung "
                            + "konnte nicht zuverlässig ermittelt "
                            + "werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.InformationOnly,

                        CauseGroup =
                            "system.security.uac-data-quality"
                    }
                }
        };
    }
}