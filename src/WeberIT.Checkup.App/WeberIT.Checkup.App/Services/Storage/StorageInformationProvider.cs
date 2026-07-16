using System.Collections;
using System.IO;
using System.Management;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Storage;

public class StorageInformationProvider :
    IStorageInformationProvider
{
    public List<PhysicalDriveInformation>
        GetPhysicalDrives()
    {
        var drives =
            GetWin32PhysicalDrives();

        ApplyWindowsStorageInformation(
            drives);

        return drives;
    }

    public List<VolumeInformation> GetVolumes()
    {
        var volumes =
            new List<VolumeInformation>();

        var diskNumbersByDriveLetter =
            GetDiskNumbersByDriveLetter();

        var systemRoot =
            NormalizeRootPath(
                Path.GetPathRoot(
                    Environment.SystemDirectory));

        var applicationRoot =
            NormalizeRootPath(
                Path.GetPathRoot(
                    AppContext.BaseDirectory));

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var volumeRoot =
                    NormalizeRootPath(
                        drive.RootDirectory.FullName);

                var driveLetter =
                    NormalizeDriveLetter(
                        drive.Name);

                var isSystemVolume =
                    PathsAreEqual(
                        volumeRoot,
                        systemRoot);

                var isApplicationVolume =
                    PathsAreEqual(
                        volumeRoot,
                        applicationRoot);

                var isReady =
                    drive.IsReady;

                var totalSizeBytes =
                    isReady
                        ? ConvertToUnsignedLong(
                            drive.TotalSize)
                        : null;

                var freeSpaceBytes =
                    isReady
                        ? ConvertToUnsignedLong(
                            drive.AvailableFreeSpace)
                        : null;

                diskNumbersByDriveLetter.TryGetValue(
                    driveLetter,
                    out var physicalDiskNumber);

                volumes.Add(
                    new VolumeInformation
                    {
                        Name =
                            drive.Name,

                        DriveLetter =
                            driveLetter,

                        DriveType =
                            BuildVolumeTypeDescription(
                                drive.DriveType,
                                isSystemVolume,
                                isApplicationVolume),

                        FileSystem =
                            isReady
                                ? drive.DriveFormat
                                : "Unbekannt",

                        TotalSize =
                            isReady
                                ? FormatBytesAsGigabytes(
                                    totalSizeBytes)
                                : "Unbekannt",

                        FreeSpace =
                            isReady
                                ? FormatFreeSpace(
                                    freeSpaceBytes,
                                    totalSizeBytes)
                                : "Unbekannt",

                        TotalSizeBytes =
                            totalSizeBytes,

                        FreeSpaceBytes =
                            freeSpaceBytes,

                        PhysicalDiskNumber =
                            physicalDiskNumber,

                        IsReady =
                            isReady,

                        IsSystemVolume =
                            isSystemVolume,

                        IsApplicationVolume =
                            isApplicationVolume
                    });
            }
        }
        catch
        {
            // Volume-Abfrage darf den Scan
            // nicht abbrechen.
        }

        return volumes;
    }

    private static List<PhysicalDriveInformation>
        GetWin32PhysicalDrives()
    {
        var drives =
            new List<PhysicalDriveInformation>();

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT DeviceID, Index, Model, "
                    + "Manufacturer, SerialNumber, Size, "
                    + "MediaType, InterfaceType, PNPDeviceID "
                    + "FROM Win32_DiskDrive");

            foreach (var result in searcher.Get())
            {
                var deviceId =
                    GetStringValue(
                        result,
                        "DeviceID");

                var model =
                    GetStringValue(
                        result,
                        "Model",
                        "Unbekannt");

                var manufacturer =
                    GetStringValue(
                        result,
                        "Manufacturer",
                        "Unbekannt");

                var serialNumber =
                    GetStringValue(
                        result,
                        "SerialNumber");

                var sizeValue =
                    GetStringValue(
                        result,
                        "Size");

                var legacyMediaType =
                    GetStringValue(
                        result,
                        "MediaType");

                var interfaceType =
                    GetStringValue(
                        result,
                        "InterfaceType");

                var pnpDeviceId =
                    GetStringValue(
                        result,
                        "PNPDeviceID");

                var capacityBytes =
                    ParseUnsignedLong(
                        sizeValue);

                drives.Add(
                    new PhysicalDriveInformation
                    {
                        DeviceId =
                            deviceId,

                        DiskNumber =
                            ParseInteger(
                                result["Index"]),

                        Model =
                            model,

                        Manufacturer =
                            manufacturer,

                        SerialNumber =
                            serialNumber,

                        CapacityBytes =
                            capacityBytes,

                        Capacity =
                            FormatBytesAsGigabytes(
                                capacityBytes),

                        DriveType =
                            DetermineLegacyDriveType(
                                model,
                                legacyMediaType,
                                interfaceType,
                                pnpDeviceId),

                        HealthStatus =
                            StorageHealthStatus.Unknown,

                        HealthDetails =
                            "Der Windows-Gesundheitsstatus "
                            + "wurde noch nicht ausgewertet."
                    });
            }
        }
        catch
        {
            // Die Abfrage darf den Scan nicht abbrechen.
        }

        return drives;
    }

    private static void ApplyWindowsStorageInformation(
        List<PhysicalDriveInformation> drives)
    {
        if (drives.Count == 0)
        {
            return;
        }

        var sourceWasAvailable =
            false;

        var matchedDiskNumbers =
            new HashSet<int>();

        try
        {
            var scope =
                new ManagementScope(
                    @"\\.\root\Microsoft\Windows\Storage");

            scope.Connect();

            using var searcher =
                new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery(
                        "SELECT DeviceId, FriendlyName, "
                        + "MediaType, BusType, HealthStatus, "
                        + "OperationalStatus "
                        + "FROM MSFT_PhysicalDisk"));

            foreach (var result in searcher.Get())
            {
                sourceWasAvailable =
                    true;

                var diskNumber =
                    ParseInteger(
                        result["DeviceId"]);

                var friendlyName =
                    GetStringValue(
                        result,
                        "FriendlyName");

                var matchingDrive =
                    FindMatchingDrive(
                        drives,
                        diskNumber,
                        friendlyName);

                if (matchingDrive is null)
                {
                    continue;
                }

                if (matchingDrive.DiskNumber.HasValue)
                {
                    matchedDiskNumbers.Add(
                        matchingDrive.DiskNumber.Value);
                }

                matchingDrive.MediaType =
                    MapMediaType(
                        result["MediaType"]);

                matchingDrive.BusType =
                    MapBusType(
                        result["BusType"]);

                matchingDrive.HealthStatus =
                    MapHealthStatus(
                        result["HealthStatus"]);

                matchingDrive.HealthDetails =
                    BuildHealthDetails(
                        matchingDrive.HealthStatus,
                        result["OperationalStatus"]);

                matchingDrive.IsVirtual =
                    IsVirtualBusType(
                        matchingDrive.BusType);

                matchingDrive.DriveType =
                    BuildPhysicalDriveType(
                        matchingDrive.MediaType,
                        matchingDrive.BusType,
                        matchingDrive.DriveType);
            }
        }
        catch
        {
            foreach (var drive in drives)
            {
                drive.HealthStatus =
                    StorageHealthStatus.Unknown;

                drive.HealthDetails =
                    "Die Windows-Storage-Quelle konnte "
                    + "nicht ausgewertet werden.";
            }

            return;
        }

        foreach (var drive in drives)
        {
            var wasMatched =
                drive.DiskNumber.HasValue
                && matchedDiskNumbers.Contains(
                    drive.DiskNumber.Value);

            if (wasMatched)
            {
                continue;
            }

            drive.HealthStatus =
                sourceWasAvailable
                    ? StorageHealthStatus.NotSupported
                    : StorageHealthStatus.Unknown;

            drive.HealthDetails =
                sourceWasAvailable
                    ? "Windows stellte für diesen Datenträger "
                      + "keine eindeutig zuordenbaren "
                      + "Gesundheitsinformationen bereit."
                    : "Windows stellte keine auswertbaren "
                      + "Storage-Gesundheitsinformationen bereit.";
        }
    }

    private static PhysicalDriveInformation?
        FindMatchingDrive(
            IEnumerable<PhysicalDriveInformation> drives,
            int? diskNumber,
            string friendlyName)
    {
        if (diskNumber.HasValue)
        {
            var driveByNumber =
                drives.FirstOrDefault(
                    drive =>
                        drive.DiskNumber
                            == diskNumber);

            if (driveByNumber is not null)
            {
                return driveByNumber;
            }
        }

        if (string.IsNullOrWhiteSpace(
                friendlyName))
        {
            return null;
        }

        return drives.FirstOrDefault(
            drive =>
                string.Equals(
                    drive.Model,
                    friendlyName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static StorageMediaType MapMediaType(
        object? value)
    {
        var numericValue =
            ParseInteger(value);

        return numericValue switch
        {
            0 => StorageMediaType.Unspecified,
            3 => StorageMediaType.Hdd,
            4 => StorageMediaType.Ssd,
            _ => StorageMediaType.Unknown
        };
    }

    private static StorageBusType MapBusType(
        object? value)
    {
        var numericValue =
            ParseInteger(value);

        if (!numericValue.HasValue
            || !Enum.IsDefined(
                typeof(StorageBusType),
                numericValue.Value))
        {
            return StorageBusType.Unknown;
        }

        return (StorageBusType)
            numericValue.Value;
    }

    private static StorageHealthStatus MapHealthStatus(
        object? value)
    {
        var numericValue =
            ParseInteger(value);

        return numericValue switch
        {
            0 => StorageHealthStatus.Healthy,
            1 => StorageHealthStatus.Warning,
            2 => StorageHealthStatus.Critical,
            _ => StorageHealthStatus.Unknown
        };
    }

    private static string BuildHealthDetails(
        StorageHealthStatus healthStatus,
        object? operationalStatus)
    {
        var operationalStatusText =
            FormatOperationalStatus(
                operationalStatus);

        var healthDescription =
            healthStatus switch
            {
                StorageHealthStatus.Healthy =>
                    "Windows meldet für den Datenträger "
                    + "keine erkannte Gesundheitswarnung.",

                StorageHealthStatus.Warning =>
                    "Windows meldet eine Warnung für "
                    + "den Datenträger.",

                StorageHealthStatus.Critical =>
                    "Windows meldet einen kritischen "
                    + "Zustand des Datenträgers.",

                StorageHealthStatus.NotSupported =>
                    "Der Gesundheitsstatus wird für "
                    + "diesen Datenträger nicht unterstützt.",

                _ =>
                    "Der Gesundheitsstatus konnte nicht "
                    + "eindeutig bestimmt werden."
            };

        if (string.IsNullOrWhiteSpace(
                operationalStatusText))
        {
            return healthDescription;
        }

        return $"{healthDescription} "
               + $"Betriebsstatus: "
               + $"{operationalStatusText}.";
    }

    private static string FormatOperationalStatus(
        object? value)
    {
        if (value is not IEnumerable values)
        {
            return string.Empty;
        }

        var statusDescriptions =
            new List<string>();

        foreach (var item in values)
        {
            var numericValue =
                ParseInteger(item);

            if (!numericValue.HasValue)
            {
                continue;
            }

            var description =
                numericValue.Value switch
                {
                    0 => "Unbekannt",
                    1 => "Sonstiger Status",
                    2 => "In Ordnung",
                    3 => "Beeinträchtigt",
                    4 => "Belastet",
                    5 => "Fehler erwartet",
                    6 => "Fehler",
                    7 => "Nicht wiederherstellbarer Fehler",
                    8 => "Wird gestartet",
                    9 => "Wird beendet",
                    10 => "Beendet",
                    11 => "In Betrieb",
                    12 => "Keine Verbindung",
                    13 => "Kommunikation verloren",
                    14 => "Abgebrochen",
                    15 => "Ruhend",
                    16 => "Unterstützende Entität fehlerhaft",
                    17 => "Abgeschlossen",
                    18 => "Energiesparmodus",
                    19 => "Wird verlagert",
                    0xD010 => "Herabgestuft",
                    0xD011 => "Unvollständig",
                    0xD012 => "Scanmodus",
                    _ => $"Status {numericValue.Value}"
                };

            statusDescriptions.Add(
                description);
        }

        return string.Join(
            ", ",
            statusDescriptions.Distinct());
    }

    private static bool IsVirtualBusType(
        StorageBusType busType)
    {
        return busType
            is StorageBusType.Virtual
            or StorageBusType.FileBackedVirtual
            or StorageBusType.StorageSpaces;
    }

    private static string BuildPhysicalDriveType(
        StorageMediaType mediaType,
        StorageBusType busType,
        string fallbackType)
    {
        if (busType == StorageBusType.Nvme)
        {
            return mediaType == StorageMediaType.Hdd
                ? "NVMe-Laufwerk"
                : "NVMe SSD";
        }

        if (mediaType == StorageMediaType.Ssd)
        {
            return busType switch
            {
                StorageBusType.Sata =>
                    "SATA SSD",

                StorageBusType.Usb =>
                    "SSD über USB",

                StorageBusType.Virtual =>
                    "Virtuelle SSD",

                StorageBusType.FileBackedVirtual =>
                    "Dateibasierte virtuelle SSD",

                StorageBusType.StorageSpaces =>
                    "Storage-Spaces-SSD",

                _ =>
                    "SSD"
            };
        }

        if (mediaType == StorageMediaType.Hdd)
        {
            return busType switch
            {
                StorageBusType.Usb =>
                    "HDD über USB",

                StorageBusType.Virtual =>
                    "Virtuelle HDD",

                StorageBusType.FileBackedVirtual =>
                    "Dateibasierte virtuelle HDD",

                StorageBusType.StorageSpaces =>
                    "Storage-Spaces-HDD",

                _ =>
                    "HDD"
            };
        }

        if (busType == StorageBusType.Usb)
        {
            return "USB-Laufwerk";
        }

        if (busType
            is StorageBusType.Virtual
            or StorageBusType.FileBackedVirtual)
        {
            return "Virtueller Datenträger";
        }

        if (busType == StorageBusType.StorageSpaces)
        {
            return "Storage-Spaces-Datenträger";
        }

        return fallbackType;
    }

    private static Dictionary<string, int?>
        GetDiskNumbersByDriveLetter()
    {
        var diskNumbersByDriveLetter =
            new Dictionary<string, int?>(
                StringComparer.OrdinalIgnoreCase);

        try
        {
            using var partitionSearcher =
                new ManagementObjectSearcher(
                    "SELECT DeviceID, DiskIndex "
                    + "FROM Win32_DiskPartition");

            foreach (ManagementObject partition
                     in partitionSearcher.Get())
            {
                var diskNumber =
                    ParseInteger(
                        partition["DiskIndex"]);

                using var logicalDisks =
                    partition.GetRelated(
                        "Win32_LogicalDisk");

                foreach (ManagementObject logicalDisk
                         in logicalDisks)
                {
                    var driveLetter =
                        NormalizeDriveLetter(
                            GetStringValue(
                                logicalDisk,
                                "DeviceID"));

                    if (!string.IsNullOrWhiteSpace(
                            driveLetter))
                    {
                        diskNumbersByDriveLetter[
                            driveLetter] =
                            diskNumber;
                    }

                    logicalDisk.Dispose();
                }
            }
        }
        catch
        {
            // Eine fehlende Zuordnung darf den Scan
            // nicht abbrechen.
        }

        return diskNumbersByDriveLetter;
    }

    private static string GetStringValue(
        ManagementBaseObject managementObject,
        string propertyName,
        string fallbackValue = "")
    {
        var value =
            managementObject[propertyName]
                ?.ToString()
                ?.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? fallbackValue
            : value;
    }

    private static ulong? ParseUnsignedLong(
        string value)
    {
        return ulong.TryParse(
            value,
            out var parsedValue)
                ? parsedValue
                : null;
    }

    private static int? ParseInteger(
        object? value)
    {
        if (value is null)
        {
            return null;
        }

        return int.TryParse(
            value.ToString(),
            out var parsedValue)
                ? parsedValue
                : null;
    }

    private static ulong? ConvertToUnsignedLong(
        long value)
    {
        return value >= 0
            ? Convert.ToUInt64(value)
            : null;
    }

    private static string FormatBytesAsGigabytes(
        ulong? bytes)
    {
        if (!bytes.HasValue)
        {
            return "Unbekannt";
        }

        var gigabytes =
            bytes.Value
            / 1000d
            / 1000d
            / 1000d;

        return $"{gigabytes:0.#} GB";
    }

    private static string FormatFreeSpace(
        ulong? freeSpaceBytes,
        ulong? totalSizeBytes)
    {
        var formattedFreeSpace =
            FormatBytesAsGigabytes(
                freeSpaceBytes);

        if (!freeSpaceBytes.HasValue
            || !totalSizeBytes.HasValue
            || totalSizeBytes.Value == 0)
        {
            return formattedFreeSpace;
        }

        var freeSpacePercent =
            freeSpaceBytes.Value
            * 100d
            / totalSizeBytes.Value;

        return $"{formattedFreeSpace} "
               + $"({freeSpacePercent:0.#} % frei)";
    }

    private static string BuildVolumeTypeDescription(
        DriveType driveType,
        bool isSystemVolume,
        bool isApplicationVolume)
    {
        var roles =
            new List<string>();

        if (isSystemVolume)
        {
            roles.Add("Systemvolume");
        }

        if (isApplicationVolume)
        {
            roles.Add("Programmlaufwerk");
        }

        if (roles.Count == 0)
        {
            return driveType.ToString();
        }

        return $"{driveType} "
               + $"({string.Join(", ", roles)})";
    }

    private static string NormalizeDriveLetter(
        string? driveName)
    {
        if (string.IsNullOrWhiteSpace(driveName))
        {
            return string.Empty;
        }

        return driveName
            .Replace("\\", string.Empty)
            .Trim()
            .ToUpperInvariant();
    }

    private static string NormalizeRootPath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim()
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        return !string.IsNullOrWhiteSpace(firstPath)
               && !string.IsNullOrWhiteSpace(secondPath)
               && string.Equals(
                   firstPath,
                   secondPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineLegacyDriveType(
        string model,
        string mediaType,
        string interfaceType,
        string pnpDeviceId)
    {
        var combinedInformation =
            $"{model} {mediaType} "
            + $"{interfaceType} {pnpDeviceId}";

        if (combinedInformation.Contains(
                "NVMe",
                StringComparison.OrdinalIgnoreCase))
        {
            return "NVMe SSD";
        }

        if (combinedInformation.Contains(
                "SSD",
                StringComparison.OrdinalIgnoreCase)
            || combinedInformation.Contains(
                "Solid State",
                StringComparison.OrdinalIgnoreCase))
        {
            return "SSD";
        }

        if (combinedInformation.Contains(
                "USB",
                StringComparison.OrdinalIgnoreCase))
        {
            return "USB-Laufwerk";
        }

        if (combinedInformation.Contains(
                "HDD",
                StringComparison.OrdinalIgnoreCase)
            || combinedInformation.Contains(
                "Fixed hard disk",
                StringComparison.OrdinalIgnoreCase))
        {
            return "HDD";
        }

        return "Unbekannt";
    }
}