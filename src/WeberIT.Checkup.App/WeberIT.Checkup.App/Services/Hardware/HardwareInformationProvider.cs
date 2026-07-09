using System.Management;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Hardware;

public class HardwareInformationProvider : IHardwareInformationProvider
{
    public string GetManufacturer()
    {
        return GetWmiValue("Win32_ComputerSystem", "Manufacturer");
    }

    public string GetModel()
    {
        return GetWmiValue("Win32_ComputerSystem", "Model");
    }

    public string GetSerialNumber()
    {
        return GetWmiValue("Win32_BIOS", "SerialNumber");
    }

    public string GetDeviceType()
    {
        var pcSystemType = GetWmiValue("Win32_ComputerSystem", "PCSystemType");

        return pcSystemType switch
        {
            "1" => "Desktop",
            "2" => "Notebook",
            "3" => "Workstation",
            "4" => "Enterprise Server",
            "5" => "Small Office/Home Office Server",
            "6" => "Appliance PC",
            "7" => "Performance Server",
            "8" => "Tablet",
            _ => "Unbekannt"
        };
    }

    public string GetBiosManufacturer()
    {
        return GetWmiValue("Win32_BIOS", "Manufacturer");
    }

    public string GetBiosVersion()
    {
        return GetWmiValue("Win32_BIOS", "SMBIOSBIOSVersion");
    }

    public string GetProcessorName()
    {
        return GetWmiValue("Win32_Processor", "Name");
    }

    public string GetInstalledMemory()
    {
        var totalPhysicalMemory = GetWmiValue("Win32_ComputerSystem", "TotalPhysicalMemory");

        if (!ulong.TryParse(totalPhysicalMemory, out var bytes))
        {
            return "Unbekannt";
        }

        var gibibytes = bytes / 1024d / 1024d / 1024d;

        return $"{gibibytes:0.#} GB";
    }

    public string GetMainboardManufacturer()
    {
        return GetWmiValue("Win32_BaseBoard", "Manufacturer");
    }

    public string GetMainboardProduct()
    {
        return GetWmiValue("Win32_BaseBoard", "Product");
    }

    public List<string> GetGraphicsCards()
    {
        var graphicsCards = GetWmiValues("Win32_VideoController", "Name");

        return graphicsCards.Count > 0
            ? graphicsCards
            : new List<string> { "Unbekannt" };
    }

    public string GetTpmStatus()
    {
        var isEnabled = GetTpmValue("IsEnabled_InitialValue");
        var isActivated = GetTpmValue("IsActivated_InitialValue");

        if (isEnabled == "Unbekannt" && isActivated == "Unbekannt")
        {
            return "Nicht erkannt";
        }

        if (isEnabled.Equals("True", StringComparison.OrdinalIgnoreCase)
            && isActivated.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            return "Aktiv";
        }

        return "Vorhanden, aber nicht aktiv";
    }

    public string GetTpmVersion()
    {
        var specificationVersion = GetTpmValue("SpecVersion");

        return string.IsNullOrWhiteSpace(specificationVersion)
            ? "Unbekannt"
            : specificationVersion;
    }

    public List<DriveInformation> GetDrives()
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

    private static string GetWmiValue(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT {propertyName} FROM {className}");

            foreach (var result in searcher.Get())
            {
                var value = result[propertyName]?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // WMI darf den Scan nicht abbrechen.
        }

        return "Unbekannt";
    }

    private static List<string> GetWmiValues(string className, string propertyName)
    {
        var values = new List<string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT {propertyName} FROM {className}");

            foreach (var result in searcher.Get())
            {
                var value = result[propertyName]?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
        }
        catch
        {
            // WMI darf den Scan nicht abbrechen.
        }

        return values;
    }

    private static string GetTpmValue(string propertyName)
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftTpm");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery($"SELECT {propertyName} FROM Win32_Tpm"));

            foreach (var result in searcher.Get())
            {
                var value = result[propertyName]?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // TPM-Abfrage darf den Scan nicht abbrechen.
        }

        return "Unbekannt";
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