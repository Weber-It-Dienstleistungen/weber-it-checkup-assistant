using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IStorageInformationProvider
{
    List<DriveInformation> GetPhysicalDrives();

    List<VolumeInformation> GetVolumes();
}