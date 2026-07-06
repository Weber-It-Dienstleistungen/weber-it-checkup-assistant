using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceScanner : IDeviceScanner
{
    private readonly IDeviceInformationScanner _deviceInformationScanner;

    public DeviceScanner(IDeviceInformationScanner deviceInformationScanner)
    {
        _deviceInformationScanner = deviceInformationScanner;
    }

    public DeviceScanResult Scan()
    {
        var deviceInformationResult = _deviceInformationScanner.Scan();

        return new DeviceScanResult
        {
            ScanDate = DateTime.Now,
            DeviceInformation = deviceInformationResult.Data ?? new DeviceInformation()
        };
    }
}