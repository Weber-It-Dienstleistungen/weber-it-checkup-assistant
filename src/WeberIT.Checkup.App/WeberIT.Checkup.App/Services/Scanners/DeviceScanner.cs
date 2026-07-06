using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceScanner : IDeviceScanner
{
    public DeviceScanResult Scan()
    {
        return new DeviceScanResult
        {
            ScanDate = DateTime.Now
        };
    }
}