using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class RestartInformationScanner :
    IRestartInformationScanner
{
    private readonly IRestartInformationProvider
        _restartInformationProvider;

    public RestartInformationScanner(
        IRestartInformationProvider restartInformationProvider)
    {
        _restartInformationProvider =
            restartInformationProvider;
    }

    public ScanResult<RestartInformation> Scan()
    {
        var restartInformation =
            _restartInformationProvider
                .GetRestartInformation();

        return new ScanResult<RestartInformation>
        {
            IsSuccessful = true,
            Data = restartInformation
        };
    }
}