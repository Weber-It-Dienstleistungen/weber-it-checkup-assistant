using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IMaintenanceProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        bool requiresAdministrator);
}