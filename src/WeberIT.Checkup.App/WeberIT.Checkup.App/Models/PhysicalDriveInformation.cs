using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class PhysicalDriveInformation :
    DriveInformation
{
    public string DeviceId { get; set; } =
        string.Empty;

    public int? DiskNumber { get; set; }

    public string SerialNumber { get; set; } =
        string.Empty;

    public ulong? CapacityBytes { get; set; }

    public StorageMediaType MediaType { get; set; } =
        StorageMediaType.Unknown;

    public StorageBusType BusType { get; set; } =
        StorageBusType.Unknown;

    public StorageHealthStatus HealthStatus { get; set; } =
        StorageHealthStatus.Unknown;

    public string HealthDetails { get; set; } =
        string.Empty;

    public bool IsSystemDrive { get; set; }

    public bool IsApplicationDrive { get; set; }

    public bool IsVirtual { get; set; }

    [JsonIgnore]
    public bool IsExcludedFromAssessment =>
        IsApplicationDrive || IsVirtual;
}