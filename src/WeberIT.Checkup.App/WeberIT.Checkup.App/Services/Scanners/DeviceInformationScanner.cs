using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceInformationScanner : IDeviceInformationScanner
{
    private readonly IWindowsInformationProvider _windowsInformationProvider;

    public DeviceInformationScanner(IWindowsInformationProvider windowsInformationProvider)
    {
        _windowsInformationProvider = windowsInformationProvider;
    }

    public ScanResult<DeviceInformation> Scan()
    {
        var deviceInformation = new DeviceInformation
        {
            Name = _windowsInformationProvider.GetComputerName(),
            Manufacturer = _windowsInformationProvider.GetManufacturer(),
            Model = _windowsInformationProvider.GetModel(),
            SerialNumber = _windowsInformationProvider.GetSerialNumber(),
            DeviceType = _windowsInformationProvider.GetDeviceType(),

            OperatingSystemName = _windowsInformationProvider.GetOperatingSystemName(),
            OperatingSystemVersion = _windowsInformationProvider.GetOperatingSystemVersion(),
            OperatingSystemArchitecture = _windowsInformationProvider.GetOperatingSystemArchitecture(),
            BiosVersion = _windowsInformationProvider.GetBiosVersion()
        };

        return new ScanResult<DeviceInformation>
        {
            IsSuccessful = true,
            Data = deviceInformation
        };
    }
}