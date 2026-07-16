using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class VolumeInformation
{
    public string Name { get; set; } =
        string.Empty;

    public string DriveLetter { get; set; } =
        string.Empty;

    public string DriveType { get; set; } =
        string.Empty;

    public string FileSystem { get; set; } =
        string.Empty;

    public string TotalSize { get; set; } =
        string.Empty;

    public string FreeSpace { get; set; } =
        string.Empty;

    public ulong? TotalSizeBytes { get; set; }

    public ulong? FreeSpaceBytes { get; set; }

    public int? PhysicalDiskNumber { get; set; }

    public bool IsReady { get; set; }

    public bool IsSystemVolume { get; set; }

    public bool IsApplicationVolume { get; set; }

    [JsonIgnore]
    public double? FreeSpacePercent
    {
        get
        {
            if (!TotalSizeBytes.HasValue
                || !FreeSpaceBytes.HasValue
                || TotalSizeBytes.Value == 0)
            {
                return null;
            }

            return FreeSpaceBytes.Value
                   * 100d
                   / TotalSizeBytes.Value;
        }
    }

    [JsonIgnore]
    public bool IsExcludedFromAssessment =>
        IsApplicationVolume;
}