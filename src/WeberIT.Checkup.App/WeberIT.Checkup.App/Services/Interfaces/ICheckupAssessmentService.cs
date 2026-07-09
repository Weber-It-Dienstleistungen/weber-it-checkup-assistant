using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICheckupAssessmentService
{
    CheckupAssessment Assess(CheckupSession checkupSession);
}