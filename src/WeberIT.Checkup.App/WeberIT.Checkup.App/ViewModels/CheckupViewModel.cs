using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;

namespace WeberIT.Checkup.App.ViewModels;

public class CheckupViewModel : BaseViewModel
{
    public string Title => "Gerät / Checkup";

    public string Subtitle =>
        "Systeminformationen auslesen und für den späteren Checkup vorbereiten.";

    public ICommand ReadSystemCommand { get; }

    public CheckupViewModel()
    {
        ReadSystemCommand = new RelayCommand(_ => ReadSystem());
    }

    private void ReadSystem()
    {
        // Scanner-Anbindung folgt im nächsten Inkrement.
    }
}