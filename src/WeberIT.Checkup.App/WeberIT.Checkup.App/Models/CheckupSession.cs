namespace WeberIT.Checkup.App.Models;

public class CheckupSession
{
    public DateTime? ScanDate { get; set; }

    public DeviceInformation DeviceInformation { get; set; } = new();

    public HardwareInformation HardwareInformation { get; set; } = new();

    public OperatingSystemInformation OperatingSystemInformation { get; set; } = new();
}