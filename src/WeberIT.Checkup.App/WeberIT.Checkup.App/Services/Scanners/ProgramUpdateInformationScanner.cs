
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class ProgramUpdateInformationScanner :
    IProgramUpdateInformationScanner
{
    private readonly IProgramUpdateInformationProvider
        _programUpdateInformationProvider;

    public ProgramUpdateInformationScanner(
        IProgramUpdateInformationProvider
            programUpdateInformationProvider)
    {
        ArgumentNullException.ThrowIfNull(
            programUpdateInformationProvider);

        _programUpdateInformationProvider =
            programUpdateInformationProvider;
    }

    public ScanResult<ProgramUpdateInformation> Scan()
    {
        var programUpdateInformation =
            _programUpdateInformationProvider
                .GetProgramUpdateInformation();

        /*
         * Eine erfolgreiche Ausführung des Providers
         * bedeutet nicht automatisch, dass WinGet
         * zuverlässig ausgewertet werden konnte.
         *
         * Nur eine vollständig bestätigte Analyse
         * wird als erfolgreicher Scanbereich gemeldet.
         *
         * Bei unvollständiger oder fehlgeschlagener
         * Analyse erhält der übergeordnete Scanner
         * den Warnungsstatus und die Diagnose.
         *
         * Die ermittelten Daten werden unabhängig
         * vom Status weiterhin übergeben. Dadurch
         * bleiben Fehlerursachen dokumentierbar.
         */
        var isAnalysisReliable =
            programUpdateInformation.IsWingetAvailable == true
            && programUpdateInformation.IsAnalysisPerformed
            && programUpdateInformation.IsAnalysisSuccessful;

        return new ScanResult<ProgramUpdateInformation>
        {
            IsSuccessful =
                isAnalysisReliable,

            Message =
                programUpdateInformation.AnalysisDetails,

            Data =
                programUpdateInformation
        };
    }
}
