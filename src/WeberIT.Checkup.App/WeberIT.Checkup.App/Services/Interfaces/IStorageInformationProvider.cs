using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IStorageInformationProvider
{
    List<PhysicalDriveInformation> GetPhysicalDrives();

    List<VolumeInformation> GetVolumes();
}