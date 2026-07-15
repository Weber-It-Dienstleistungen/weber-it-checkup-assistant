using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class WindowsUpdateInformationScanner :
    IWindowsUpdateInformationScanner
{
    private readonly IWindowsUpdateInformationProvider
        _windowsUpdateInformationProvider;

    public WindowsUpdateInformationScanner(
        IWindowsUpdateInformationProvider windowsUpdateInformationProvider)
    {
        _windowsUpdateInformationProvider =
            windowsUpdateInformationProvider;
    }

    public ScanResult<WindowsUpdateInformation> Scan()
    {
        var windowsUpdateInformation =
            _windowsUpdateInformationProvider
                .GetWindowsUpdateInformation();

        return new ScanResult<WindowsUpdateInformation>
        {
            IsSuccessful = true,
            Data = windowsUpdateInformation
        };
    }
}