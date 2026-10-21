
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class WindowsUpdateInformationScanner :
    IWindowsUpdateInformationScanner
{
    private readonly IWindowsUpdateInformationProvider
        _windowsUpdateInformationProvider;

    public WindowsUpdateInformationScanner(
        IWindowsUpdateInformationProvider
            windowsUpdateInformationProvider)
    {
        ArgumentNullException.ThrowIfNull(
            windowsUpdateInformationProvider);

        _windowsUpdateInformationProvider =
            windowsUpdateInformationProvider;
    }

    public ScanResult<WindowsUpdateInformation> Scan()
    {
        var windowsUpdateInformation =
            _windowsUpdateInformationProvider
                .GetWindowsUpdateInformation();

        /*
         * SCHRITT 46:
         *
         * Eine erfolgreiche Ausführung des Providers
         * bedeutet nicht automatisch, dass die
         * Windows-Update-Onlineprüfung erfolgreich war.
         *
         * Die Prüfung gilt nur als zuverlässig, wenn:
         *
         * - Die Onlineprüfung durchgeführt wurde.
         * - Die Onlineprüfung erfolgreich abgeschlossen
         *   wurde.
         *
         * Wurde die Suche übersprungen, durch einen
         * Zeitablauf beendet oder vom Windows Update
         * Agent nicht erfolgreich abgeschlossen,
         * meldet der Scanner einen Warnungsstatus.
         *
         * Die ermittelten Daten bleiben unabhängig
         * vom Scanstatus vollständig erhalten.
         *
         * Insbesondere werden lokale Informationen
         * über den Windows-Update-Dienst, bereits
         * installierte Updates und Fehler im
         * Updateverlauf nicht verworfen.
         *
         * Diese Änderung beeinflusst weder
         * die Bewertung noch den Score oder
         * den Vorher-/Nachher-Vergleich.
         */

        var isOnlineSearchReliable =
            windowsUpdateInformation.IsUpdateSearchPerformed
            && windowsUpdateInformation.IsUpdateSearchSuccessful;

        return new ScanResult<WindowsUpdateInformation>
        {
            IsSuccessful =
                isOnlineSearchReliable,

            Message =
                windowsUpdateInformation.UpdateSearchDetails,

            Data =
                windowsUpdateInformation
        };
    }
}
