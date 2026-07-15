using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ISecurityInformationScanner
{
    ScanResult<SecurityInformation> Scan();
}