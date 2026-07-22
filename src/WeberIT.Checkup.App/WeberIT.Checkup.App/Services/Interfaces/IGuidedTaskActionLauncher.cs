namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IGuidedTaskActionLauncher
{
    bool CanLaunch(
        string actionCode);

    string GetTargetDescription(
        string actionCode);

    void Launch(
        string actionCode);
}