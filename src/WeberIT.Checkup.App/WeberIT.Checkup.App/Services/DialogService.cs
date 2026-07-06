using System.Windows;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Services;

public class DialogService : IDialogService
{
    private Window? _currentDialog;

    public bool? ShowCustomerEditDialog(CustomerEditViewModel viewModel)
    {
        var dialog = new CustomerEditDialog
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow
        };

        _currentDialog = dialog;

        var result = dialog.ShowDialog();

        _currentDialog = null;

        return result;
    }

    public void CloseDialog(bool? dialogResult)
    {
        if (_currentDialog is null)
        {
            return;
        }

        _currentDialog.DialogResult = dialogResult;
        _currentDialog.Close();
    }
}