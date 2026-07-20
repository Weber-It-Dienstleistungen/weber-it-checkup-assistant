using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerDevicesViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;
    private readonly IDeviceIdentityService _deviceIdentityService;
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
                _ => AddDevice(),
                _ => SelectedCustomer is not null);

        RescanDeviceCommand =
            new RelayCommand(
                _ => RescanDevice(),
                _ => SelectedCustomer is not null
                     && SelectedDevice is not null);

        DeleteDeviceCommand =
            new RelayCommand(
                _ => DeleteDevice(),
                _ => SelectedCustomer is not null
                     && SelectedDevice is not null);
    }

    public RelayCommand AddDeviceCommand { get; }

    public RelayCommand RescanDeviceCommand { get; }

    public RelayCommand DeleteDeviceCommand { get; }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
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
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));

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
        get => _selectedDevice;
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
            UpdateMatchingDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        AddNewDevice(
            checkupSession);
    }

    private void UpdateMatchingDevice(
        CustomerDevice matchingDevice,
        CheckupSession checkupSession)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Gerät bereits vorhanden",
                $"Das Gerät \"{matchingDevice.DisplayName}\" "
                + "ist diesem Kunden bereits zugeordnet. "
                + "Soll der vorhandene Systemcheck durch "
                + "den neuen Scan ersetzt werden?");

        if (!confirmed)
        {
            return;
        }

        ApplyCheckupToDevice(
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
                : $"Gerät {SelectedCustomer.Devices.Count + 1}";

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

    private void RescanDevice()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice =
            SelectedDevice;

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
                    $"Der neue Scan gehört nicht zum "
                    + $"ausgewählten Gerät "
                    + $"\"{selectedDevice.DisplayName}\", "
                    + $"sondern zum bereits gespeicherten "
                    + $"Gerät \"{matchingDevice.DisplayName}\". "
                    + "Soll stattdessen dieses erkannte "
                    + "Gerät aktualisiert werden?");

            if (!confirmed)
            {
                return;
            }

            ApplyCheckupToDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        if (matchingDevice is null)
        {
            var confirmed =
                _dialogService.Confirm(
                    "Gerät nicht eindeutig erkannt",
                    $"Der neue Scan konnte dem ausgewählten "
                    + $"Gerät \"{selectedDevice.DisplayName}\" "
                    + "nicht eindeutig zugeordnet werden. "
                    + "Soll der gespeicherte Systemcheck "
                    + "trotzdem durch die neuen Daten "
                    + "ersetzt werden?");

            if (!confirmed)
            {
                return;
            }
        }

        ApplyCheckupToDevice(
            selectedDevice,
            checkupSession);
    }

    private void ApplyCheckupToDevice(
        CustomerDevice device,
        CheckupSession checkupSession)
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

        var scannedComputerName =
            checkupSession.DeviceInformation.Name;

        if (!string.IsNullOrWhiteSpace(
                scannedComputerName))
        {
            device.DisplayName =
                scannedComputerName;
        }

        device.CheckupSession =
            checkupSession;

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
                    "Gerät konnte nicht aktualisiert werden",
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
                "Gerät konnte nicht aktualisiert werden",
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
            + "Es wurde kein Gerät angelegt und kein "
            + "vorhandener Systemcheck überschrieben."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {errorDetails}";
    }
}