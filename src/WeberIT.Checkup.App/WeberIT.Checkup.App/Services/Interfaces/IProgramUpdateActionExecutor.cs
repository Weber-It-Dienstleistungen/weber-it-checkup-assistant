using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IProgramUpdateActionExecutor
{
    Task<ProgramUpdateActionExecutionResult> ExecuteAsync(
        CheckupTaskActionPlan plan);
}