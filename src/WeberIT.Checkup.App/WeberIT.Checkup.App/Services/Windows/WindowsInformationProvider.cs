using System;
using System.Management;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Windows;

public class WindowsInformationProvider : IWindowsInformationProvider
{
    public string GetComputerName()
    {
        return Environment.MachineName;
    }

    public string GetOperatingSystemName()
    {
        return GetWmiValue("Win32_OperatingSystem", "Caption");
    }

    public string GetOperatingSystemVersion()
    {
        return GetWmiValue("Win32_OperatingSystem", "Version");
    }

    public string GetOperatingSystemArchitecture()
    {
        return GetWmiValue("Win32_OperatingSystem", "OSArchitecture");
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