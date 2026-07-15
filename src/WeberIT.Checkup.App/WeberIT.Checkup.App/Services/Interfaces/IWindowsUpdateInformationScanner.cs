using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IWindowsUpdateInformationScanner
{
    ScanResult<WindowsUpdateInformation> Scan();
}