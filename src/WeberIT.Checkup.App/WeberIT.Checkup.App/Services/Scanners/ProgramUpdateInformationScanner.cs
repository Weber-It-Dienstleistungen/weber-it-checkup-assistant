using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class ProgramUpdateInformationScanner :
    IProgramUpdateInformationScanner
{
    private readonly IProgramUpdateInformationProvider
        _programUpdateInformationProvider;

    public ProgramUpdateInformationScanner(
        IProgramUpdateInformationProvider programUpdateInformationProvider)
    {
        _programUpdateInformationProvider =
            programUpdateInformationProvider;
    }

    public ScanResult<ProgramUpdateInformation> Scan()
    {
        var programUpdateInformation =
            _programUpdateInformationProvider
                .GetProgramUpdateInformation();

        return new ScanResult<ProgramUpdateInformation>
        {
            IsSuccessful = true,
            Data = programUpdateInformation
        };
    }
}