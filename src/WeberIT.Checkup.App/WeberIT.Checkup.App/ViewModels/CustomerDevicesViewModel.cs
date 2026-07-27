using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerDevicesViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly ICheckupScanner _checkupScanner;

    private readonly ICheckupAssessmentService
        _checkupAssessmentService;

    private readonly IDeviceIdentityService
        _deviceIdentityService;

    private readonly IDialogService _dialogService;

    private Customer? _selectedCustomer;
    private CustomerDevice? _selectedDevice;
    private CheckupTaskList? _subscribedTaskList;

    public CustomerDevicesViewModel(
        ICustomerService customerService,
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        IDeviceIdentityService deviceIdentityService,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(
            customerService);

        ArgumentNullException.ThrowIfNull(
            checkupScanner);

        ArgumentNullException.ThrowIfNull(
            checkupAssessmentService);

        ArgumentNullException.ThrowIfNull(
            deviceIdentityService);

        ArgumentNullException.ThrowIfNull(
            dialogService);

        _customerService =
            customerService;

        _checkupScanner =
            checkupScanner;

        _checkupAssessmentService =
            checkupAssessmentService;

        _deviceIdentityService =
            deviceIdentityService;

        _dialogService =
            dialogService;

        AddDeviceCommand =
            new RelayCommand(
                _ =>
                    AddDevice(),
                _ =>
                    SelectedCustomer is not null);

        RescanDeviceCommand =
            new RelayCommand(
                _ =>
                    ExecuteCustomerCheckupAction(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null);

        DeleteDeviceCommand =
            new RelayCommand(
                _ =>
                    DeleteDevice(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && !HasSelectedDeviceInProgressCheckup);
    }

    public RelayCommand AddDeviceCommand { get; }

    public RelayCommand RescanDeviceCommand { get; }

    public RelayCommand DeleteDeviceCommand { get; }

    public Customer? SelectedCustomer
    {
        get =>
            _selectedCustomer;

        set
        {
            if (_selectedCustomer == value)
            {
                return;
            }

            SelectedDevice =
                null;

            _selectedCustomer =
                value;

            OnPropertyChanged();

            OnPropertyChanged(
                nameof(Devices));

            OnPropertyChanged(
                nameof(DeviceCountText));

            OnPropertyChanged(
                nameof(HasSelectedDeviceInProgressCheckup));

            OnPropertyChanged(
                nameof(RescanDeviceButtonText));

            AddDeviceCommand
                .RaiseCanExecuteChanged();

            RescanDeviceCommand
                .RaiseCanExecuteChanged();

            DeleteDeviceCommand
                .RaiseCanExecuteChanged();
        }
    }

    public CustomerDevice? SelectedDevice
    {
        get =>
            _selectedDevice;

        set
        {
            if (_selectedDevice == value)
            {
                return;
            }

            UnsubscribeFromSelectedTaskList();

            _selectedDevice =
                value;

            SubscribeToSelectedTaskList();

            OnPropertyChanged();

            OnPropertyChanged(
                nameof(HasSelectedDeviceInProgressCheckup));

            OnPropertyChanged(
                nameof(RescanDeviceButtonText));

            RescanDeviceCommand
                .RaiseCanExecuteChanged();

            DeleteDeviceCommand
                .RaiseCanExecuteChanged();
        }
    }

    public IEnumerable<CustomerDevice> Devices =>
        SelectedCustomer?.Devices.ToList()
        ?? Enumerable.Empty<CustomerDevice>();

    public string DeviceCountText
    {
        get
        {
            var count =
                SelectedCustomer?.Devices.Count
                ?? 0;

            return count == 1
                ? "1 Gerät gespeichert"
                : $"{count} Geräte gespeichert";
        }
    }

    public bool HasSelectedDeviceInProgressCheckup =>
        SelectedDevice?
            .CheckupSession
            .HasInProgressCustomerCheckupVisit
        ?? false;

    public string RescanDeviceButtonText =>
        HasSelectedDeviceInProgressCheckup
            ? "Abschlusskontrolle"
            : "Checkup starten";

    private void AddDevice()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var checkupSession =
            TryCreateCheckupSession();

        if (checkupSession is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                checkupSession.DeviceInformation);

        if (matchingDevice is not null)
        {
            StartCheckupForMatchingDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        AddNewDevice(
            checkupSession);
    }

    private void StartCheckupForMatchingDevice(
        CustomerDevice matchingDevice,
        CheckupSession checkupSession)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        if (matchingDevice
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                matchingDevice);

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Gerät bereits vorhanden",
                $"Das Gerät \"{matchingDevice.DisplayName}\" "
                + "ist diesem Kunden bereits zugeordnet."
                + Environment.NewLine
                + Environment.NewLine
                + "Soll für dieses Gerät ein neuer "
                + "Kundencheckup gestartet werden?"
                + Environment.NewLine
                + Environment.NewLine
                + "Der neue Scan wird als unveränderlicher "
                + "Vorher-Zustand des Vorgangs gesichert.");

        if (!confirmed)
        {
            return;
        }

        StartCustomerCheckupOnDevice(
            matchingDevice,
            checkupSession);
    }

    private void AddNewDevice(
        CheckupSession checkupSession)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var displayName =
            !string.IsNullOrWhiteSpace(
                checkupSession.DeviceInformation.Name)
                ? checkupSession.DeviceInformation.Name
                : $"Gerät "
                  + $"{SelectedCustomer.Devices.Count + 1}";

        var device =
            new CustomerDevice
            {
                DisplayName =
                    displayName,

                CheckupSession =
                    checkupSession
            };

        try
        {
            var wasAdded =
                _customerService.AddDeviceToCustomer(
                    SelectedCustomer.Id,
                    device);

            if (!wasAdded)
            {
                ShowPersistenceError(
                    "Gerät konnte nicht gespeichert werden",
                    "Der ausgewählte Kunde ist in der "
                    + "Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Gerät konnte nicht gespeichert werden",
                exception.Message);

            return;
        }

        SelectedCustomer.Devices.Add(
            device);

        SelectedDevice =
            device;

        RefreshDeviceDisplay();
    }

    private void ExecuteCustomerCheckupAction()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        if (HasSelectedDeviceInProgressCheckup)
        {
            CompleteCustomerCheckup();

            return;
        }

        StartCustomerCheckup();
    }

    private void StartCustomerCheckup()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice =
            SelectedDevice;

        if (selectedDevice
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                selectedDevice);

            return;
        }

        var checkupSession =
            TryCreateCheckupSession();

        if (checkupSession is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                checkupSession.DeviceInformation);

        if (matchingDevice is not null
            && matchingDevice.Id
                != selectedDevice.Id)
        {
            var confirmed =
                _dialogService.Confirm(
                    "Anderes Gerät erkannt",
                    $"Der Eingangsscan gehört nicht zum "
                    + $"ausgewählten Gerät "
                    + $"\"{selectedDevice.DisplayName}\", "
                    + $"sondern zum bereits gespeicherten "
                    + $"Gerät \"{matchingDevice.DisplayName}\"."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Soll der Kundencheckup stattdessen "
                    + "für das erkannte Gerät gestartet werden?");

            if (!confirmed)
            {
                return;
            }

            StartCustomerCheckupOnDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        if (matchingDevice is null)
        {
            var confirmed =
                _dialogService.Confirm(
                    "Gerät nicht eindeutig erkannt",
                    $"Der Eingangsscan konnte dem ausgewählten "
                    + $"Gerät \"{selectedDevice.DisplayName}\" "
                    + "nicht eindeutig zugeordnet werden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Soll der Kundencheckup trotzdem für "
                    + "dieses Gerät gestartet und der Scan "
                    + "als Vorher-Zustand gesichert werden?");

            if (!confirmed)
            {
                return;
            }
        }

        StartCustomerCheckupOnDevice(
            selectedDevice,
            checkupSession);
    }

    private void CompleteCustomerCheckup()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice =
            SelectedDevice;

        var currentVisit =
            selectedDevice
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        if (currentVisit is null)
        {
            RefreshDeviceDisplay();

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Abschlusskontrolle starten",
                $"Für das Gerät \"{selectedDevice.DisplayName}\" "
                + "wird jetzt ein vollständiger Kontrollscan "
                + "durchgeführt."
                + Environment.NewLine
                + Environment.NewLine
                + "Der vorhandene Eingangsscan bleibt als "
                + "Vorher-Zustand erhalten. Der neue Scan wird "
                + "als Nachher-Zustand des Kundencheckups "
                + "gespeichert."
                + Environment.NewLine
                + Environment.NewLine
                + "Soll die Abschlusskontrolle jetzt starten?");

        if (!confirmed)
        {
            return;
        }

        var afterCheckup =
            TryCreateCheckupSession();

        if (afterCheckup is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                afterCheckup.DeviceInformation);

        if (matchingDevice is not null
            && matchingDevice.Id
                != selectedDevice.Id)
        {
            _dialogService.ShowError(
                "Falsches Gerät erkannt",
                $"Der Kontrollscan gehört nicht zum "
                + $"ausgewählten Gerät "
                + $"\"{selectedDevice.DisplayName}\", "
                + $"sondern zum gespeicherten Gerät "
                + $"\"{matchingDevice.DisplayName}\"."
                + Environment.NewLine
                + Environment.NewLine
                + "Der laufende Kundencheckup wurde nicht "
                + "verändert. Schließen Sie das richtige "
                + "Gerät an und starten Sie die "
                + "Abschlusskontrolle erneut.");

            return;
        }

        if (matchingDevice is null)
        {
            var continueDespiteUnclearIdentity =
                _dialogService.Confirm(
                    "Gerät nicht eindeutig erkannt",
                    $"Der Kontrollscan konnte dem Gerät "
                    + $"\"{selectedDevice.DisplayName}\" "
                    + "nicht eindeutig zugeordnet werden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Soll der Scan trotzdem als "
                    + "Nachher-Zustand dieses Kundencheckups "
                    + "gespeichert werden?");

            if (!continueDespiteUnclearIdentity)
            {
                return;
            }
        }

        CompleteCustomerCheckupOnDevice(
            selectedDevice,
            afterCheckup);
    }

    private void StartCustomerCheckupOnDevice(
        CustomerDevice device,
        CheckupSession entranceCheckup)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        if (device
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                device);

            return;
        }

        var previousDisplayName =
            device.DisplayName;

        var previousCheckupSession =
            device.CheckupSession;

        var previousUpdatedAt =
            device.UpdatedAt;

        CustomerCheckupVisit customerCheckupVisit;

        try
        {
            customerCheckupVisit =
                CustomerCheckupVisit.Start(
                    entranceCheckup);
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Kundencheckup konnte nicht gestartet werden",
                exception.Message);

            return;
        }

        entranceCheckup.CustomerCheckupVisits =
            previousCheckupSession
                .CustomerCheckupVisits
                .ToList();

        entranceCheckup.CustomerCheckupVisits.Add(
            customerCheckupVisit);

        var scannedComputerName =
            entranceCheckup
                .DeviceInformation
                .Name;

        if (!string.IsNullOrWhiteSpace(
                scannedComputerName))
        {
            device.DisplayName =
                scannedComputerName;
        }

        device.CheckupSession =
            entranceCheckup;

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    device);

            if (!wasUpdated)
            {
                RestoreDevice(
                    device,
                    previousDisplayName,
                    previousCheckupSession,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Kundencheckup konnte nicht gestartet werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestoreDevice(
                device,
                previousDisplayName,
                previousCheckupSession,
                previousUpdatedAt);

            ShowPersistenceError(
                "Kundencheckup konnte nicht gestartet werden",
                exception.Message);

            return;
        }

        SelectedDevice =
            device;

        RefreshTaskListSubscription();
        RefreshDeviceDisplay();
    }

    private void CompleteCustomerCheckupOnDevice(
        CustomerDevice device,
        CheckupSession afterCheckup)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var previousDisplayName =
            device.DisplayName;

        var previousCheckupSession =
            device.CheckupSession;

        var previousUpdatedAt =
            device.UpdatedAt;

        var currentVisit =
            previousCheckupSession
                .CurrentCustomerCheckupVisit;

        if (currentVisit is null)
        {
            _dialogService.ShowError(
                "Kein laufender Kundencheckup",
                "Für das ausgewählte Gerät wurde kein "
                + "laufender Kundencheckup gefunden. "
                + "Die Abschlusskontrolle wurde nicht "
                + "gespeichert.");

            return;
        }

        CustomerCheckupVisit completedVisit;

        try
        {
            completedVisit =
                CreateIndependentVisitCopy(
                    currentVisit);

            completedVisit.Complete(
                afterCheckup,
                currentVisit.TechnicianSummary,
                currentVisit.NextSteps,
                currentVisit.NextCheckupDate);
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Kundencheckup konnte nicht abgeschlossen werden",
                exception.Message);

            return;
        }

        afterCheckup.CustomerCheckupVisits =
            previousCheckupSession
                .CustomerCheckupVisits
                .Select(
                    visit =>
                        visit.Id == currentVisit.Id
                            ? completedVisit
                            : visit)
                .ToList();

        var scannedComputerName =
            afterCheckup
                .DeviceInformation
                .Name;

        if (!string.IsNullOrWhiteSpace(
                scannedComputerName))
        {
            device.DisplayName =
                scannedComputerName;
        }

        device.CheckupSession =
            afterCheckup;

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    device);

            if (!wasUpdated)
            {
                RestoreDevice(
                    device,
                    previousDisplayName,
                    previousCheckupSession,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Kundencheckup konnte nicht abgeschlossen werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestoreDevice(
                device,
                previousDisplayName,
                previousCheckupSession,
                previousUpdatedAt);

            ShowPersistenceError(
                "Kundencheckup konnte nicht abgeschlossen werden",
                exception.Message);

            return;
        }

        SelectedDevice =
            device;

        RefreshTaskListSubscription();
        RefreshDeviceDisplay();
    }

    private void DeleteDevice()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var device =
            SelectedDevice;

        if (device
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                device);

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Gerät löschen",
                $"Soll das Gerät \"{device.DisplayName}\" "
                + "wirklich gelöscht werden?");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var wasDeleted =
                _customerService.DeleteCustomerDevice(
                    SelectedCustomer.Id,
                    device.Id);

            if (!wasDeleted)
            {
                ShowPersistenceError(
                    "Gerät konnte nicht gelöscht werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Gerät konnte nicht gelöscht werden",
                exception.Message);

            return;
        }

        SelectedCustomer.Devices.Remove(
            device);

        SelectedDevice =
            null;

        RefreshDeviceDisplay();
    }

    private CheckupSession? TryCreateCheckupSession()
    {
        try
        {
            var checkupSession =
                _checkupScanner.Scan();

            checkupSession.Assessment =
                _checkupAssessmentService.Assess(
                    checkupSession);

            return checkupSession;
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Systemscan fehlgeschlagen",
                BuildScanErrorMessage(
                    exception));

            return null;
        }
    }

    private void SubscribeToSelectedTaskList()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        _subscribedTaskList =
            SelectedDevice
                .CheckupSession
                .TaskList;

        _subscribedTaskList.PersistenceRequested +=
            SelectedTaskList_OnPersistenceRequested;
    }

    private void UnsubscribeFromSelectedTaskList()
    {
        if (_subscribedTaskList is null)
        {
            return;
        }

        _subscribedTaskList.PersistenceRequested -=
            SelectedTaskList_OnPersistenceRequested;

        _subscribedTaskList =
            null;
    }

    private void RefreshTaskListSubscription()
    {
        UnsubscribeFromSelectedTaskList();
        SubscribeToSelectedTaskList();
    }

    private void SelectedTaskList_OnPersistenceRequested(
        object? sender,
        EventArgs e)
    {
        PersistSelectedDeviceTaskList();
    }

    private void PersistSelectedDeviceTaskList()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            var message =
                "Für die Aufgabenstatusänderung ist kein "
                + "gespeichertes Kundengerät ausgewählt.";

            ShowTaskPersistenceError(
                message);

            throw new InvalidOperationException(
                message);
        }

        var previousUpdatedAt =
            SelectedDevice.UpdatedAt;

        SelectedDevice.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    SelectedDevice);

            if (!wasUpdated)
            {
                throw new InvalidOperationException(
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");
            }
        }
        catch (Exception exception)
        {
            SelectedDevice.UpdatedAt =
                previousUpdatedAt;

            ShowTaskPersistenceError(
                exception.Message);

            throw new InvalidOperationException(
                "Der Aufgabenstatus konnte nicht "
                + "dauerhaft gespeichert werden.",
                exception);
        }

        OnPropertyChanged(
            nameof(SelectedDevice));
    }

    private void RefreshDeviceDisplay()
    {
        OnPropertyChanged(
            nameof(Devices));

        OnPropertyChanged(
            nameof(DeviceCountText));

        OnPropertyChanged(
            nameof(SelectedDevice));

        OnPropertyChanged(
            nameof(HasSelectedDeviceInProgressCheckup));

        OnPropertyChanged(
            nameof(RescanDeviceButtonText));

        RescanDeviceCommand
            .RaiseCanExecuteChanged();

        DeleteDeviceCommand
            .RaiseCanExecuteChanged();
    }

    private void ShowActiveCheckupError(
        CustomerDevice device)
    {
        var currentVisit =
            device
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        var startedAtText =
            currentVisit?.StartedAtText
            ?? "unbekanntem Zeitpunkt";

        _dialogService.ShowError(
            "Kundencheckup bereits aktiv",
            $"Für das Gerät \"{device.DisplayName}\" "
            + "läuft bereits ein Kundencheckup."
            + Environment.NewLine
            + Environment.NewLine
            + $"Der Eingangsscan vom {startedAtText} "
            + "ist als Vorher-Zustand gesichert."
            + Environment.NewLine
            + Environment.NewLine
            + "Führen Sie über den Button "
            + "\"Abschlusskontrolle\" den Nachher-Scan durch.");
    }

    private void ShowTaskPersistenceError(
        string errorDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                errorDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : errorDetails;

        _dialogService.ShowError(
            "Aufgabenstatus nicht gespeichert",
            "Die Statusänderung konnte nicht dauerhaft "
            + "gespeichert werden und wurde deshalb "
            + "zurückgenommen."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {details}");
    }

    private void ShowPersistenceError(
        string title,
        string errorDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                errorDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : errorDetails;

        _dialogService.ShowError(
            title,
            "Die Änderung konnte nicht dauerhaft in "
            + "der Datenbank gespeichert werden. "
            + "Die Geräteliste wurde nicht als "
            + "erfolgreich aktualisiert."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {details}");
    }

    private static CustomerCheckupVisit
        CreateIndependentVisitCopy(
            CustomerCheckupVisit source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return new CustomerCheckupVisit
        {
            VisitModelVersion =
                source.VisitModelVersion,

            Id =
                source.Id,

            StartedAt =
                source.StartedAt,

            CompletedAt =
                source.CompletedAt,

            Status =
                source.Status,

            BeforeCheckup =
                source.BeforeCheckup,

            AfterCheckup =
                source.AfterCheckup,

            TechnicianSummary =
                source.TechnicianSummary,

            NextSteps =
                source.NextSteps,

            NextCheckupDate =
                source.NextCheckupDate,

            CancellationReason =
                source.CancellationReason
        };
    }

    private static void RestoreDevice(
        CustomerDevice device,
        string displayName,
        CheckupSession checkupSession,
        DateTime? updatedAt)
    {
        device.DisplayName =
            displayName;

        device.CheckupSession =
            checkupSession;

        device.UpdatedAt =
            updatedAt;
    }

    private static string BuildScanErrorMessage(
        Exception exception)
    {
        var errorDetails =
            string.IsNullOrWhiteSpace(
                exception.Message)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : exception.Message;

        return
            "Die Systeminformationen konnten nicht "
            + "vollständig ausgelesen oder bewertet werden. "
            + "Der gespeicherte Gerätestand und ein eventuell "
            + "laufender Kundencheckup bleiben unverändert."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {errorDetails}";
    }
}