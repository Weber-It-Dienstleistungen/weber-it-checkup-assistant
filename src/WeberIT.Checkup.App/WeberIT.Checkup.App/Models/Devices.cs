namespace WeberIT.Checkup.App.Models;

public class Device
{
    public string DeviceNumber { get; set; } = string.Empty;

    public string CustomerNumber { get; set; } = string.Empty;

    public DeviceInformation Information { get; set; } = new();
}