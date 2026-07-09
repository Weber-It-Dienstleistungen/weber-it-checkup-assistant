using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class CheckupAssessmentService : ICheckupAssessmentService
{
    private readonly IEnumerable<ICheckupAssessmentRule> _assessmentRules;

    public CheckupAssessmentService(IEnumerable<ICheckupAssessmentRule> assessmentRules)
    {
        _assessmentRules = assessmentRules;
    }

    public CheckupAssessment Assess(CheckupSession checkupSession)
    {
        var assessment = new CheckupAssessment();

        foreach (var assessmentRule in _assessmentRules)
        {
            assessment.Findings.AddRange(assessmentRule.Evaluate(checkupSession));
        }

        return assessment;
    }
}