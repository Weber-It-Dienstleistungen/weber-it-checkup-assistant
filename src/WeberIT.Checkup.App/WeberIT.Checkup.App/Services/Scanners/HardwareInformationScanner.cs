using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class HardwareInformationScanner : IHardwareInformationScanner
{
    private readonly IHardwareInformationProvider _hardwareInformationProvider;

    public HardwareInformationScanner(IHardwareInformationProvider hardwareInformationProvider)
    {
        _hardwareInformationProvider = hardwareInformationProvider;
    }

    public ScanResult<HardwareInformation> Scan()
    {
        var hardwareInformation = new HardwareInformation
        {
            ProcessorName = _hardwareInformationProvider.GetProcessorName(),
            InstalledMemory = _hardwareInformationProvider.GetInstalledMemory()
        };

        return new ScanResult<HardwareInformation>
        {
            IsSuccessful = true,
            Data = hardwareInformation
        };
    }
}