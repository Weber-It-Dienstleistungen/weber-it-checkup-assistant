using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IProgramUpdateActionPlanBuilder
{
    CheckupTaskActionPlan Build(
        CheckupTask task,
        ProgramUpdateInformation programUpdateInformation,
        IReadOnlyCollection<ProgramUpdateActionSelection>
            selections);
}