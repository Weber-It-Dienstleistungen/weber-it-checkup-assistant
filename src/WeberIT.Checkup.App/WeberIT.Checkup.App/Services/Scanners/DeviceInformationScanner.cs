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
            Manufacturer = _windowsInformationProvider.GetManufacturer(),
            Model = _windowsInformationProvider.GetModel(),
            SerialNumber = _windowsInformationProvider.GetSerialNumber(),
            DeviceType = _windowsInformationProvider.GetDeviceType()
        };

        return new ScanResult<DeviceInformation>
        {
            IsSuccessful = true,
            Data = deviceInformation,
            Message = null
        };
    }
}