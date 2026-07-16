namespace WeberIT.Checkup.App.Models;

public class StorageInformation
{
    public bool IsAnalysisSuccessful { get; set; } =
        true;

    public string AnalysisMessage { get; set; } =
        string.Empty;

    public List<PhysicalDriveInformation> PhysicalDrives
    {
        get;
        set;
    } = new();

    public List<VolumeInformation> Volumes { get; set; } =
        new();
}