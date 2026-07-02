using System.Windows.Input;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly CustomersViewModel _customersViewModel;

    public string ApplicationTitle { get; } = "Weber IT Checkup Assistent";

    public string ApplicationSubtitle { get; } =
        "Professioneller Windows-Checkup für Kunden, Geräte und Dokumentation.";

    public BaseViewModel? CurrentViewModel => _navigationService.CurrentViewModel;

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowCustomersCommand { get; }

    public MainViewModel(
        INavigationService navigationService,
        DashboardViewModel dashboardViewModel,
        CustomersViewModel customersViewModel)
    {
        _navigationService = navigationService;
        _dashboardViewModel = dashboardViewModel;
        _customersViewModel = customersViewModel;

        ShowDashboardCommand = new RelayCommand(_ => _navigationService.NavigateTo(_dashboardViewModel));
        ShowCustomersCommand = new RelayCommand(_ => _navigationService.NavigateTo(_customersViewModel));

        _navigationService.CurrentViewChanged += OnCurrentViewChanged;
        _navigationService.NavigateTo(_dashboardViewModel);
    }

    private void OnCurrentViewChanged()
    {
        OnPropertyChanged(nameof(CurrentViewModel));
    }
}