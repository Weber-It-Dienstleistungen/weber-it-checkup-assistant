
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Führt eine eigenständige WinGet-Zwischenprüfung
/// für einen laufenden Kundencheckup durch.
///
/// Der Eingangsscan, bestehende Wartungsaktionen
/// und der spätere Abschlussscan bleiben erhalten.
///
/// Die neue Zwischenprüfung wird erst nach einer
/// erfolgreichen Geräteprüfung erstellt und
/// anschließend über den bestehenden Kundenservice
/// dauerhaft gespeichert.
/// </summary>
public sealed class ProgramUpdateIntermediateScanService
{
    private const string ExecutionActionCode =
        "control.program-updates.intermediate-scan";

    private const string ExecutionActionTitle =
        "Programmupdates erneut prüfen";

    private readonly IProgramUpdateInformationProvider
        _programUpdateInformationProvider;

    private readonly IDeviceInformationScanner
        _deviceInformationScanner;

    private readonly IDeviceIdentityService
        _deviceIdentityService;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    private readonly ICustomerService
        _customerService;

    public ProgramUpdateIntermediateScanService(
        IProgramUpdateInformationProvider
            programUpdateInformationProvider,
        IDeviceInformationScanner deviceInformationScanner,
        IDeviceIdentityService deviceIdentityService,
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator,
        ICustomerService customerService)
    {
        ArgumentNullException.ThrowIfNull(
            programUpdateInformationProvider);

        ArgumentNullException.ThrowIfNull(
            deviceInformationScanner);

        ArgumentNullException.ThrowIfNull(
            deviceIdentityService);

        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        ArgumentNullException.ThrowIfNull(
            customerService);

        _programUpdateInformationProvider =
            programUpdateInformationProvider;

        _deviceInformationScanner =
            deviceInformationScanner;

        _deviceIdentityService =
            deviceIdentityService;

        _executionCoordinator =
            executionCoordinator;

        _customerService =
            customerService;
    }

    /// <summary>
    /// Startet eine lesende WinGet-Zwischenprüfung.
    ///
    /// Die langsamen Systemabfragen werden
    /// außerhalb des UI-Threads ausgeführt.
    ///
    /// Die Speicherung findet erst statt, wenn
    /// Gerät und Kundencheckup erneut geprüft
    /// wurden.
    ///
    /// Das Ergebnis wird unabhängig kopiert.
    /// </summary>
    public async Task<ProgramUpdateIntermediateScan>
        RunAsync(
            Customer customer,
            CustomerDevice device)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(device);

        ValidateCustomerDevice(
            customer,
            device);

        var session =
            device.CheckupSession;

        var currentVisit =
            GetEditableVisit(session);

