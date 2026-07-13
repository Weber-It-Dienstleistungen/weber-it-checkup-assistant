using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerDevicesViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;
    private readonly IDialogService _dialogService;

    private Customer? _selectedCustomer;
    private CustomerDevice? _selectedDevice;

    public CustomerDevicesViewModel(
        ICustomerService customerService,
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _checkupScanner = checkupScanner;
        _checkupAssessmentService = checkupAssessmentService;
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

        var checkupSession = CreateCheckupSession();

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

        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(DeviceCountText));
    }

    private void RescanDevice()
    {
        if (SelectedCustomer is null || SelectedDevice is null)
        {
            return;
        }

        var checkupSession = CreateCheckupSession();

        if (!string.IsNullOrWhiteSpace(
                checkupSession.DeviceInformation.Name))
        {
            SelectedDevice.DisplayName =
                checkupSession.DeviceInformation.Name;
        }

        SelectedDevice.CheckupSession = checkupSession;
        SelectedDevice.UpdatedAt = DateTime.Now;

        _customerService.UpdateCustomerDevice(
            SelectedCustomer.Id,
            SelectedDevice);

        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(SelectedDevice));
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

        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(DeviceCountText));
    }

    private CheckupSession CreateCheckupSession()
    {
        var checkupSession = _checkupScanner.Scan();

        checkupSession.Assessment =
            _checkupAssessmentService.Assess(checkupSession);

        return checkupSession;
    }
}