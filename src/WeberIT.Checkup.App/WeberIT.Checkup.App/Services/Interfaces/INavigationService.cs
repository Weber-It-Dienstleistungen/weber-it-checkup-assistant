using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface INavigationService
{
    BaseViewModel? CurrentViewModel { get; }

    event Action? CurrentViewChanged;

    void NavigateTo(BaseViewModel viewModel);
}