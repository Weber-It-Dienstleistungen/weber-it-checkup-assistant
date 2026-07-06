namespace WeberIT.Checkup.App.Models;

public class DeviceScanResult
{
    public DateTime ScanDate { get; set; } = DateTime.Now;

    public DeviceInformation DeviceInformation { get; set; } = new();
}