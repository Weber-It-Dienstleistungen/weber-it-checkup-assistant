using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IRestartInformationScanner
{
    ScanResult<RestartInformation> Scan();
}