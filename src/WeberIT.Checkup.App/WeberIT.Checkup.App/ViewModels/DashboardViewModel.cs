using System.Collections.ObjectModel;
using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly INavigationService _navigationService;
    private readonly CustomersViewModel _customersViewModel;
    private readonly CheckupViewModel _checkupViewModel;

    private int _customerCount;
    private int _deviceCount;
    private int _checkupCount;
    private int _recommendationCount;
    private int _unscannedDeviceCount;
    private int _devicesWithOpenFindingsCount;
    private int _legacyCheckupCount;

    public DashboardViewModel(
        ICustomerService customerService,
        INavigationService navigationService,
        CustomersViewModel customersViewModel,
        CheckupViewModel checkupViewModel)
    {
        _customerService =
            customerService;

        _navigationService =
            navigationService;

        _customersViewModel =
            customersViewModel;

        _checkupViewModel =
            checkupViewModel;

        StartCheckupCommand =
            new RelayCommand(
                _ => StartCheckup());

        ShowCustomersCommand =
            new RelayCommand(
                _ => ShowCustomers());

        Refresh();
    }

    public string WelcomeText =>
        "Hier sehen Sie den aktuellen Bestand Ihrer Kunden, "
        + "Geräte und gespeicherten Systemchecks.";

    public int CustomerCount
    {
        get => _customerCount;

        private set
        {
            if (_customerCount == value)
            {
                return;
            }

            _customerCount =
                value;

            OnPropertyChanged();
        }
    }

    public int DeviceCount
    {
        get => _deviceCount;

        private set
        {
            if (_deviceCount == value)
            {
                return;
            }

            _deviceCount =
                value;

            OnPropertyChanged();
        }
    }

    public int CheckupCount
    {
        get => _checkupCount;

        private set
        {
            if (_checkupCount == value)
            {
                return;
            }

            _checkupCount =
                value;

            OnPropertyChanged();
        }
    }

    public int RecommendationCount
    {
        get => _recommendationCount;

        private set
        {
            if (_recommendationCount == value)
            {
                return;
            }

            _recommendationCount =
                value;

            OnPropertyChanged();
        }
    }

    public int UnscannedDeviceCount
    {
        get => _unscannedDeviceCount;

        private set
        {
            if (_unscannedDeviceCount == value)
            {
                return;
            }

            _unscannedDeviceCount =
                value;

            OnPropertyChanged();
        }
    }

    public int DevicesWithOpenFindingsCount
    {
        get => _devicesWithOpenFindingsCount;

        private set
        {
            if (_devicesWithOpenFindingsCount == value)
            {
                return;
            }

            _devicesWithOpenFindingsCount =
                value;

            OnPropertyChanged();
        }
    }

    public int LegacyCheckupCount
    {
        get => _legacyCheckupCount;

        private set
        {
            if (_legacyCheckupCount == value)
            {
                return;
            }

            _legacyCheckupCount =
                value;

            OnPropertyChanged();
        }
    }

    public string ScannedDevicesText =>
        CheckupCount == 1
            ? "1 Gerät besitzt einen gespeicherten Checkup."
            : $"{CheckupCount} Geräte besitzen einen gespeicherten Checkup.";

    public string UnscannedDevicesText =>
        UnscannedDeviceCount == 1
            ? "1 Gerät besitzt noch keinen gespeicherten Checkup."
            : $"{UnscannedDeviceCount} Geräte besitzen noch keinen gespeicherten Checkup.";

    public string DevicesWithOpenFindingsText =>
        DevicesWithOpenFindingsCount == 1
            ? "1 Gerät enthält mindestens einen offenen Befund."
            : $"{DevicesWithOpenFindingsCount} Geräte enthalten mindestens einen offenen Befund.";

    public string LegacyCheckupsText =>
        LegacyCheckupCount == 1
            ? "1 gespeicherter Checkup verwendet noch keine getrennte Zustandsbewertung."
            : $"{LegacyCheckupCount} gespeicherte Checkups verwenden noch keine getrennte Zustandsbewertung.";

    public ObservableCollection<DashboardActivityItem>
        RecentActivities
    { get; } =
            new();

    public ObservableCollection<DashboardRecommendationItem>
        PriorityRecommendations
    { get; } =
            new();

    public bool HasRecentActivities =>
        RecentActivities.Count > 0;

    public bool HasPriorityRecommendations =>
        PriorityRecommendations.Count > 0;

    public ICommand StartCheckupCommand { get; }

    public ICommand ShowCustomersCommand { get; }

    public void Refresh()
    {
        var customers =
            _customerService
                .GetCustomers()
                .OrderBy(
                    customer =>
                        customer.DisplayName)
                .ToList();

        var devices =
            customers
                .SelectMany(
                    customer =>
                        customer.Devices.Select(
                            device =>
                                new DashboardDeviceContext
                                {
                                    Customer =
                                        customer,

                                    Device =
                                        device
                                }))
                .ToList();

        var scannedDevices =
            devices
                .Where(
                    item =>
                        item.Device
                            .CheckupSession
                            .ScanDate
                            .HasValue)
                .ToList();

        var findings =
            scannedDevices
                .SelectMany(
                    item =>
                        item.Device
                            .CheckupSession
                            .Assessment
                            .Findings)
                .ToList();

        CustomerCount =
            customers.Count;

        DeviceCount =
            devices.Count;

        CheckupCount =
            scannedDevices.Count;

        UnscannedDeviceCount =
            Math.Max(
                0,
                DeviceCount - CheckupCount);

        RecommendationCount =
            findings.Count(
                finding =>
                    finding.Severity
                        is FindingSeverity.Recommendation
                        or FindingSeverity.Warning
                        or FindingSeverity.Critical);

        DevicesWithOpenFindingsCount =
            scannedDevices.Count(
                item =>
                    item.Device
                        .CheckupSession
                        .Assessment
                        .Findings
                        .Any(
                            finding =>
                                finding.Severity
                                    is FindingSeverity.Recommendation
                                    or FindingSeverity.Warning
                                    or FindingSeverity.Critical));

        LegacyCheckupCount =
            scannedDevices.Count(
                item =>
                    item.Device
                        .CheckupSession
                        .Assessment
                        .ScoringVersion <= 0);

        OnPropertyChanged(
            nameof(ScannedDevicesText));

        OnPropertyChanged(
            nameof(UnscannedDevicesText));

        OnPropertyChanged(
            nameof(DevicesWithOpenFindingsText));

        OnPropertyChanged(
            nameof(LegacyCheckupsText));

        BuildRecentActivities(
            customers,
            devices);

        BuildPriorityRecommendations(
            devices);
    }

    private void StartCheckup()
    {
        _checkupViewModel.SetCustomer(
            _customersViewModel.SelectedCustomer);

        _navigationService.NavigateTo(
            _checkupViewModel);
    }

    private void ShowCustomers()
    {
        _navigationService.NavigateTo(
            _customersViewModel);
    }

    private void BuildRecentActivities(
        IReadOnlyCollection<Customer> customers,
        IReadOnlyCollection<DashboardDeviceContext> devices)
    {
        RecentActivities.Clear();

        var activities =
            new List<DashboardActivityItem>();

        foreach (var customer in customers)
        {
            activities.Add(
                new DashboardActivityItem
                {
                    Title =
                        "Kunde angelegt",

                    Description =
                        customer.DisplayName,

                    Timestamp =
                        customer.CreatedAt,

                    ActivityType =
                        DashboardActivityType.Customer
                });

            if (customer.UpdatedAt.HasValue)
            {
                activities.Add(
                    new DashboardActivityItem
                    {
                        Title =
                            "Kundendaten aktualisiert",

                        Description =
                            customer.DisplayName,

                        Timestamp =
                            customer.UpdatedAt.Value,

                        ActivityType =
                            DashboardActivityType.Customer
                    });
            }
        }

        foreach (var item in devices)
        {
            var customer =
                item.Customer;

            var device =
                item.Device;

            if (device.CheckupSession.ScanDate.HasValue)
            {
                activities.Add(
                    new DashboardActivityItem
                    {
                        Title =
                            "Systemcheck durchgeführt",

                        Description =
                            $"{device.DisplayName} · "
                            + $"{customer.DisplayName}",

                        Timestamp =
                            device.CheckupSession.ScanDate.Value,

                        ActivityType =
                            DashboardActivityType.Checkup
                    });
            }
            else
            {
                activities.Add(
                    new DashboardActivityItem
                    {
                        Title =
                            "Gerät angelegt",

                        Description =
                            $"{device.DisplayName} · "
                            + $"{customer.DisplayName}",

                        Timestamp =
                            device.CreatedAt,

                        ActivityType =
                            DashboardActivityType.Device
                    });
            }
        }

        foreach (var activity in activities
                     .OrderByDescending(
                         activity =>
                             activity.Timestamp)
                     .Take(6))
        {
            RecentActivities.Add(
                activity);
        }

        OnPropertyChanged(
            nameof(HasRecentActivities));
    }

    private void BuildPriorityRecommendations(
        IReadOnlyCollection<DashboardDeviceContext> devices)
    {
        PriorityRecommendations.Clear();

        var recommendations =
            devices
                .SelectMany(
                    item =>
                        item.Device
                            .CheckupSession
                            .Assessment
                            .Findings
                            .Where(
                                finding =>
                                    finding.Severity
                                        is FindingSeverity.Recommendation
                                        or FindingSeverity.Warning
                                        or FindingSeverity.Critical)
                            .Select(
                                finding =>
                                    new DashboardRecommendationItem
                                    {
                                        Title =
                                            finding.Title,

                                        Description =
                                            finding.Description,

                                        DeviceName =
                                            item.Device.DisplayName,

                                        CustomerName =
                                            item.Customer.DisplayName,

                                        Severity =
                                            finding.Severity
                                    }))
                .OrderByDescending(
                    item =>
                        GetSeverityPriority(
                            item.Severity))
                .ThenBy(
                    item =>
                        item.CustomerName)
                .ThenBy(
                    item =>
                        item.DeviceName)
                .Take(5)
                .ToList();

        foreach (var recommendation in recommendations)
        {
            PriorityRecommendations.Add(
                recommendation);
        }

        OnPropertyChanged(
            nameof(HasPriorityRecommendations));
    }

    private static int GetSeverityPriority(
        FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => 4,
            FindingSeverity.Warning => 3,
            FindingSeverity.Recommendation => 2,
            FindingSeverity.Information => 1,
            _ => 0
        };
    }
}

