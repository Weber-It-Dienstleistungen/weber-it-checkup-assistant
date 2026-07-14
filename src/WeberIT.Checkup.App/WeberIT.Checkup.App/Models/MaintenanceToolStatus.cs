namespace WeberIT.Checkup.App.Models;

public enum MaintenanceToolStatus
{
    NotRun,
    Running,
    Successful,
    SuccessfulWithRepairs,
    ActionRequired,
    RestartRequired,
    Skipped,
    Failed
}