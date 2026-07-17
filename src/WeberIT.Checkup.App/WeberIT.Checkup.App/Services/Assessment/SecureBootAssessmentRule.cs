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
                        Code =
                            "system.security.secure-boot-enabled",

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
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.secure-boot-configuration"
                    }
                },

            SecurityState.Disabled =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.secure-boot-disabled",

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
                            FindingSeverity.Recommendation,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.secure-boot-configuration"
                    }
                },

            SecurityState.NotSupported =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "hardware.secure-boot.not-supported",

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
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.InformationOnly,

                        CauseGroup =
                            "hardware.secure-boot.data-quality"
                    }
                },

            _ =>
                new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.secure-boot-not-evaluable",

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
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.InformationOnly,

                        CauseGroup =
                            "system.security.secure-boot-data-quality"
                    }
                }
        };
    }
}