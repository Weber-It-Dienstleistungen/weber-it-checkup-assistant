using WeberIT.Checkup.App.Views.Pages;

namespace WeberIT.Checkup.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    public string ApplicationTitle { get; } = "Weber IT Checkup Assistent";

    public string ApplicationSubtitle { get; } =
        "Professioneller Windows-Checkup für Kunden, Geräte und Dokumentation.";

    private object? _currentView;

    public object? CurrentView
    {
        get => _currentView;
        set
        {
            _currentView = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel()
    {
        CurrentView = new DashboardView();
    }
}