        // =============================================
        // GLEICHZEITIGE SYSTEMAKTIONEN VERHINDERN
        // =============================================

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                ExecutionActionCode,
                ExecutionActionTitle);

        if (executionLease is null)
        {
            var activeAction =
                _executionCoordinator.ActiveActionTitle;

            throw new InvalidOperationException(
                "Die WinGet-Zwischenprüfung kann "
                + "nicht gestartet werden, weil "
                + "bereits eine andere Systemaktion "
                + "ausgeführt wird."
                + (string.IsNullOrWhiteSpace(activeAction)
                    ? string.Empty
                    : Environment.NewLine
                      + Environment.NewLine
                      + "Aktive Aktion: "
                      + activeAction));
        }

        // =============================================
        // GERÄTEIDENTITÄT ERNEUT PRÜFEN
        // =============================================

        var deviceScan =
            await Task.Run(
                () => _deviceInformationScanner.Scan());

        if (!deviceScan.IsSuccessful
            || deviceScan.Data is null)
        {
            throw new InvalidOperationException(
                "Die Identität des aktuell "
                + "angeschlossenen Computers "
                + "konnte nicht zuverlässig "
                + "ermittelt werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Es wurde keine WinGet-"
                + "Zwischenprüfung durchgeführt.");
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                customer.Devices,
                deviceScan.Data);

        if (matchingDevice is null
            || matchingDevice.Id != device.Id)
        {
            throw new InvalidOperationException(
                "Der aktuell untersuchte Computer "
                + "stimmt nicht eindeutig mit dem "
                + "ausgewählten Kundengerät überein."
                + Environment.NewLine
                + Environment.NewLine
                + "Es wurde keine WinGet-"
                + "Zwischenprüfung durchgeführt "
                + "oder gespeichert.");
        }

        // =============================================
        // BESTEHENDE WINGet-ANALYSE AUSFÜHREN
        // =============================================

        var information =
            await Task.Run(
                () => _programUpdateInformationProvider
                    .GetProgramUpdateInformation());

        if (information is null)
        {
            throw new InvalidOperationException(
                "Die WinGet-Analyse hat kein "
                + "verwertbares Ergebnisobjekt "
                + "zurückgegeben."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Kundencheckup bleibt "
                + "unverändert.");
        }

        // =============================================
        // VORGANG NACH DER ANALYSE ERNEUT PRÜFEN
        // =============================================

        ValidateCustomerDevice(
            customer,
            device);

        if (!ReferenceEquals(
                device.CheckupSession,
                session))
        {
            throw new InvalidOperationException(
                "Der gespeicherte Gerätestand "
                + "hat sich während der "
                + "WinGet-Analyse geändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Das Ergebnis wurde vorsorglich "
                + "nicht übernommen.");
        }

        var activeVisit =
            GetEditableVisit(session);

        if (!ReferenceEquals(
                activeVisit,
                currentVisit))
        {
            throw new InvalidOperationException(
                "Der aktive Kundencheckup hat "
                + "sich während der "
                + "WinGet-Analyse geändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Das Ergebnis wurde vorsorglich "
                + "nicht gespeichert.");
        }

        // =============================================
        // UNABHÄNGIGE VORGANGSKOPIE ERZEUGEN
        // =============================================

        var updatedVisit =
            currentVisit.CreateIndependentCopy();

        var recordedScan =
            updatedVisit.RecordProgramUpdateIntermediateScan(
                information);

        // =============================================
        // BESTEHENDEN SPEICHERWEG VERWENDEN
        // =============================================

        var previousVisits =
            session.CustomerCheckupVisits;

        var previousUpdatedAt =
            device.UpdatedAt;

        session.CustomerCheckupVisits =
            previousVisits
                .Select(visit =>
                    ReferenceEquals(
                        visit,
                        currentVisit)
                        ? updatedVisit
                        : visit)
                .ToList();

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var saved =
                _customerService.UpdateCustomerDevice(
                    customer.Id,
                    device);

            if (!saved)
            {
                throw new InvalidOperationException(
                    "Der Kunde oder das ausgewählte "
                    + "Gerät ist in der Datenbank "
                    + "nicht mehr vorhanden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Die Zwischenprüfung konnte "
                    + "nicht gespeichert werden.");
            }
        }
        catch
        {
            // =========================================
            // FEHLGESCHLAGENE SPEICHERUNG ZURÜCKNEHMEN
            // =========================================

            session.CustomerCheckupVisits =
                previousVisits;

            device.UpdatedAt =
                previousUpdatedAt;

            throw;
        }

        // =============================================
        // ERFOLGREICH GESPEICHERTES ERGEBNIS
        // =============================================

        return recordedScan;
    }

    /// <summary>
    /// Prüft, ob das ausgewählte Gerät weiterhin
    /// tatsächlich zum angegebenen Kunden gehört.
    /// </summary>
    private static void ValidateCustomerDevice(
        Customer customer,
        CustomerDevice device)
    {
        if (customer.Id == Guid.Empty
            || device.Id == Guid.Empty
            || !customer.Devices.Any(
                storedDevice =>
                    ReferenceEquals(
                        storedDevice,
                        device)))
        {
            throw new InvalidOperationException(
                "Das ausgewählte Gerät konnte "
                + "dem Kunden nicht eindeutig "
                + "zugeordnet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Es wurde keine Zwischenprüfung "
                + "gespeichert.");
        }
    }

    /// <summary>
    /// Liefert ausschließlich einen laufenden,
    /// noch nicht zum Abschluss vorbereiteten
    /// Kundencheckup.
    /// </summary>
    private static CustomerCheckupVisit
        GetEditableVisit(
            CheckupSession session)
    {
        var visit =
            session.CurrentCustomerCheckupVisit;

        if (visit is null
            || !visit.IsInProgress)
        {
            throw new InvalidOperationException(
                "Für dieses Gerät ist derzeit "
                + "kein bearbeitbarer Kundencheckup "
                + "vorhanden.");
        }

        if (visit.IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Für diesen Kundencheckup wurde "
                + "bereits eine Abschlusskontrolle "
                + "vorbereitet."
                + Environment.NewLine
                + Environment.NewLine
                + "Weitere WinGet-Zwischenprüfungen "
                + "sind deshalb nicht zulässig.");
        }

        return visit;
    }
}
