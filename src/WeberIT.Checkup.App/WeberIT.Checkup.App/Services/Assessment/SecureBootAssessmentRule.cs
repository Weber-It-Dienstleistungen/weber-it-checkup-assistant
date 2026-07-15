using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class SecureBootAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var status =
            checkupSession
                .SecurityInformation
                .SecureBootStatus;

        return status switch
        {
            SecurityState.Enabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Secure Boot aktiv",

                        Description =
                            "Der sichere UEFI-Start ist aktiviert. "
                            + "Windows kann dadurch beim Start "
                            + "nicht vertrauenswürdige "
                            + "Boot-Komponenten besser abwehren.",

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
                            "Secure Boot deaktiviert",

                        Description =
                            "Das Gerät verwendet UEFI, Secure Boot "
                            + "ist jedoch deaktiviert. Es sollte "
                            + "geprüft werden, ob die Funktion ohne "
                            + "Beeinträchtigung der vorhandenen "
                            + "Konfiguration aktiviert werden kann. "
                            + "Eine automatische Änderung erfolgt "
                            + "nicht.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Recommendation
                    }
                },

            SecurityState.NotSupported =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Secure Boot nicht unterstützt",

                        Description =
                            "Das Gerät wurde im klassischen "
                            + "BIOS-Modus gestartet und unterstützt "
                            + "Secure Boot in diesem Zustand nicht. "
                            + "Dies wird bei älteren Geräten nicht "
                            + "als unmittelbarer Fehler bewertet.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information
                    }
                },

            _ =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Title =
                            "Secure-Boot-Status nicht ermittelbar",

                        Description =
                            "Der Zustand von Secure Boot konnte "
                            + "nicht zuverlässig bestimmt werden. "
                            + "Das bedeutet nicht automatisch, dass "
                            + "die Funktion deaktiviert oder nicht "
                            + "unterstützt ist.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information
                    }
                }
        };
    }
}