using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IDialogService
{
    bool? ShowCustomerEditDialog(Customer customer, bool isNewCustomer);

    void CloseDialog(bool? dialogResult);
}