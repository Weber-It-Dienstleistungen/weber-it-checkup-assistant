using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CheckupViewModel : BaseViewModel
{
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;
    private readonly ICustomerService _customerService;
    private readonly IDeviceIdentityService _deviceIdentityService;
    private readonly IDialogService _dialogService;

    private readonly RelayCommand _saveCheckupCommand;

    private Customer? _selectedCustomer;
    private CheckupSession _currentCheckup = new();
    private Guid? _savedCustomerId;
    private Guid? _savedDeviceId;
    private bool _lastSaveUpdatedExistingDevice;

    public CheckupViewModel(
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        ICustomerService customerService,
        IDeviceIdentityService deviceIdentityService,
        IDialogService dialogService)
    {
        _checkupScanner = checkupScanner;
        _checkupAssessmentService = checkupAssessmentService;
        _customerService = customerService;
        _deviceIdentityService = deviceIdentityService;
        _dialogService = dialogService;

        SubscribeToTaskList(
            _currentCheckup.TaskList);

        ReadSystemCommand =
            new RelayCommand(_ => ReadSystem());

        _saveCheckupCommand =
            new RelayCommand(
                _ => SaveCheckup(),
                _ => CanSaveCheckup);

        SaveCheckupCommand = _saveCheckupCommand;
    }

    public string Title => "Gerät / Checkup";

    public string Subtitle =>
        "Systeminformationen auslesen, bewerten und bei Bedarf dauerhaft einem Kunden zuordnen.";

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        private set
        {
            if (_selectedCustomer == value)
            {
                return;
            }

            _selectedCustomer = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCustomerText));
            OnPropertyChanged(nameof(HasSelectedCustomer));
            OnPropertyChanged(nameof(IsCurrentCheckupSaved));
            OnPropertyChanged(nameof(CanSaveCheckup));
            OnPropertyChanged(nameof(PersistenceStatusText));

            _saveCheckupCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedCustomer =>
        SelectedCustomer is not null;

    public string SelectedCustomerText =>
        SelectedCustomer is not null
            ? $"Aktiver Kunde: {SelectedCustomer.CustomerNumber} - {SelectedCustomer.DisplayName}"
            : "Kein Kunde ausgewählt.";

    public CheckupSession CurrentCheckup
    {
        get => _currentCheckup;
        private set
        {
            if (ReferenceEquals(
                    _currentCheckup,
                    value))
            {
                return;
            }

            UnsubscribeFromTaskList(
                _currentCheckup.TaskList);

            _currentCheckup =
                value;

            SubscribeToTaskList(
                _currentCheckup.TaskList);

            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceInformation));
            OnPropertyChanged(nameof(HardwareInformation));
            OnPropertyChanged(nameof(OperatingSystemInformation));
            OnPropertyChanged(nameof(StorageInformation));
            OnPropertyChanged(nameof(Assessment));
            OnPropertyChanged(nameof(ScanDate));
            OnPropertyChanged(nameof(HasCurrentCheckup));
            OnPropertyChanged(nameof(IsCurrentCheckupSaved));
            OnPropertyChanged(nameof(CanSaveCheckup));
            OnPropertyChanged(nameof(ScanStatusText));
            OnPropertyChanged(nameof(PersistenceStatusText));

            _saveCheckupCommand.RaiseCanExecuteChanged();
        }
    }

    public DeviceInformation DeviceInformation =>
        CurrentCheckup.DeviceInformation;

    public HardwareInformation HardwareInformation =>
        CurrentCheckup.HardwareInformation;

    public OperatingSystemInformation OperatingSystemInformation =>
        CurrentCheckup.OperatingSystemInformation;

    public StorageInformation StorageInformation =>
        CurrentCheckup.StorageInformation;

    public CheckupAssessment Assessment =>
        CurrentCheckup.Assessment;

    public DateTime? ScanDate =>
        CurrentCheckup.ScanDate;

    public bool HasCurrentCheckup =>
        ScanDate.HasValue;

    public bool IsCurrentCheckupSaved =>
        SelectedCustomer is not null
        && _savedCustomerId
            == SelectedCustomer.Id
        && _savedDeviceId.HasValue;

    public bool CanSaveCheckup =>
        HasCurrentCheckup
        && SelectedCustomer is not null
        && !IsCurrentCheckupSaved;

    public string ScanStatusText =>
        ScanDate.HasValue
            ? $"Letzter Scan: {ScanDate.Value:dd.MM.yyyy HH:mm}"
            : "Noch kein Systemscan durchgeführt.";

    public string PersistenceStatusText
    {
        get
        {
            if (!HasCurrentCheckup)
            {
                return "Noch keine Daten zum Speichern vorhanden.";
            }

            if (SelectedCustomer is null)
            {
                return "Der Scan ist nicht dauerhaft gespeichert, da kein Kunde ausgewählt ist.";
            }

            if (IsCurrentCheckupSaved)
            {
                return _lastSaveUpdatedExistingDevice
                    ? $"Das vorhandene Gerät bei {SelectedCustomer.DisplayName} wurde aktualisiert."
                    : $"Dauerhaft bei {SelectedCustomer.DisplayName} gespeichert.";
            }

            return $"Der Scan wurde noch nicht bei {SelectedCustomer.DisplayName} gespeichert.";
        }
    }

    public ICommand ReadSystemCommand { get; }

    public ICommand SaveCheckupCommand { get; }

    public void SetCustomer(Customer? customer)
    {
        SelectedCustomer =
            customer;
    }

    private void ReadSystem()
    {
        try
        {
            var checkupSession =
                _checkupScanner.Scan();

            checkupSession.Assessment =
                _checkupAssessmentService.Assess(
                    checkupSession);

            _savedCustomerId =
                null;

            _savedDeviceId =
                null;

            _lastSaveUpdatedExistingDevice =
                false;

            CurrentCheckup =
                checkupSession;
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Systemscan fehlgeschlagen",
                BuildScanErrorMessage(
                    exception));
        }
    }

    private void SaveCheckup()
    {
        if (!CanSaveCheckup
            || SelectedCustomer is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                CurrentCheckup.DeviceInformation);

        if (matchingDevice is not null)
        {
            UpdateExistingDevice(
                matchingDevice);

            return;
        }

        AddNewDevice();
    }

    private void UpdateExistingDevice(
        CustomerDevice matchingDevice)
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

        var previousDisplayName =
            matchingDevice.DisplayName;

        var previousCheckupSession =
            matchingDevice.CheckupSession;

        var previousUpdatedAt =
            matchingDevice.UpdatedAt;

        var scannedComputerName =
            CurrentCheckup.DeviceInformation.Name;

        if (!string.IsNullOrWhiteSpace(
                scannedComputerName))
        {
            matchingDevice.DisplayName =
                scannedComputerName;
        }

        matchingDevice.CheckupSession =
            CurrentCheckup;

        matchingDevice.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    matchingDevice);

            if (!wasUpdated)
            {
                RestoreDevice(
                    matchingDevice,
                    previousDisplayName,
                    previousCheckupSession,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestoreDevice(
                matchingDevice,
                previousDisplayName,
                previousCheckupSession,
                previousUpdatedAt);

            ShowPersistenceError(
                exception.Message);

            return;
        }

        CompleteSave(
            true,
            matchingDevice.Id);
    }

    private void AddNewDevice()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var displayName =
            !string.IsNullOrWhiteSpace(
                CurrentCheckup.DeviceInformation.Name)
                ? CurrentCheckup.DeviceInformation.Name
                : $"Gerät {SelectedCustomer.Devices.Count + 1}";

        var device =
            new CustomerDevice
            {
                DisplayName =
                    displayName,

                CheckupSession =
                    CurrentCheckup
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
                    "Der ausgewählte Kunde ist in der "
                    + "Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                exception.Message);

            return;
        }

        SelectedCustomer.Devices.Add(
            device);

        CompleteSave(
            false,
            device.Id);
    }

    private void CompleteSave(
        bool updatedExistingDevice,
        Guid savedDeviceId)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        _savedCustomerId =
            SelectedCustomer.Id;

        _savedDeviceId =
            savedDeviceId;

        _lastSaveUpdatedExistingDevice =
            updatedExistingDevice;

        OnPropertyChanged(
            nameof(IsCurrentCheckupSaved));

        OnPropertyChanged(
            nameof(CanSaveCheckup));

        OnPropertyChanged(
            nameof(PersistenceStatusText));

        _saveCheckupCommand
            .RaiseCanExecuteChanged();
    }

    private void SubscribeToTaskList(
        CheckupTaskList taskList)
    {
        taskList.PersistenceRequested +=
            CurrentTaskList_OnPersistenceRequested;
    }

    private void UnsubscribeFromTaskList(
        CheckupTaskList taskList)
    {
        taskList.PersistenceRequested -=
            CurrentTaskList_OnPersistenceRequested;
    }

    private void CurrentTaskList_OnPersistenceRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsCurrentCheckupSaved)
        {
            return;
        }

        PersistCurrentTaskList();
    }

    private void PersistCurrentTaskList()
    {
        if (SelectedCustomer is null
            || !_savedDeviceId.HasValue)
        {
            return;
        }

        var device =
            SelectedCustomer.Devices
                .FirstOrDefault(
                    existingDevice =>
                        existingDevice.Id
                        == _savedDeviceId.Value);

        if (device is null)
        {
            var message =
                "Das gespeicherte Gerät wurde in der "
                + "aktuellen Kundenliste nicht mehr gefunden.";

            ShowTaskPersistenceError(
                message);

            throw new InvalidOperationException(
                message);
        }

        var previousUpdatedAt =
            device.UpdatedAt;

        device.CheckupSession =
            CurrentCheckup;

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
                throw new InvalidOperationException(
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");
            }
        }
        catch (Exception exception)
        {
            device.UpdatedAt =
                previousUpdatedAt;

            ShowTaskPersistenceError(
                exception.Message);

            throw new InvalidOperationException(
                "Der Aufgabenstatus konnte nicht "
                + "dauerhaft gespeichert werden.",
                exception);
        }
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
        string errorDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                errorDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : errorDetails;

        _dialogService.ShowError(
            "Speichern fehlgeschlagen",
            "Der Systemcheck konnte nicht dauerhaft "
            + "gespeichert werden. Die angezeigten "
            + "Scandaten bleiben erhalten und können "
            + "erneut gespeichert werden."
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
            + "Die bisherigen Checkup-Daten bleiben unverändert."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {errorDetails}";
    }
}