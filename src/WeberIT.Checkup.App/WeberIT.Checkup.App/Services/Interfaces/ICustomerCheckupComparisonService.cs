using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface ICustomerCheckupComparisonService
{
    CustomerCheckupComparison Compare(
        CustomerCheckupVisit customerCheckupVisit,
        CheckupSession workingCheckup,
        CheckupSession afterCheckup);
}