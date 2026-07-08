using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IHardwareInformationScanner
{
    ScanResult<HardwareInformation> Scan();
}