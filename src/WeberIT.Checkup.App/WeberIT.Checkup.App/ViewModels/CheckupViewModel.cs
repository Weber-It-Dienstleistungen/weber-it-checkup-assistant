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
            _currentCheckup = value;

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
        && _savedCustomerId == SelectedCustomer.Id;

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
        SelectedCustomer = customer;
    }

    private void ReadSystem()
    {
        var checkupSession = _checkupScanner.Scan();

        checkupSession.Assessment =
            _checkupAssessmentService.Assess(checkupSession);

        _savedCustomerId = null;
        _lastSaveUpdatedExistingDevice = false;

        CurrentCheckup = checkupSession;
    }

    private void SaveCheckup()
    {
        if (!CanSaveCheckup || SelectedCustomer is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                CurrentCheckup.DeviceInformation);

        if (matchingDevice is not null)
        {
            UpdateExistingDevice(matchingDevice);
            return;
        }

        AddNewDevice();
    }

    private void UpdateExistingDevice(CustomerDevice matchingDevice)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Gerät bereits vorhanden",
            $"Das Gerät \"{matchingDevice.DisplayName}\" ist diesem Kunden bereits zugeordnet. "
            + "Soll der vorhandene Systemcheck durch den neuen Scan ersetzt werden?");

        if (!confirmed)
        {
            return;
        }

        var scannedComputerName =
            CurrentCheckup.DeviceInformation.Name;

        if (!string.IsNullOrWhiteSpace(scannedComputerName))
        {
            matchingDevice.DisplayName = scannedComputerName;
        }

        matchingDevice.CheckupSession = CurrentCheckup;
        matchingDevice.UpdatedAt = DateTime.Now;

        _customerService.UpdateCustomerDevice(
            SelectedCustomer.Id,
            matchingDevice);

        CompleteSave(true);
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

        var device = new CustomerDevice
        {
            DisplayName = displayName,
            CheckupSession = CurrentCheckup
        };

        _customerService.AddDeviceToCustomer(
            SelectedCustomer.Id,
            device);

        SelectedCustomer.Devices.Add(device);

        CompleteSave(false);
    }

    private void CompleteSave(bool updatedExistingDevice)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        _savedCustomerId = SelectedCustomer.Id;
        _lastSaveUpdatedExistingDevice = updatedExistingDevice;

        OnPropertyChanged(nameof(IsCurrentCheckupSaved));
        OnPropertyChanged(nameof(CanSaveCheckup));
        OnPropertyChanged(nameof(PersistenceStatusText));

        _saveCheckupCommand.RaiseCanExecuteChanged();
    }
}