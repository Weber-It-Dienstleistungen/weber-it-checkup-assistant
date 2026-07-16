using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class StartupInformationScanner :
    IStartupInformationScanner
{
    private readonly IStartupInformationProvider
        _startupInformationProvider;

    public StartupInformationScanner(
        IStartupInformationProvider startupInformationProvider)
    {
        _startupInformationProvider =
            startupInformationProvider;
    }

    public ScanResult<StartupInformation> Scan()
    {
        try
        {
            var information =
                _startupInformationProvider.Analyze();

            return new ScanResult<StartupInformation>
            {
                IsSuccessful =
                    information.AnalysisStatus
                        is StartupAnalysisStatus.Analyzed
                        or StartupAnalysisStatus.PartiallyAnalyzed,

                Message =
                    information.AnalysisMessage,

                Data =
                    information
            };
        }
        catch
        {
            var information =
                new StartupInformation
                {
                    AnalysisDate =
                        DateTime.Now,

                    AnalysisStatus =
                        StartupAnalysisStatus.NotEvaluable,

                    AnalysisMessage =
                        "Die Autostartanalyse konnte nicht "
                        + "ausgeführt werden."
                };

            return new ScanResult<StartupInformation>
            {
                IsSuccessful =
                    false,

                Message =
                    information.AnalysisMessage,

                Data =
                    information
            };
        }
    }
}