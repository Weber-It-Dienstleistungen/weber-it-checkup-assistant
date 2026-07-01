using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Services;

public class NavigationService
{
    public BaseViewModel? CurrentViewModel { get; private set; }

    public event Action? CurrentViewChanged;

    public void NavigateTo(BaseViewModel viewModel)
    {
        CurrentViewModel = viewModel;
        CurrentViewChanged?.Invoke();
    }
}