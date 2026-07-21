using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICleanupActionPlanBuilder
{
    IReadOnlyList<CleanupActionCategory>
        GetSelectableCategories(
            CleanupPotentialInformation cleanupInformation);

    CheckupTaskActionPlan Build(
        CheckupTask task,
        CleanupPotentialInformation cleanupInformation,
        IReadOnlyCollection<CleanupActionSelection>
            selections);
}