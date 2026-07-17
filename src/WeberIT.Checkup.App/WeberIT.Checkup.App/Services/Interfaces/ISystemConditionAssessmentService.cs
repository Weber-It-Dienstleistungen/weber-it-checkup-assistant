using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ISystemConditionAssessmentService
{
    ConditionAssessment Assess(
        CheckupSession checkupSession,
        IReadOnlyCollection<CheckupFinding> findings);
}