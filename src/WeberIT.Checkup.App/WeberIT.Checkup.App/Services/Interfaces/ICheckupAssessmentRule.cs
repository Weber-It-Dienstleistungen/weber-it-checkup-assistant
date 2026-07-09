using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICheckupAssessmentRule
{
    IEnumerable<CheckupFinding> Evaluate(CheckupSession checkupSession);
}