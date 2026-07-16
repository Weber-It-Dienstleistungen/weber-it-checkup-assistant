using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class StorageAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var findings =
            new List<CheckupFinding>();

        var storageInformation =
            checkupSession.StorageInformation;

        AddAnalysisFinding(
            storageInformation,
            findings);

        var assessedDrives =
            GetAssessedDrives(
                storageInformation,
                findings);

        AddPhysicalHealthFindings(
            assessedDrives,
            findings);

        AddDriveTypeFinding(
            assessedDrives,
            findings);

        AddVolumeSpaceFindings(
            storageInformation.Volumes,
            findings);

        return findings;
    }

    private static void AddAnalysisFinding(
        StorageInformation storageInformation,
        ICollection<CheckupFinding> findings)
    {
        if (storageInformation.IsAnalysisSuccessful)
        {
            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Title =
                    "Datenträgeranalyse unvollständig",

                Description =
                    string.IsNullOrWhiteSpace(
                        storageInformation.AnalysisMessage)
                            ? "Die physischen Datenträger konnten "
                              + "nicht vollständig ausgewertet werden."
                            : storageInformation.AnalysisMessage,

                Category =
                    FindingCategory.Storage,

                Severity =
                    FindingSeverity.Warning
            });
    }

    private static List<PhysicalDriveInformation>
        GetAssessedDrives(
            StorageInformation storageInformation,
            ICollection<CheckupFinding> findings)
    {
        var detectedDrives =
            storageInformation.PhysicalDrives;

        if (detectedDrives.Count == 0)
        {
            findings.Add(
                new CheckupFinding
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
                });

            return new List<PhysicalDriveInformation>();
        }

        var portableApplicationDrives =
            detectedDrives
                .Where(
                    drive =>
                        drive.IsPortableApplicationDrive)
                .ToList();

        if (portableApplicationDrives.Count > 0)
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "Portabler Programmdatenträger erkannt",

                    Description =
                        portableApplicationDrives.Count == 1
                            ? "Der USB-Datenträger, von dem die "
                              + "Anwendung ausgeführt wird, wurde "
                              + "erkannt und von der Bewertung "
                              + "des Kundenrechners ausgeschlossen."
                            : "Die USB-Datenträger, von denen die "
                              + "Anwendung ausgeführt wird, wurden "
                              + "erkannt und von der Bewertung "
                              + "des Kundenrechners ausgeschlossen.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                });
        }

        var assessedDrives =
            detectedDrives
                .Where(
                    drive =>
                        !drive.IsExcludedFromAssessment)
                .ToList();

        if (assessedDrives.Count == 0)
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "Kein Kundendatenträger bewertbar",

                    Description =
                        "Nach dem Ausschluss des portablen "
                        + "Programmdatenträgers und virtueller "
                        + "Datenträger blieb kein physischer "
                        + "Kundendatenträger zur Bewertung übrig.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                });
        }

        return assessedDrives;
    }

    private static void AddPhysicalHealthFindings(
        IEnumerable<PhysicalDriveInformation> drives,
        ICollection<CheckupFinding> findings)
    {
        foreach (var drive in drives)
        {
            switch (drive.HealthStatus)
            {
                case StorageHealthStatus.Critical:
                    findings.Add(
                        new CheckupFinding
                        {
                            Title =
                                "Kritischer Datenträgerzustand erkannt",

                            Description =
                                BuildDriveDescription(
                                    drive,
                                    drive.HealthDetails),

                            Category =
                                FindingCategory.Storage,

                            Severity =
                                FindingSeverity.Critical
                        });
                    break;

                case StorageHealthStatus.Warning:
                    findings.Add(
                        new CheckupFinding
                        {
                            Title =
                                "Datenträgerwarnung erkannt",

                            Description =
                                BuildDriveDescription(
                                    drive,
                                    drive.HealthDetails),

                            Category =
                                FindingCategory.Storage,

                            Severity =
                                FindingSeverity.Warning
                        });
                    break;

                case StorageHealthStatus.Unknown:
                    findings.Add(
                        new CheckupFinding
                        {
                            Title =
                                "Datenträgerzustand nicht auswertbar",

                            Description =
                                BuildDriveDescription(
                                    drive,
                                    string.IsNullOrWhiteSpace(
                                        drive.HealthDetails)
                                            ? "Der physische Zustand "
                                              + "konnte nicht eindeutig "
                                              + "bestimmt werden."
                                            : drive.HealthDetails),

                            Category =
                                FindingCategory.Storage,

                            Severity =
                                FindingSeverity.Information
                        });
                    break;

                case StorageHealthStatus.NotSupported:
                    findings.Add(
                        new CheckupFinding
                        {
                            Title =
                                "Keine Gesundheitsdaten verfügbar",

                            Description =
                                BuildDriveDescription(
                                    drive,
                                    string.IsNullOrWhiteSpace(
                                        drive.HealthDetails)
                                            ? "Windows stellt für diesen "
                                              + "Datenträger keine "
                                              + "Gesundheitsdaten bereit."
                                            : drive.HealthDetails),

                            Category =
                                FindingCategory.Storage,

                            Severity =
                                FindingSeverity.Information
                        });
                    break;
            }
        }
    }

    private static void AddDriveTypeFinding(
        IReadOnlyCollection<PhysicalDriveInformation> drives,
        ICollection<CheckupFinding> findings)
    {
        if (drives.Count == 0)
        {
            return;
        }

        var drivesForPerformanceAssessment =
            drives
                .Where(
                    drive =>
                        drive.IsSystemDrive)
                .ToList();

        if (drivesForPerformanceAssessment.Count == 0)
        {
            drivesForPerformanceAssessment =
                drives.ToList();
        }

        if (drivesForPerformanceAssessment.Any(
                drive =>
                    drive.MediaType
                        == StorageMediaType.Hdd
                    || drive.DriveType.Contains(
                        "HDD",
                        StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "HDD als relevanter Datenträger erkannt",

                    Description =
                        "Mindestens ein für das System relevantes "
                        + "Laufwerk ist eine klassische Festplatte. "
                        + "Eine SSD-Aufrüstung kann die wahrgenommene "
                        + "Systemleistung deutlich verbessern. "
                        + "Eine HDD ist deshalb nicht automatisch defekt.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Recommendation
                });

            return;
        }

        if (drivesForPerformanceAssessment.Any(
                drive =>
                    drive.BusType
                        == StorageBusType.Nvme))
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "NVMe-SSD erkannt",

                    Description =
                        "Für das System wurde mindestens "
                        + "eine schnelle NVMe-SSD erkannt. "
                        + "Diese Aussage betrifft die "
                        + "Laufwerksart, nicht dessen Restlebensdauer.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                });

            return;
        }

        if (drivesForPerformanceAssessment.Any(
                drive =>
                    drive.MediaType
                        == StorageMediaType.Ssd
                    || drive.DriveType.Contains(
                        "SSD",
                        StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "SSD erkannt",

                    Description =
                        "Für das System wurde mindestens "
                        + "ein SSD-Laufwerk erkannt. "
                        + "Die Laufwerksart allein belegt "
                        + "keinen einwandfreien Gesundheitszustand.",

                    Category =
                        FindingCategory.Storage,

                    Severity =
                        FindingSeverity.Information
                });

            return;
        }

        findings.Add(
            new CheckupFinding
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
            });
    }

    private static void AddVolumeSpaceFindings(
        IEnumerable<VolumeInformation> volumes,
        ICollection<CheckupFinding> findings)
    {
        foreach (var volume in volumes)
        {
            if (!ShouldAssessVolumeSpace(volume))
            {
                continue;
            }

            var freeSpacePercent =
                volume.FreeSpacePercent;

            if (!freeSpacePercent.HasValue)
            {
                continue;
            }

            if (volume.IsSystemVolume
                && IsCriticallyLowSystemSpace(
                    volume,
                    freeSpacePercent.Value))
            {
                findings.Add(
                    new CheckupFinding
                    {
                        Title =
                            "Systemlaufwerk hat sehr wenig freien Speicher",

                        Description =
                            $"{volume.DriveLetter} verfügt nur noch über "
                            + $"{volume.FreeSpace}. Ein sehr knappes "
                            + "Systemlaufwerk kann Windows-Updates, "
                            + "temporäre Dateien und den stabilen "
                            + "Systembetrieb beeinträchtigen.",

                        Category =
                            FindingCategory.Storage,

                        Severity =
                            FindingSeverity.Warning
                    });

                continue;
            }

            if (freeSpacePercent.Value < 10d)
            {
                findings.Add(
                    new CheckupFinding
                    {
                        Title =
                            "Volume fast voll",

                        Description =
                            $"{volume.DriveLetter} verfügt nur noch über "
                            + $"{volume.FreeSpace}. Der geringe freie "
                            + "Speicher sollte geprüft werden.",

                        Category =
                            FindingCategory.Storage,

                        Severity =
                            FindingSeverity.Recommendation
                    });
            }
        }
    }

    private static bool ShouldAssessVolumeSpace(
        VolumeInformation volume)
    {
        if (!volume.IsReady
            || !volume.TotalSizeBytes.HasValue
            || volume.TotalSizeBytes.Value == 0)
        {
            return false;
        }

        return volume.DriveType.StartsWith(
            "Fixed",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCriticallyLowSystemSpace(
        VolumeInformation volume,
        double freeSpacePercent)
    {
        const ulong twentyGigabytes =
            20UL * 1000UL * 1000UL * 1000UL;

        return freeSpacePercent < 10d
               || !volume.FreeSpaceBytes.HasValue
               || volume.FreeSpaceBytes.Value
                   < twentyGigabytes;
    }

    private static string BuildDriveDescription(
        PhysicalDriveInformation drive,
        string details)
    {
        var driveName =
            string.IsNullOrWhiteSpace(drive.Model)
                ? "Unbekannter Datenträger"
                : drive.Model;

        var diskDescription =
            drive.DiskNumber.HasValue
                ? $"Datenträger {drive.DiskNumber.Value}"
                : "Datenträger ohne eindeutige Nummer";

        return $"{diskDescription} ({driveName}): "
               + details;
    }
}