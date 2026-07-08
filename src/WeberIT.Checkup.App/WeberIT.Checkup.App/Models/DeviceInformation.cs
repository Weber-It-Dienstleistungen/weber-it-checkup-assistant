namespace WeberIT.Checkup.App.Models;

public class DeviceInformation
{
    public string Name { get; set; } = string.Empty;

    public string DeviceType { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string OperatingSystemName { get; set; } = string.Empty;

    public string OperatingSystemVersion { get; set; } = string.Empty;

    public string OperatingSystemArchitecture { get; set; } = string.Empty;

    public string BiosVersion { get; set; } = string.Empty;

    public string ProcessorName { get; set; } = string.Empty;

    public string InstalledMemory { get; set; } = string.Empty;
}