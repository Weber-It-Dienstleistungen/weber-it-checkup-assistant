using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class CheckupAssessmentService :
    ICheckupAssessmentService
{
    private const int CurrentScoringVersion =
        1;

    private readonly IEnumerable<ICheckupAssessmentRule>
        _assessmentRules;

    private readonly ISystemConditionAssessmentService
        _systemConditionAssessmentService;

    private readonly IHardwareConditionAssessmentService
        _hardwareConditionAssessmentService;

    public CheckupAssessmentService(
        IEnumerable<ICheckupAssessmentRule> assessmentRules,
        ISystemConditionAssessmentService
            systemConditionAssessmentService,
        IHardwareConditionAssessmentService
            hardwareConditionAssessmentService)
    {
        _assessmentRules =
            assessmentRules;

        _systemConditionAssessmentService =
            systemConditionAssessmentService;

        _hardwareConditionAssessmentService =
            hardwareConditionAssessmentService;
    }

    public CheckupAssessment Assess(
        CheckupSession checkupSession)
    {
        var assessment =
            new CheckupAssessment
            {
                ScoringVersion =
                    CurrentScoringVersion,

                AssessmentCreatedAt =
                    DateTime.Now
            };

        foreach (var assessmentRule in _assessmentRules)
        {
            assessment.Findings.AddRange(
                assessmentRule.Evaluate(
                    checkupSession));
        }

        assessment.SystemCondition =
            _systemConditionAssessmentService.Assess(
                checkupSession,
                assessment.Findings);

        var hardwareAssessment =
            _hardwareConditionAssessmentService.Assess(
                checkupSession,
                assessment.Findings);

        assessment.HardwareCondition =
            hardwareAssessment.Condition;

        assessment.HardwarePlanningHorizon =
            hardwareAssessment.PlanningHorizon;

        assessment.HardwarePlanningSummary =
            hardwareAssessment.PlanningSummary;

        return assessment;
    }
}