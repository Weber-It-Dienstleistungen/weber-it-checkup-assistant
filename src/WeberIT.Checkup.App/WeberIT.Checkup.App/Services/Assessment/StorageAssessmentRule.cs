using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class StorageAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var detectedDrives =
            checkupSession
                .StorageInformation
                .PhysicalDrives;

        if (detectedDrives.Count == 0)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title =
                        "Keine Laufwerksinformationen gefunden",

                    Description =
                        "Es konnten keine physischen "
                        + "Laufwerke ausgewertet werden.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Warning
                }
            };
        }

        var assessedDrives =
            detectedDrives
                .Where(
                    drive =>
                        !drive.IsExcludedFromAssessment)
                .ToList();

        if (assessedDrives.Count == 0)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title =
                        "Kein Kundendatenträger bewertbar",

                    Description =
                        "Die erkannten Datenträger gehören "
                        + "zum Programmlaufwerk oder wurden "
                        + "als virtuelle Datenträger eingestuft. "
                        + "Sie fließen nicht in die Bewertung ein.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                }
            };
        }

        if (assessedDrives.Any(
                drive =>
                    drive.DriveType.Contains(
                        "NVMe",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title =
                        "Schnelle NVMe-SSD erkannt",

                    Description =
                        "Das Gerät verfügt über mindestens "
                        + "ein bewertbares NVMe-Laufwerk.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                }
            };
        }

        if (assessedDrives.Any(
                drive =>
                    drive.DriveType.Contains(
                        "SSD",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title =
                        "SSD erkannt",

                    Description =
                        "Das Gerät verfügt über mindestens "
                        + "ein bewertbares SSD-Laufwerk.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                }
            };
        }

        if (assessedDrives.Any(
                drive =>
                    drive.DriveType.Contains(
                        "HDD",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title =
                        "HDD erkannt",

                    Description =
                        "Es wurde mindestens eine klassische "
                        + "Festplatte erkannt. Eine SSD-Aufrüstung "
                        + "kann die Systemleistung deutlich verbessern.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Recommendation
                }
            };
        }

        return new List<CheckupFinding>
        {
            new()
            {
                Title =
                    "Laufwerkstyp nicht eindeutig erkannt",

                Description =
                    "Der Laufwerkstyp der bewertbaren "
                    + "Kundendatenträger konnte nicht "
                    + "eindeutig bestimmt werden.",

                Category =
                    FindingCategory.Storage,

                Severity =
                    FindingSeverity.Information
            }
        };
    }
}