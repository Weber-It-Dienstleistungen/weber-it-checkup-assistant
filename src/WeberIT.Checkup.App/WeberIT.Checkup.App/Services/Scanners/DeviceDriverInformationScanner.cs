using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceDriverInformationScanner :
    IDeviceDriverInformationScanner
{
    private readonly IDeviceDriverInformationProvider
        _deviceDriverInformationProvider;

    public DeviceDriverInformationScanner(
        IDeviceDriverInformationProvider deviceDriverInformationProvider)
    {
        _deviceDriverInformationProvider =
            deviceDriverInformationProvider;
    }

    public ScanResult<DeviceDriverInformation> Scan()
    {
        try
        {
            var information =
                _deviceDriverInformationProvider.Analyze();

            return new ScanResult<DeviceDriverInformation>
            {
                IsSuccessful =
                    information.AnalysisStatus
                        is DeviceDriverAnalysisStatus.Analyzed
                        or DeviceDriverAnalysisStatus.PartiallyAnalyzed,

                Message =
                    information.AnalysisMessage,

                Data =
                    information
            };
        }
        catch
        {
            var information =
                new DeviceDriverInformation
                {
                    AnalysisDate =
                        DateTime.Now,

                    AnalysisStatus =
                        DeviceDriverAnalysisStatus.NotEvaluable,

                    AnalysisMessage =
                        "Die Geräte- und Treiberanalyse konnte nicht ausgeführt werden."
                };

            return new ScanResult<DeviceDriverInformation>
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