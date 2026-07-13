using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerDevicesViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;

    private Customer? _selectedCustomer;
    private CustomerDevice? _selectedDevice;

    public CustomerDevicesViewModel(
        ICustomerService customerService,
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService)
    {
        _customerService = customerService;
        _checkupScanner = checkupScanner;
        _checkupAssessmentService = checkupAssessmentService;

        AddDeviceCommand = new RelayCommand(
            _ => AddDevice(),
            _ => SelectedCustomer is not null);
    }

    public RelayCommand AddDeviceCommand { get; }

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

        var checkupSession = _checkupScanner.Scan();

        checkupSession.Assessment =
            _checkupAssessmentService.Assess(checkupSession);

        var displayName =
            !string.IsNullOrWhiteSpace(checkupSession.DeviceInformation.Name)
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

        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(DeviceCountText));
    }
}