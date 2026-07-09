using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class StorageAssessmentRule : ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(CheckupSession checkupSession)
    {
        var drives = checkupSession.HardwareInformation.Drives;

        if (drives.Count == 0)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Keine Laufwerksinformationen gefunden",
                    Description = "Es konnten keine physischen Laufwerke ausgewertet werden.",
                    Category = FindingCategory.Storage,
                    Severity = FindingSeverity.Warning
                }
            };
        }

        if (drives.Any(drive => drive.DriveType.Contains("NVMe", StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Schnelle NVMe-SSD erkannt",
                    Description = "Das Gerät verfügt über mindestens ein sehr schnelles NVMe-Laufwerk.",
                    Category = FindingCategory.Storage,
                    Severity = FindingSeverity.Information
                }
            };
        }

        if (drives.Any(drive => drive.DriveType.Contains("SSD", StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "SSD erkannt",
                    Description = "Das Gerät verfügt über mindestens ein SSD-Laufwerk.",
                    Category = FindingCategory.Storage,
                    Severity = FindingSeverity.Information
                }
            };
        }

        if (drives.Any(drive => drive.DriveType.Contains("HDD", StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "HDD erkannt",
                    Description = "Es wurde mindestens eine klassische Festplatte erkannt. Eine SSD-Aufrüstung kann die Systemleistung deutlich verbessern.",
                    Category = FindingCategory.Storage,
                    Severity = FindingSeverity.Recommendation
                }
            };
        }

        return new List<CheckupFinding>
        {
            new()
            {
                Title = "Laufwerkstyp nicht eindeutig erkannt",
                Description = "Der Laufwerkstyp konnte nicht eindeutig bewertet werden.",
                Category = FindingCategory.Storage,
                Severity = FindingSeverity.Information
            }
        };
    }
}