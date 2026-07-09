using System;
using System.Management;
using Microsoft.Win32;
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
        var displayVersion = GetWindowsRegistryValue("DisplayVersion");

        if (!string.IsNullOrWhiteSpace(displayVersion))
        {
            return displayVersion;
        }

        var releaseId = GetWindowsRegistryValue("ReleaseId");

        return string.IsNullOrWhiteSpace(releaseId)
            ? "Unbekannt"
            : releaseId;
    }

    public string GetOperatingSystemBuildNumber()
    {
        var buildNumber = GetWindowsRegistryValue("CurrentBuildNumber");

        return string.IsNullOrWhiteSpace(buildNumber)
            ? "Unbekannt"
            : buildNumber;
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

    private static string GetWindowsRegistryValue(string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            var value = key?.GetValue(valueName)?.ToString();

            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
        catch
        {
            // Registry-Abfragen dürfen den Scan nicht abbrechen.
        }

        return string.Empty;
    }
}