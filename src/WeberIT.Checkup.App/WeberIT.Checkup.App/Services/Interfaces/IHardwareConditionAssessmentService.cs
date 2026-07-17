using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IHardwareConditionAssessmentService
{
    (
        ConditionAssessment Condition,
        HardwarePlanningHorizon PlanningHorizon,
        string PlanningSummary
    ) Assess(
        CheckupSession checkupSession,
        IReadOnlyCollection<CheckupFinding> findings);
}