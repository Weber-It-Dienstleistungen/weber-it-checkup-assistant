using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICheckupTaskActionExecutionCoordinator
{
    event EventHandler? StateChanged;

    bool IsExecutionRunning { get; }

    string ActiveActionCode { get; }

    string ActiveActionTitle { get; }

    CheckupTaskActionExecutionLease? TryBeginExecution(
        string actionCode,
        string actionTitle);
}