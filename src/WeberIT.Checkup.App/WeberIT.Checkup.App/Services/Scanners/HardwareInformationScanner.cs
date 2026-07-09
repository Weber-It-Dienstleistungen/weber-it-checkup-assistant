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
            InstalledMemory = _hardwareInformationProvider.GetInstalledMemory(),
            MainboardManufacturer = _hardwareInformationProvider.GetMainboardManufacturer(),
            MainboardProduct = _hardwareInformationProvider.GetMainboardProduct(),
            BiosManufacturer = _hardwareInformationProvider.GetBiosManufacturer(),
            BiosVersion = _hardwareInformationProvider.GetBiosVersion(),
            GraphicsCards = _hardwareInformationProvider.GetGraphicsCards(),
            TpmStatus = _hardwareInformationProvider.GetTpmStatus(),
            TpmVersion = _hardwareInformationProvider.GetTpmVersion(),
            Drives = _hardwareInformationProvider.GetDrives()
        };

        return new ScanResult<HardwareInformation>
        {
            IsSuccessful = true,
            Data = hardwareInformation
        };
    }
}