using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class ControlledCleanupActionExecutor :
    ICleanupActionExecutor
{
    private readonly CleanupActionExecutor
        _standardCleanupExecutor;

    private readonly BrowserCacheCleanupExecutor
        _browserCacheCleanupExecutor;

    public ControlledCleanupActionExecutor(
        CleanupActionExecutor standardCleanupExecutor,
        BrowserCacheCleanupExecutor browserCacheCleanupExecutor)
    {
        ArgumentNullException.ThrowIfNull(
            standardCleanupExecutor);

        ArgumentNullException.ThrowIfNull(
            browserCacheCleanupExecutor);

        _standardCleanupExecutor =
            standardCleanupExecutor;

        _browserCacheCleanupExecutor =
            browserCacheCleanupExecutor;
    }

    public Task<CleanupActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        if (plan.CleanupCategories.Count == 1
            && plan.CleanupCategories[0].Category
                == CleanupCategoryType.BrowserCache)
        {
            return _browserCacheCleanupExecutor
                .ExecuteAsync(
                    plan,
                    cancellationToken);
        }

        return _standardCleanupExecutor
            .ExecuteAsync(
                plan,
                cancellationToken);
    }
}