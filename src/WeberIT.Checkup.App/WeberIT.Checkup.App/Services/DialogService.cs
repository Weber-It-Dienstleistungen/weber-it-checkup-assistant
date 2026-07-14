using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Services;

public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private Window? _currentDialog;

    public DialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool? ShowCustomerEditDialog(
        Customer customer,
        bool isNewCustomer)
    {
        var viewModel =
            _serviceProvider.GetRequiredService<CustomerEditViewModel>();

        viewModel.Initialize(
            customer,
            isNewCustomer);

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

    public bool Confirm(
        string title,
        string message)
    {
        var dialog = new ConfirmationDialog(
            title,
            message)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true;
    }

    public void ShowError(
        string title,
        string message)
    {
        var dialog = new MessageDialog(
            title,
            message)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();
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