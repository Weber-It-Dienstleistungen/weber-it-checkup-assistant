using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class DriveEncryptionAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var encryption =
            checkupSession
                .SecurityInformation
                .SystemDriveEncryption;

        var deviceType =
            checkupSession
                .DeviceInformation
                .DeviceType
                ?.Trim()
            ?? string.Empty;

        if (encryption.ProtectionState
            == SecurityState.NotSupported)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.drive-encryption-not-supported",

                    Title =
                        "Laufwerksverschlüsselung nicht verfügbar",

                    Description =
                        "Für das Windows-Systemlaufwerk konnte "
                        + "keine unterstützte BitLocker- oder "
                        + "Geräteverschlüsselungsschnittstelle "
                        + "gefunden werden. Das kann von der "
                        + "Windows-Ausgabe und der Hardware "
                        + "abhängen.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.security.drive-encryption-data-quality"
                }
            };
        }

        if (encryption.ProtectionState
            == SecurityState.Unknown)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.drive-encryption-not-evaluable",

                    Title =
                        "Laufwerksverschlüsselung nicht auswertbar",

                    Description =
                        $"Der Schutzstatus des "
                        + $"Systemlaufwerks "
                        + $"{BuildDriveDescription(encryption)} "
                        + "konnte nicht zuverlässig ermittelt "
                        + "werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.security.drive-encryption-data-quality"
                }
            };
        }

        if (encryption.ProtectionState
            == SecurityState.Enabled)
        {
            if (IsConversionState(
                    encryption,
                    "Verschlüsselung läuft"))
            {
                return new List<CheckupFinding>
                {
                    new()
                    {
                        Code =
                            "system.security.drive-encryption-running",

                        Title =
                            "Laufwerksverschlüsselung läuft",

                        Description =
                            $"Das Systemlaufwerk "
                            + $"{BuildDriveDescription(encryption)} "
                            + "wird derzeit verschlüsselt. Der "
                            + "Vorgang sollte bis zum Abschluss "
                            + "nicht unterbrochen werden.",

                        Category =
                            FindingCategory.Security,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.security.drive-encryption-protection"
                    }
                };
            }

            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.drive-encryption-enabled",

                    Title =
                        "Systemlaufwerk geschützt",

                    Description =
                        $"Das Systemlaufwerk "
                        + $"{BuildDriveDescription(encryption)} "
                        + "ist durch BitLocker beziehungsweise "
                        + "die Windows-Geräteverschlüsselung "
                        + "geschützt.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.drive-encryption-protection"
                }
            };
        }

        if (IsConversionState(
                encryption,
                "Verschlüsselung läuft")
            || IsConversionState(
                encryption,
                "Verschlüsselung angehalten"))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.drive-encryption-incomplete",

                    Title =
                        "Laufwerksverschlüsselung nicht vollständig geschützt",

                    Description =
                        $"Das Systemlaufwerk "
                        + $"{BuildDriveDescription(encryption)} "
                        + "befindet sich im Zustand "
                        + $"„{encryption.ConversionStatus}“, "
                        + "der Schutz ist jedoch noch nicht aktiv. "
                        + "Der Verschlüsselungsstatus sollte "
                        + "geprüft werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.drive-encryption-protection"
                }
            };
        }

        var isMobileDevice =
            deviceType.Contains(
                "Notebook",
                StringComparison.OrdinalIgnoreCase)
            || deviceType.Contains(
                "Tablet",
                StringComparison.OrdinalIgnoreCase);

        return new List<CheckupFinding>
        {
            new()
            {
                Code =
                    isMobileDevice
                        ? "system.security.mobile-drive-not-encrypted"
                        : "system.security.stationary-drive-not-encrypted",

                Title =
                    "Systemlaufwerk nicht verschlüsselt",

                Description =
                    isMobileDevice
                        ? $"Das Systemlaufwerk "
                          + $"{BuildDriveDescription(encryption)} "
                          + "ist nicht geschützt. Bei einem "
                          + "mobilen Gerät sollte BitLocker "
                          + "beziehungsweise die "
                          + "Geräteverschlüsselung geprüft werden, "
                          + "da bei Verlust oder Diebstahl ein "
                          + "erhöhtes Risiko für die gespeicherten "
                          + "Daten besteht."
                        : $"Das Systemlaufwerk "
                          + $"{BuildDriveDescription(encryption)} "
                          + "ist nicht verschlüsselt. Bei einem "
                          + "stationären Privat-PC wird dies "
                          + "zunächst nur als Hinweis bewertet. "
                          + "Abhängig von den gespeicherten Daten "
                          + "kann eine Verschlüsselung dennoch "
                          + "sinnvoll sein.",

                Category =
                    FindingCategory.Security,

                Severity =
                    isMobileDevice
                        ? FindingSeverity.Recommendation
                        : FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.security.drive-encryption-protection"
            }
        };
    }

    private static bool IsConversionState(
        DriveEncryptionInformation encryption,
        string expectedStatus)
    {
        return encryption.ConversionStatus.Equals(
            expectedStatus,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDriveDescription(
        DriveEncryptionInformation encryption)
    {
        var driveLetter =
            string.IsNullOrWhiteSpace(
                encryption.DriveLetter)
                ? "Windows-Laufwerk"
                : encryption.DriveLetter;

        if (!encryption.EncryptionPercentage.HasValue)
        {
            return driveLetter;
        }

        return $"{driveLetter} "
               + $"({encryption.EncryptionPercentage.Value} %)";
    }
}