using System.IO;
using System.Management;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Storage;

public class StorageInformationProvider : IStorageInformationProvider
{
    public List<DriveInformation> GetPhysicalDrives()
    {
        var drives = new List<DriveInformation>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, Manufacturer, Size, MediaType, InterfaceType, PNPDeviceID FROM Win32_DiskDrive");

            foreach (var result in searcher.Get())
            {
                var model = result["Model"]?.ToString()?.Trim() ?? "Unbekannt";
                var manufacturer = result["Manufacturer"]?.ToString()?.Trim() ?? "Unbekannt";
                var size = result["Size"]?.ToString()?.Trim() ?? string.Empty;
                var mediaType = result["MediaType"]?.ToString()?.Trim() ?? string.Empty;
                var interfaceType = result["InterfaceType"]?.ToString()?.Trim() ?? string.Empty;
                var pnpDeviceId = result["PNPDeviceID"]?.ToString()?.Trim() ?? string.Empty;

                drives.Add(new DriveInformation
                {
                    Model = model,
                    Manufacturer = manufacturer,
                    Capacity = FormatBytesAsGigabytes(size),
                    DriveType = DetermineDriveType(model, mediaType, interfaceType, pnpDeviceId)
                });
            }
        }
        catch
        {
            // Laufwerksabfrage darf den Scan nicht abbrechen.
        }

        return drives.Count > 0
            ? drives
            : new List<DriveInformation>
            {
                new()
                {
                    Model = "Unbekannt",
                    Manufacturer = "Unbekannt",
                    Capacity = "Unbekannt",
                    DriveType = "Unbekannt"
                }
            };
    }

    public List<VolumeInformation> GetVolumes()
    {
        var volumes = new List<VolumeInformation>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                volumes.Add(new VolumeInformation
                {
                    Name = drive.Name,
                    DriveLetter = drive.Name.Replace("\\", string.Empty),
                    DriveType = drive.DriveType.ToString(),
                    FileSystem = drive.IsReady ? drive.DriveFormat : "Unbekannt",
                    TotalSize = drive.IsReady ? FormatBytesAsGigabytes(drive.TotalSize.ToString()) : "Unbekannt",
                    FreeSpace = drive.IsReady ? FormatBytesAsGigabytes(drive.AvailableFreeSpace.ToString()) : "Unbekannt",
                    IsReady = drive.IsReady
                });
            }
        }
        catch
        {
            // Volume-Abfrage darf den Scan nicht abbrechen.
        }

        return volumes;
    }

    private static string FormatBytesAsGigabytes(string bytesValue)
    {
        if (!ulong.TryParse(bytesValue, out var bytes))
        {
            return "Unbekannt";
        }

        var gigabytes = bytes / 1000d / 1000d / 1000d;

        return $"{gigabytes:0.#} GB";
    }

    private static string DetermineDriveType(
        string model,
        string mediaType,
        string interfaceType,
        string pnpDeviceId)
    {
        var combinedInformation = $"{model} {mediaType} {interfaceType} {pnpDeviceId}";

        if (combinedInformation.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
        {
            return "NVMe SSD";
        }

        if (combinedInformation.Contains("SSD", StringComparison.OrdinalIgnoreCase)
            || combinedInformation.Contains("Solid State", StringComparison.OrdinalIgnoreCase))
        {
            return "SATA SSD";
        }

        if (combinedInformation.Contains("USB", StringComparison.OrdinalIgnoreCase))
        {
            return "USB-Laufwerk";
        }

        if (combinedInformation.Contains("HDD", StringComparison.OrdinalIgnoreCase)
            || combinedInformation.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase))
        {
            return "HDD";
        }

        return "Unbekannt";
    }
}