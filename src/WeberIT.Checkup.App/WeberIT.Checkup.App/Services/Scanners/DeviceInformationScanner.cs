using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceInformationScanner : IDeviceInformationScanner
{
    private readonly IWindowsInformationProvider _windowsInformationProvider;
    private readonly IHardwareInformationProvider _hardwareInformationProvider;

    public DeviceInformationScanner(
        IWindowsInformationProvider windowsInformationProvider,
        IHardwareInformationProvider hardwareInformationProvider)
    {
        _windowsInformationProvider = windowsInformationProvider;
        _hardwareInformationProvider = hardwareInformationProvider;
    }

    public ScanResult<DeviceInformation> Scan()
    {
        var deviceInformation = new DeviceInformation
        {
            Name = _windowsInformationProvider.GetComputerName(),

            Manufacturer = _hardwareInformationProvider.GetManufacturer(),
            Model = _hardwareInformationProvider.GetModel(),
            SerialNumber = _hardwareInformationProvider.GetSerialNumber(),
            DeviceType = _hardwareInformationProvider.GetDeviceType(),
            BiosVersion = _hardwareInformationProvider.GetBiosVersion(),

            OperatingSystemName = _windowsInformationProvider.GetOperatingSystemName(),
            OperatingSystemVersion = _windowsInformationProvider.GetOperatingSystemVersion(),
            OperatingSystemArchitecture = _windowsInformationProvider.GetOperatingSystemArchitecture()
        };

        return new ScanResult<DeviceInformation>
        {
            IsSuccessful = true,
            Data = deviceInformation
        };
    }
}