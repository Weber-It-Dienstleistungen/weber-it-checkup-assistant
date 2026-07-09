namespace WeberIT.Checkup.App.Models;

public class StorageInformation
{
    public List<DriveInformation> PhysicalDrives { get; set; } = new();

    public List<VolumeInformation> Volumes { get; set; } = new();
}