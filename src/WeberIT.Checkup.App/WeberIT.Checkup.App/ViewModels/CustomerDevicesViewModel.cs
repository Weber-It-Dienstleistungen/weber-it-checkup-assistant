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

    public CustomerDevicesViewModel(
        ICustomerService customerService,
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        IDeviceIdentityService deviceIdentityService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _checkupScanner = checkupScanner;
        _checkupAssessmentService = checkupAssessmentService;
        _deviceIdentityService = deviceIdentityService;
        _dialogService = dialogService;

        AddDeviceCommand = new RelayCommand(
            _ => AddDevice(),
            _ => SelectedCustomer is not null);

        RescanDeviceCommand = new RelayCommand(
            _ => RescanDevice(),
            _ => SelectedCustomer is not null
                 && SelectedDevice is not null);

        DeleteDeviceCommand = new RelayCommand(
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

            _selectedCustomer = value;
            SelectedDevice = null;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));

            AddDeviceCommand.RaiseCanExecuteChanged();
            RescanDeviceCommand.RaiseCanExecuteChanged();
            DeleteDeviceCommand.RaiseCanExecuteChanged();
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

            _selectedDevice = value;

            OnPropertyChanged();

            RescanDeviceCommand.RaiseCanExecuteChanged();
            DeleteDeviceCommand.RaiseCanExecuteChanged();
        }
    }

    public IEnumerable<CustomerDevice> Devices =>
        SelectedCustomer?.Devices.ToList()
        ?? Enumerable.Empty<CustomerDevice>();

    public string DeviceCountText
    {
        get
        {
            var count = SelectedCustomer?.Devices.Count ?? 0;

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

        var checkupSession = TryCreateCheckupSession();

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

        AddNewDevice(checkupSession);
    }

    private void UpdateMatchingDevice(
        CustomerDevice matchingDevice,
        CheckupSession checkupSession)
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

        ApplyCheckupToDevice(
            matchingDevice,
            checkupSession);
    }

    private void AddNewDevice(CheckupSession checkupSession)
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

        var device = new CustomerDevice
        {
            DisplayName = displayName,
            CheckupSession = checkupSession
        };

        _customerService.AddDeviceToCustomer(
            SelectedCustomer.Id,
            device);

        SelectedCustomer.Devices.Add(device);
        SelectedDevice = device;

        RefreshDeviceDisplay();
    }

    private void RescanDevice()
    {
        if (SelectedCustomer is null || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice = SelectedDevice;
        var checkupSession = TryCreateCheckupSession();

        if (checkupSession is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                checkupSession.DeviceInformation);

        if (matchingDevice is not null
            && matchingDevice.Id != selectedDevice.Id)
        {
            var confirmed = _dialogService.Confirm(
                "Anderes Gerät erkannt",
                $"Der neue Scan gehört nicht zum ausgewählten Gerät "
                + $"\"{selectedDevice.DisplayName}\", sondern zum bereits gespeicherten Gerät "
                + $"\"{matchingDevice.DisplayName}\". "
                + "Soll stattdessen dieses erkannte Gerät aktualisiert werden?");

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
            var confirmed = _dialogService.Confirm(
                "Gerät nicht eindeutig erkannt",
                $"Der neue Scan konnte dem ausgewählten Gerät "
                + $"\"{selectedDevice.DisplayName}\" nicht eindeutig zugeordnet werden. "
                + "Soll der gespeicherte Systemcheck trotzdem durch die neuen Daten ersetzt werden?");

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

        var scannedComputerName =
            checkupSession.DeviceInformation.Name;

        if (!string.IsNullOrWhiteSpace(scannedComputerName))
        {
            device.DisplayName = scannedComputerName;
        }

        device.CheckupSession = checkupSession;
        device.UpdatedAt = DateTime.Now;

        _customerService.UpdateCustomerDevice(
            SelectedCustomer.Id,
            device);

        SelectedDevice = device;

        RefreshDeviceDisplay();
    }

    private void DeleteDevice()
    {
        if (SelectedCustomer is null || SelectedDevice is null)
        {
            return;
        }

        var device = SelectedDevice;

        var confirmed = _dialogService.Confirm(
            "Gerät löschen",
            $"Soll das Gerät \"{device.DisplayName}\" wirklich gelöscht werden?");

        if (!confirmed)
        {
            return;
        }

        _customerService.DeleteCustomerDevice(
            SelectedCustomer.Id,
            device.Id);

        SelectedCustomer.Devices.Remove(device);
        SelectedDevice = null;

        RefreshDeviceDisplay();
    }

    private CheckupSession? TryCreateCheckupSession()
    {
        try
        {
            var checkupSession = _checkupScanner.Scan();

            checkupSession.Assessment =
                _checkupAssessmentService.Assess(checkupSession);

            return checkupSession;
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Systemscan fehlgeschlagen",
                BuildScanErrorMessage(exception));

            return null;
        }
    }

    private void RefreshDeviceDisplay()
    {
        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(SelectedDevice));
    }

    private static string BuildScanErrorMessage(Exception exception)
    {
        var errorDetails = string.IsNullOrWhiteSpace(exception.Message)
            ? "Keine weiteren Fehlerdetails verfügbar."
            : exception.Message;

        return "Die Systeminformationen konnten nicht vollständig ausgelesen oder bewertet werden. "
               + "Es wurde kein Gerät angelegt und kein vorhandener Systemcheck überschrieben."
               + Environment.NewLine
               + Environment.NewLine
               + $"Technische Details: {errorDetails}";
    }
}