using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IDialogService
{
    bool? ShowCustomerEditDialog(
        Customer customer,
        bool isNewCustomer);

    CustomerCheckupCompletionDraft?
        ShowCustomerCheckupCompletionDialog(
            string deviceDisplayName,
            CustomerCheckupVisit customerCheckupVisit,
            CustomerCheckupComparison comparison);

    bool Confirm(
        string title,
        string message);

    void ShowError(
        string title,
        string message);

    void CloseDialog(
        bool? dialogResult);
}