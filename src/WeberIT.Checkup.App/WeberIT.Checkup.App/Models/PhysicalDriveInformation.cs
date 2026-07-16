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
    public bool IsPortableApplicationDrive =>
        IsApplicationDrive
        && BusType == StorageBusType.Usb;

    [JsonIgnore]
    public bool IsExcludedFromAssessment =>
        IsPortableApplicationDrive
        || IsVirtual;

    [JsonIgnore]
    public string DiskNumberText =>
        DiskNumber.HasValue
            ? $"Datenträger {DiskNumber.Value}"
            : "Datenträgernummer unbekannt";

    [JsonIgnore]
    public string MediaTypeText =>
        MediaType switch
        {
            StorageMediaType.Hdd =>
                "HDD",

            StorageMediaType.Ssd =>
                "SSD",

            StorageMediaType.Unspecified =>
                "Nicht spezifiziert",

            _ =>
                "Unbekannt"
        };

    [JsonIgnore]
    public string BusTypeText =>
        BusType switch
        {
            StorageBusType.Scsi =>
                "SCSI",

            StorageBusType.Atapi =>
                "ATAPI",

            StorageBusType.Ata =>
                "ATA",

            StorageBusType.FireWire =>
                "FireWire",

            StorageBusType.Ssa =>
                "SSA",

            StorageBusType.FibreChannel =>
                "Fibre Channel",

            StorageBusType.Usb =>
                "USB",

            StorageBusType.Raid =>
                "RAID",

            StorageBusType.Iscsi =>
                "iSCSI",

            StorageBusType.Sas =>
                "SAS",

            StorageBusType.Sata =>
                "SATA",

            StorageBusType.Sd =>
                "SD",

            StorageBusType.Mmc =>
                "MMC",

            StorageBusType.Virtual =>
                "Virtuell",

            StorageBusType.FileBackedVirtual =>
                "Dateibasiert virtuell",

            StorageBusType.StorageSpaces =>
                "Storage Spaces",

            StorageBusType.Nvme =>
                "NVMe",

            _ =>
                "Unbekannt"
        };

    [JsonIgnore]
    public string HealthStatusText =>
        HealthStatus switch
        {
            StorageHealthStatus.Healthy =>
                "Keine Warnung erkannt",

            StorageHealthStatus.Warning =>
                "Warnung erkannt",

            StorageHealthStatus.Critical =>
                "Kritischer Zustand",

            StorageHealthStatus.NotSupported =>
                "Nicht unterstützt",

            _ =>
                "Nicht auswertbar"
        };

    [JsonIgnore]
    public string RoleText
    {
        get
        {
            var roles =
                new List<string>();

            if (IsSystemDrive)
            {
                roles.Add("Systemdatenträger");
            }

            if (IsPortableApplicationDrive)
            {
                roles.Add(
                    "Portabler Programmdatenträger");
            }
            else if (IsApplicationDrive)
            {
                roles.Add("Programmdatenträger");
            }

            if (IsVirtual)
            {
                roles.Add("Virtueller Datenträger");
            }

            return roles.Count > 0
                ? string.Join(", ", roles)
                : "Zusätzlicher Datenträger";
        }
    }
}