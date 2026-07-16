namespace WeberIT.Checkup.App.Models;

public class HardwareInformation
{
    public string ProcessorName { get; set; } = string.Empty;

    public string InstalledMemory { get; set; } = string.Empty;

    public string MainboardManufacturer { get; set; } = string.Empty;

    public string MainboardProduct { get; set; } = string.Empty;

    public string BiosManufacturer { get; set; } = string.Empty;

    public string BiosVersion { get; set; } = string.Empty;

    public List<string> GraphicsCards { get; set; } = new();

    public string TpmStatus { get; set; } = string.Empty;

    public string TpmVersion { get; set; } = string.Empty;
}