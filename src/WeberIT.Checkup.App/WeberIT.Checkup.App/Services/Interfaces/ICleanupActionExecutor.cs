using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICleanupActionExecutor
{
    Task<CleanupActionExecutionResult> ExecuteAsync(
        CheckupTaskActionPlan plan,
        CancellationToken cancellationToken = default);
}