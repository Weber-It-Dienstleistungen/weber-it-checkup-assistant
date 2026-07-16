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

                var mediaType =
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
                    ParseUnsignedLong(sizeValue);

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
                            DetermineDriveType(
                                model,
                                mediaType,
                                interfaceType,
                                pnpDeviceId)
                    });
            }
        }
        catch
        {
            // Laufwerksabfrage darf den Scan
            // weiterhin nicht abbrechen.
        }

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
                            BuildDriveTypeDescription(
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
            // weiterhin nicht abbrechen.
        }

        return volumes;
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
            // nicht abbrechen. Das Volume bleibt dann
            // ohne physische Disknummer.
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

    private static string BuildDriveTypeDescription(
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

    private static string DetermineDriveType(
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
            return "SATA SSD";
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