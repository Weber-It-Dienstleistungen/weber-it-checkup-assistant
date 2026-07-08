using System.Management;
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

    public string GetBiosVersion()
    {
        return GetWmiValue("Win32_BIOS", "SMBIOSBIOSVersion");
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
}