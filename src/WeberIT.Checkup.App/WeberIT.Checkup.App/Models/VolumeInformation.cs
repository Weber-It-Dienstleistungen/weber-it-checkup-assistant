namespace WeberIT.Checkup.App.Models;

public class VolumeInformation
{
    public string Name { get; set; } = string.Empty;

    public string DriveLetter { get; set; } = string.Empty;

    public string DriveType { get; set; } = string.Empty;

    public string FileSystem { get; set; } = string.Empty;

    public string TotalSize { get; set; } = string.Empty;

    public string FreeSpace { get; set; } = string.Empty;

    public bool IsReady { get; set; }
}