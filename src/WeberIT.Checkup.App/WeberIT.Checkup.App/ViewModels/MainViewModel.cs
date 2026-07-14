using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly CustomersViewModel _customersViewModel;
    private readonly CheckupViewModel _checkupViewModel;
    private readonly MaintenanceViewModel _maintenanceViewModel;

    public string ApplicationTitle { get; } =
        "Weber IT Checkup Assistent";

    public string ApplicationSubtitle { get; } =
        "Professioneller Windows-Checkup für Kunden, Geräte und Dokumentation.";

    public BaseViewModel? CurrentViewModel =>
        _navigationService.CurrentViewModel;

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowCustomersCommand { get; }

    public ICommand ShowCheckupCommand { get; }

    public ICommand ShowMaintenanceCommand { get; }

    public MainViewModel(
        INavigationService navigationService,
        DashboardViewModel dashboardViewModel,
        CustomersViewModel customersViewModel,
        CheckupViewModel checkupViewModel,
        MaintenanceViewModel maintenanceViewModel)
    {
        _navigationService = navigationService;
        _dashboardViewModel = dashboardViewModel;
        _customersViewModel = customersViewModel;
        _checkupViewModel = checkupViewModel;
        _maintenanceViewModel = maintenanceViewModel;

        ShowDashboardCommand =
            new RelayCommand(
                _ => ShowDashboard());

        ShowCustomersCommand =
            new RelayCommand(
                _ => ShowCustomers());

        ShowCheckupCommand =
            new RelayCommand(
                _ => ShowCheckup());

        ShowMaintenanceCommand =
            new RelayCommand(
                _ => ShowMaintenance());

        _navigationService.CurrentViewChanged +=
            OnCurrentViewChanged;

        ShowDashboard();
    }

    private void ShowDashboard()
    {
        _dashboardViewModel.Refresh();

        _navigationService.NavigateTo(
            _dashboardViewModel);
    }

    private void ShowCustomers()
    {
        _navigationService.NavigateTo(
            _customersViewModel);
    }

    private void ShowCheckup()
    {
        _checkupViewModel.SetCustomer(
            _customersViewModel.SelectedCustomer);

        _navigationService.NavigateTo(
            _checkupViewModel);
    }

    private void ShowMaintenance()
    {
        _navigationService.NavigateTo(
            _maintenanceViewModel);
    }

    private void OnCurrentViewChanged()
    {
        OnPropertyChanged(
            nameof(CurrentViewModel));
    }
}