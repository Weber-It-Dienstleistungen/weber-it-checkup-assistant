using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class OperatingSystemInformationScanner : IOperatingSystemInformationScanner
{
    private readonly IWindowsInformationProvider _windowsInformationProvider;

    public OperatingSystemInformationScanner(IWindowsInformationProvider windowsInformationProvider)
    {
        _windowsInformationProvider = windowsInformationProvider;
    }

    public ScanResult<OperatingSystemInformation> Scan()
    {
        var operatingSystemInformation = new OperatingSystemInformation
        {
            Name = _windowsInformationProvider.GetOperatingSystemName(),
            Version = _windowsInformationProvider.GetOperatingSystemVersion(),
            BuildNumber = _windowsInformationProvider.GetOperatingSystemBuildNumber(),
            Architecture = _windowsInformationProvider.GetOperatingSystemArchitecture()
        };

        return new ScanResult<OperatingSystemInformation>
        {
            IsSuccessful = true,
            Data = operatingSystemInformation
        };
    }
}