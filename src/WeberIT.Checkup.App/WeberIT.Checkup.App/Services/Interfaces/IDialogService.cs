using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IDialogService
{
    bool? ShowCustomerEditDialog(CustomerEditViewModel viewModel);

    void CloseDialog(bool? dialogResult);
}