namespace WeberIT.Checkup.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    public string ApplicationTitle { get; } = "Weber IT Checkup Assistent";

    public string ApplicationSubtitle { get; } =
        "Professioneller Windows-Checkup für Kunden, Geräte und Dokumentation.";

    private object? _currentViewModel;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel(DashboardViewModel dashboardViewModel)
    {
        CurrentViewModel = dashboardViewModel;
    }
}