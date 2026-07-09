using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class CheckupScanner : ICheckupScanner
{
    private readonly IDeviceInformationScanner _deviceInformationScanner;
    private readonly IHardwareInformationScanner _hardwareInformationScanner;
    private readonly IOperatingSystemInformationScanner _operatingSystemInformationScanner;
    private readonly IStorageInformationScanner _storageInformationScanner;

    public CheckupScanner(
        IDeviceInformationScanner deviceInformationScanner,
        IHardwareInformationScanner hardwareInformationScanner,
        IOperatingSystemInformationScanner operatingSystemInformationScanner,
        IStorageInformationScanner storageInformationScanner)
    {
        _deviceInformationScanner = deviceInformationScanner;
        _hardwareInformationScanner = hardwareInformationScanner;
        _operatingSystemInformationScanner = operatingSystemInformationScanner;
        _storageInformationScanner = storageInformationScanner;
    }

    public CheckupSession Scan()
    {
        var deviceInformationResult = _deviceInformationScanner.Scan();
        var hardwareInformationResult = _hardwareInformationScanner.Scan();
        var operatingSystemInformationResult = _operatingSystemInformationScanner.Scan();
        var storageInformationResult = _storageInformationScanner.Scan();

        return new CheckupSession
        {
            ScanDate = DateTime.Now,
            DeviceInformation = deviceInformationResult.Data ?? new DeviceInformation(),
            HardwareInformation = hardwareInformationResult.Data ?? new HardwareInformation(),
            OperatingSystemInformation = operatingSystemInformationResult.Data ?? new OperatingSystemInformation(),
            StorageInformation = storageInformationResult.Data ?? new StorageInformation()
        };
    }
}