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

    private readonly WindowsTemporaryFilesCleanupExecutor
        _windowsTemporaryFilesCleanupExecutor;

    public ControlledCleanupActionExecutor(
        CleanupActionExecutor standardCleanupExecutor,
        BrowserCacheCleanupExecutor browserCacheCleanupExecutor,
        WindowsTemporaryFilesCleanupExecutor
            windowsTemporaryFilesCleanupExecutor)
    {
        ArgumentNullException.ThrowIfNull(
            standardCleanupExecutor);

        ArgumentNullException.ThrowIfNull(
            browserCacheCleanupExecutor);

        ArgumentNullException.ThrowIfNull(
            windowsTemporaryFilesCleanupExecutor);

        _standardCleanupExecutor =
            standardCleanupExecutor;

        _browserCacheCleanupExecutor =
            browserCacheCleanupExecutor;

        _windowsTemporaryFilesCleanupExecutor =
            windowsTemporaryFilesCleanupExecutor;
    }

    public Task<CleanupActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        if (plan.CleanupCategories.Count == 1)
        {
            var selectedCategory =
                plan.CleanupCategories[0].Category;

            if (selectedCategory
                == CleanupCategoryType.BrowserCache)
            {
                return _browserCacheCleanupExecutor
                    .ExecuteAsync(
                        plan,
                        cancellationToken);
            }

            if (selectedCategory
                == CleanupCategoryType.WindowsTemporaryFiles)
            {
                return _windowsTemporaryFilesCleanupExecutor
                    .ExecuteAsync(
                        plan,
                        cancellationToken);
            }
        }

        return _standardCleanupExecutor
            .ExecuteAsync(
                plan,
                cancellationToken);
    }
}