public class DashboardDeviceContext
{
    public required Customer Customer { get; init; }

    public required CustomerDevice Device { get; init; }
}

public class DashboardActivityItem
{
    public string Title { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public DateTime Timestamp { get; set; }

    public DashboardActivityType ActivityType { get; set; }

    public string TimestampText =>
        FormatTimestamp(
            Timestamp);

    private static string FormatTimestamp(
        DateTime timestamp)
    {
        var difference =
            DateTime.Now - timestamp;

        if (difference.TotalMinutes < 1)
        {
            return "gerade eben";
        }

        if (difference.TotalMinutes < 60)
        {
            return $"vor {(int)difference.TotalMinutes} Min.";
        }

        if (difference.TotalHours < 24)
        {
            return $"vor {(int)difference.TotalHours} Std.";
        }

        if (difference.TotalDays < 7)
        {
            return $"vor {(int)difference.TotalDays} Tagen";
        }

        return timestamp.ToString(
            "dd.MM.yyyy");
    }
}

public class DashboardRecommendationItem
{
    public string Title { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string DeviceName { get; set; } =
        string.Empty;

    public string CustomerName { get; set; } =
        string.Empty;

    public FindingSeverity Severity { get; set; }

    public string ContextText =>
        $"{CustomerName} · {DeviceName}";

    public string SeverityText =>
        Severity switch
        {
            FindingSeverity.Critical =>
                "Kritisch",

            FindingSeverity.Warning =>
                "Warnung",

            FindingSeverity.Recommendation =>
                "Empfehlung",

            FindingSeverity.Information =>
                "Information",

            _ =>
                "Hinweis"
        };
}

public enum DashboardActivityType
{
    Customer,
    Device,
    Checkup
}