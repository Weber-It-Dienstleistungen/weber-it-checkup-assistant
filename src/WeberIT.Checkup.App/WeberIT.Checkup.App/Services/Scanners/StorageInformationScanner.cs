using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class StorageInformationScanner :
    IStorageInformationScanner
{
    private readonly IStorageInformationProvider
        _storageInformationProvider;

    public StorageInformationScanner(
        IStorageInformationProvider storageInformationProvider)
    {
        _storageInformationProvider =
            storageInformationProvider;
    }

    public ScanResult<StorageInformation> Scan()
    {
        var physicalDrives =
            _storageInformationProvider
                .GetPhysicalDrives();

        var volumes =
            _storageInformationProvider
                .GetVolumes();

        ApplyVolumeRolesToPhysicalDrives(
            physicalDrives,
            volumes);

        var storageInformation =
            new StorageInformation
            {
                PhysicalDrives =
                    physicalDrives,

                Volumes =
                    volumes,

                IsAnalysisSuccessful =
                    physicalDrives.Count > 0,

                AnalysisMessage =
                    BuildAnalysisMessage(
                        physicalDrives,
                        volumes)
            };

        return new ScanResult<StorageInformation>
        {
            IsSuccessful =
                storageInformation
                    .IsAnalysisSuccessful,

            Data =
                storageInformation
        };
    }

    private static void ApplyVolumeRolesToPhysicalDrives(
        IEnumerable<PhysicalDriveInformation> physicalDrives,
        IEnumerable<VolumeInformation> volumes)
    {
        foreach (var physicalDrive in physicalDrives)
        {
            if (!physicalDrive.DiskNumber.HasValue)
            {
                continue;
            }

            var assignedVolumes =
                volumes
                    .Where(
                        volume =>
                            volume.PhysicalDiskNumber
                                == physicalDrive.DiskNumber)
                    .ToList();

            physicalDrive.IsSystemDrive =
                assignedVolumes.Any(
                    volume =>
                        volume.IsSystemVolume);

            physicalDrive.IsApplicationDrive =
                assignedVolumes.Any(
                    volume =>
                        volume.IsApplicationVolume);
        }
    }

    private static string BuildAnalysisMessage(
        IReadOnlyCollection<PhysicalDriveInformation>
            physicalDrives,
        IReadOnlyCollection<VolumeInformation>
            volumes)
    {
        if (physicalDrives.Count == 0)
        {
            return "Es konnten keine physischen "
                   + "Datenträger ermittelt werden.";
        }

        var readyVolumes =
            volumes
                .Where(volume => volume.IsReady)
                .ToList();

        var unassignedVolumeCount =
            readyVolumes.Count(
                volume =>
                    !volume.PhysicalDiskNumber.HasValue);

        if (unassignedVolumeCount > 0)
        {
            return $"{unassignedVolumeCount} bereite Volumes "
                   + "konnten keinem physischen Datenträger "
                   + "eindeutig zugeordnet werden.";
        }

        return "Physische Datenträger und bereite Volumes "
               + "wurden erfolgreich ermittelt und zugeordnet.";
    }
}