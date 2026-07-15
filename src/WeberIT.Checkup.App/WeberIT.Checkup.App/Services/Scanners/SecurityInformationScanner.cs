using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Security;

namespace WeberIT.Checkup.App.Services.Scanners;

public class SecurityInformationScanner : ISecurityInformationScanner
{
    private readonly ISecurityInformationProvider
        _securityInformationProvider;

    public SecurityInformationScanner(
        ISecurityInformationProvider securityInformationProvider)
    {
        _securityInformationProvider =
            securityInformationProvider;
    }

    public ScanResult<SecurityInformation> Scan()
    {
        var securityInformation =
            _securityInformationProvider.GetSecurityInformation();

        return new ScanResult<SecurityInformation>
        {
            IsSuccessful = true,
            Data = securityInformation
        };
    }
}