using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class AntivirusAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var securityInformation =
            checkupSession.SecurityInformation;

        var products =
            securityInformation.AntivirusProducts;

        var findings =
            new List<CheckupFinding>();

        if (products.Count == 0)
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        securityInformation.AntivirusStatus
                        == SecurityState.Disabled
                            ? "system.security.antivirus-missing-disabled"
                            : "system.security.antivirus-not-registered",

                    Title =
                        "Kein Virenschutzprodukt registriert",

                    Description =
                        "Windows meldet derzeit kein registriertes "
                        + "Antivirenprodukt. Es sollte in der "
                        + "Windows-Sicherheit geprüft werden, ob "
                        + "Microsoft Defender oder ein anderes "
                        + "Virenschutzprodukt aktiv ist.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        securityInformation.AntivirusStatus
                        == SecurityState.Disabled
                            ? FindingSeverity.Critical
                            : FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.antivirus-protection"
                });

            return findings;
        }

        var productNames =
            string.Join(
                ", ",
                products.Select(
                    product => product.DisplayName));

        switch (securityInformation.AntivirusStatus)
        {
            case SecurityState.Enabled:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.security.antivirus-enabled",

                        Title =
                            "Virenschutz aktiv",

                        Description =
                            "Windows meldet einen ordnungsgemäßen "
                            + "Virenschutzstatus. Registriert: "
                            + $"{productNames}.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.antivirus-protection"
                    });
                break;

            case SecurityState.Disabled:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.security.antivirus-disabled",

                        Title =
                            "Virenschutz prüfen",

                        Description =
                            "Windows meldet beim Virenschutz "
                            + "Handlungsbedarf. Registriert: "
                            + $"{productNames}. Der Status sollte "
                            + "direkt in der Windows-Sicherheit "
                            + "beziehungsweise im genannten "
                            + "Virenschutzprodukt geprüft werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Critical,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.antivirus-protection"
                    });
                break;

            default:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.security.antivirus-not-evaluable",

                        Title =
                            "Virenschutzstatus nicht eindeutig",

                        Description =
                            "Als Virenschutz registriert: "
                            + $"{productNames}. Der aktuelle "
                            + "Schutzstatus konnte von Windows "
                            + "jedoch nicht eindeutig bestätigt "
                            + "werden und sollte manuell geprüft "
                            + "werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.InformationOnly,

                        CauseGroup =
                            "system.security.antivirus-data-quality"
                    });
                break;
        }

        if (products.Count > 1)
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.security.multiple-antivirus-products",

                    Title =
                        "Mehrere Virenschutzprodukte registriert",

                    Description =
                        "Windows führt mehrere Produkte: "
                        + $"{productNames}. Das ist nicht "
                        + "automatisch fehlerhaft, weil Microsoft "
                        + "Defender bei einem Drittanbieterprodukt "
                        + "passiv sein kann. Die registrierten "
                        + "Produkte sollten dennoch auf veraltete "
                        + "oder unvollständig entfernte Einträge "
                        + "geprüft werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Recommendation,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.antivirus-registration"
                });
        }

        return findings;
    }
}