
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Führt eine WinGet-Updateanalyse nur dann aus,
/// wenn zuvor die offizielle Paketquelle
/// erfolgreich aktualisiert werden konnte.
///
/// Dieser Dienst ist ausschließlich für die
/// WinGet-Zwischenprüfung vorgesehen.
///
/// Der normale Eingangs- und Abschlusscheck
/// verwenden weiterhin den ursprünglichen
/// IProgramUpdateInformationProvider.
/// </summary>
public sealed class
    FreshnessCheckedProgramUpdateInformationProvider :
        IProgramUpdateInformationProvider
{
    private readonly IProgramUpdateInformationProvider
        _innerProvider;

    private readonly WingetSourceFreshnessService
        _sourceFreshnessService;

    public FreshnessCheckedProgramUpdateInformationProvider(
        IProgramUpdateInformationProvider innerProvider,
        WingetSourceFreshnessService sourceFreshnessService)
    {
        ArgumentNullException.ThrowIfNull(
            innerProvider);

        ArgumentNullException.ThrowIfNull(
            sourceFreshnessService);

        _innerProvider =
            innerProvider;

        _sourceFreshnessService =
            sourceFreshnessService;
    }

    // =============================================
    // ABGESICHERTE WINGET-ANALYSE
    // =============================================

    public ProgramUpdateInformation
        GetProgramUpdateInformation()
    {
        WingetSourceFreshnessResult
            freshnessResult;

        try
        {
            /*
             * Das bestehende Provider-Interface
             * arbeitet synchron.
             *
             * Die Quellenprüfung ist asynchron.
             *
             * Wir führen sie daher auf einem
             * separaten Hintergrundthread aus,
             * damit keine SynchronizationContext-
             * Blockade entstehen kann.
             *
             * Der aufrufende Zwischenprüfungs-
             * dienst führt die Analyse bereits
             * außerhalb des UI-Threads aus.
             */
            freshnessResult =
                Task.Run(
                    () =>
                        _sourceFreshnessService
                            .CheckAsync())
                    .GetAwaiter()
                    .GetResult();
        }
        catch (Exception exception)
        {
            return CreateUnreliableInformation(
                "Die WinGet-Quellenprüfung "
                + "konnte nicht zuverlässig "
                + "ausgeführt werden."
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message);
        }

        // =========================================
        // QUELLENREFRESH FEHLGESCHLAGEN
        // =========================================

        if (!freshnessResult.IsSourceRefreshConfirmed)
        {
            /*
             * Wir rufen den normalen WinGet-
             * Updateprovider ausdrücklich
             * NICHT mehr auf.
             *
             * Damit kann ein möglicherweise
             * veralteter lokaler Paketindex
             * nicht als bestätigter aktueller
             * Zwischenbefund übernommen werden.
             */
            return CreateUnreliableInformation(
                "Die WinGet-Paketquelle konnte "
                + "nicht erfolgreich aktualisiert "
                + "werden."
                + Environment.NewLine
                + Environment.NewLine
                + freshnessResult.Details);
        }

        // =========================================
        // QUELLE AKTUALISIERT:
        // UPDATEANALYSE AUSFÜHREN
        // =========================================

        ProgramUpdateInformation information;

        try
        {
            information =
                _innerProvider
                    .GetProgramUpdateInformation();
        }
        catch (Exception exception)
        {
            return CreateUnreliableInformation(
                "Die WinGet-Quelle wurde "
                + "erfolgreich aktualisiert, "
                + "aber die anschließende "
                + "Updateanalyse ist fehlgeschlagen."
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message);
        }

        if (information is null)
        {
            return CreateUnreliableInformation(
                "Der WinGet-Analysedienst hat "
                + "kein auswertbares Ergebnis "
                + "zurückgegeben.");
        }

        /*
         * Wir dokumentieren die erfolgreiche
         * vorgeschaltete Quellenprüfung
         * zusätzlich im technischen Befund.
         *
         * Die eigentliche Zuverlässigkeit
         * der Paketliste wird weiterhin
         * anhand der bekannten Kriterien
         * bestimmt:
         *
         * - WinGet verfügbar
         * - Analyse durchgeführt
         * - Analyse erfolgreich
         * - keine abgeschnittene Liste
         * - vollständige Paketinformationen
         * - konsistente Updateanzahl
         */
        var previousDetails =
            information.AnalysisDetails;

        information.AnalysisDetails =
            "WinGet-Quellenprüfung: "
            + freshnessResult.Details
            + Environment.NewLine
            + Environment.NewLine
            + previousDetails;

        return information;
    }

    // =============================================
    // NICHT BELASTBAREN BEFUND ERZEUGEN
    // =============================================

    private static ProgramUpdateInformation
        CreateUnreliableInformation(
            string details)
    {
        /*
         * Ein abgebrochener Quellenrefresh
         * ist ausdrücklich keine bestätigte
         * Aussage über verfügbare Updates.
         *
         * Der Wert 0 in AvailableUpdateCount
         * ist hier kein Nullbefund:
         *
         * IsAnalysisSuccessful = false
         * verhindert die Zuverlässigkeits-
         * bestätigung durch das gespeicherte
         * Zwischenprüfungsmodell.
         */
        return new ProgramUpdateInformation
        {
            IsWingetAvailable = null,

            IsAnalysisPerformed = false,

            IsAnalysisSuccessful = false,

            AnalysisDate = null,

            AnalysisDetails = details,

            AvailableUpdateCount = 0,

            IsResultTruncated = false,

            AvailableUpdates =
                new List<AvailableProgramUpdate>()
        };
    }
}
