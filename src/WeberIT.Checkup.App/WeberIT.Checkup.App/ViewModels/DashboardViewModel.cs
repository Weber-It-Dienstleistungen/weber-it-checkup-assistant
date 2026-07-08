namespace WeberIT.Checkup.App.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    public string WelcomeText =>
        "Willkommen im Weber IT Checkup Assistent.";

    public string DescriptionText =>
        "Das Dashboard dient als zentrale Übersicht. Der Gerätescan wird im Bereich Gerät / Checkup gestartet.";
}