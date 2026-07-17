using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class TpmAssessmentRule : ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var tpmStatus =
            checkupSession.HardwareInformation.TpmStatus;

        var tpmVersion =
            checkupSession.HardwareInformation.TpmVersion;

        if (IsStatus(
                tpmStatus,
                "Vorhanden, aber nicht aktiv"))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.tpm-inactive",

                    Title =
                        "TPM nicht aktiv",

                    Description =
                        "Ein Trusted Platform Module wurde erkannt, "
                        + "ist jedoch nicht vollständig aktiviert. "
                        + "Die TPM-Einstellungen sollten im UEFI beziehungsweise BIOS geprüft werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.tpm-configuration"
                }
            };
        }

        if (!IsStatus(
                tpmStatus,
                "Aktiv"))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.tpm.not-detected",

                    Title =
                        "TPM nicht erkannt",

                    Description =
                        "Es wurde kein aktives Trusted Platform Module erkannt. "
                        + "Möglicherweise besitzt das Gerät kein TPM oder der Status "
                        + "konnte von Windows nicht ausgelesen werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "hardware.tpm.data-quality"
                }
            };
        }

        if (IsTpmVersion2(tpmVersion))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.tpm.version-2-active",

                    Title =
                        "TPM 2.0 aktiv",

                    Description =
                        "Ein aktives Trusted Platform Module in Version 2.0 wurde erkannt.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.HardwareCondition,

                    CauseGroup =
                        "hardware.tpm.capability"
                }
            };
        }

        if (IsUnknownVersion(tpmVersion))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.tpm.version-not-evaluable",

                    Title =
                        "TPM aktiv",

                    Description =
                        "Ein aktives Trusted Platform Module wurde erkannt. "
                        + "Die genaue TPM-Version konnte jedoch nicht zuverlässig bestimmt werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "hardware.tpm.data-quality"
                }
            };
        }

        return new List<CheckupFinding>
        {
            new()
            {
                Code =
                    "hardware.tpm.older-version",

                Title =
                    "Ältere TPM-Version erkannt",

                Description =
                    $"Das Trusted Platform Module ist aktiv, meldet jedoch die Version "
                    + $"\"{tpmVersion}\". TPM 2.0 sollte für aktuelle Sicherheitsfunktionen "
                    + "und moderne Windows-Anforderungen bevorzugt werden.",

                Category =
                    FindingCategory.Security,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.HardwareCondition,

                CauseGroup =
                    "hardware.tpm.capability"
            }
        };
    }

    private static bool IsStatus(
        string? actualStatus,
        string expectedStatus)
    {
        return actualStatus?.Equals(
                   expectedStatus,
                   StringComparison.OrdinalIgnoreCase)
               == true;
    }

    private static bool IsTpmVersion2(string? tpmVersion)
    {
        return !string.IsNullOrWhiteSpace(tpmVersion)
               && tpmVersion.Contains(
                   "2.0",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnknownVersion(string? tpmVersion)
    {
        return string.IsNullOrWhiteSpace(tpmVersion)
               || tpmVersion.Equals(
                   "Unbekannt",
                   StringComparison.OrdinalIgnoreCase);
    }
}