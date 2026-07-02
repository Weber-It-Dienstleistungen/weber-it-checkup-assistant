using System.Windows;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Services;

public class DialogService : IDialogService
{
    public bool? ShowCustomerEditDialog(CustomerEditViewModel viewModel)
    {
        var dialog = new CustomerEditDialog
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog();
    }
}