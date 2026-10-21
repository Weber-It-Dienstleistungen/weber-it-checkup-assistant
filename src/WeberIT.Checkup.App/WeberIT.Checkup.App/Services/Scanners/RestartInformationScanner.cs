
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
        ArgumentNullException.ThrowIfNull(
            restartInformationProvider);

        _restartInformationProvider =
            restartInformationProvider;
    }

    public ScanResult<RestartInformation> Scan()
    {
        var restartInformation =
            _restartInformationProvider
                .GetRestartInformation();

        /*
         * SCHRITT 45:
         *
         * Die erfolgreiche Ausführung des Providers
         * bedeutet nicht automatisch, dass sämtliche
         * Neustartquellen zuverlässig geprüft wurden.
         *
         * Ein grüner Scanstatus setzt daher voraus:
         *
         * - Die Analyse wurde durchgeführt.
         * - Der Neustartstatus ist eindeutig.
         * - Keine einzelne Quelle ist fehlgeschlagen.
         *
         * Ein fachlich bestätigter Neustartbedarf
         * bleibt auch bei teilweise fehlgeschlagenen
         * Quellen im Ergebnis erhalten. Lediglich
         * die Fortschrittsanzeige weist dann auf die
         * unvollständige Prüfung hin.
         *
         * Die eigentlichen Befund-, Bewertungs-
         * und Vergleichsregeln bleiben unverändert.
         */

        var isScanSuccessful =
            restartInformation.IsAnalysisPerformed
            && restartInformation.IsAnalysisConclusive
            && !restartInformation.HasFailedSources;

        return new ScanResult<RestartInformation>
        {
            IsSuccessful =
                isScanSuccessful,

            Message =
                BuildScanMessage(
                    restartInformation),

            Data =
                restartInformation
        };
    }

    private static string BuildScanMessage(
        RestartInformation information)
    {
        if (!information.IsAnalysisPerformed)
        {
            return
                "Die Prüfung des Windows-Neustartstatus "
                + "wurde nicht durchgeführt.";
        }

        var failedSources =
            information.Sources
                .Where(source =>
                    !source.IsCheckSuccessful)
                .Select(source =>
                    source.DisplayName)
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (failedSources.Count > 0)
        {
            var introduction =
                information.IsAnalysisConclusive
                    ? "Der Neustartstatus konnte trotz "
                      + "unvollständiger Einzelprüfungen "
                      + "fachlich bestimmt werden. "
                    : "Der Windows-Neustartstatus konnte "
                      + "nicht vollständig und eindeutig "
                      + "bestimmt werden. ";

            return introduction
                + "Nicht zuverlässig überprüfte Quellen: "
                + string.Join(
                    ", ",
                    failedSources)
                + ".";
        }

        if (!information.IsAnalysisConclusive)
        {
            if (information.HasAdvisoryRestartHint)
            {
                return
                    "Windows hat ausstehende Dateioperationen "
                    + "gemeldet. Dieser Hinweis allein beweist "
                    + "keinen zwingend erforderlichen Neustart. "
                    + "Der Neustartstatus bleibt daher "
                    + "nicht eindeutig.";
            }

            return
                "Die Neustartprüfung wurde durchgeführt, "
                + "konnte aber keinen eindeutig "
                + "bewertbaren Gesamtstatus liefern.";
        }

        if (information.IsRestartRequired == true)
        {
            return
                "Mindestens eine bestätigende Windows-Quelle "
                + "meldet einen ausstehenden Neustart.";
        }

        if (information.IsRestartRequired == false)
        {
            return
                "Die geprüften Windows-Indikatoren melden "
                + "keinen erforderlichen Neustart.";
        }

        return
            "Der Windows-Neustartstatus konnte "
            + "nicht eindeutig bestimmt werden.";
    }
}
