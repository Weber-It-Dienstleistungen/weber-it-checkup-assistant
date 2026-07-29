using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text;
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

    public DialogService(
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(
            serviceProvider);

        _serviceProvider =
            serviceProvider;
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

        var dialog =
            new CustomerEditDialog
            {
                DataContext =
                    viewModel
            };

        AssignOwnerIfAvailable(
            dialog);

        _currentDialog =
            dialog;

        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            if (ReferenceEquals(
                    _currentDialog,
                    dialog))
            {
                _currentDialog =
                    null;
            }
        }
    }

    public CustomerCheckupCompletionDraft?
        ShowCustomerCheckupCompletionDialog(
            string deviceDisplayName,
            CustomerCheckupVisit customerCheckupVisit,
            CustomerCheckupComparison comparison)
    {
        try
        {
            var dialog =
                new CustomerCheckupCompletionDialog(
                    deviceDisplayName,
                    customerCheckupVisit,
                    comparison);

            AssignOwnerIfAvailable(
                dialog);

            _currentDialog =
                dialog;

            try
            {
                var result =
                    dialog.ShowDialog();

                return result == true
                    ? dialog.CompletionDraft
                    : null;
            }
            finally
            {
                if (ReferenceEquals(
                        _currentDialog,
                        dialog))
                {
                    _currentDialog =
                        null;
                }
            }
        }
        catch (Exception exception)
        {
            _currentDialog =
                null;

            Debug.WriteLine(
                "Der Abschlussdialog konnte nicht geöffnet werden.");

            Debug.WriteLine(
                exception.ToString());

            ShowDialogCreationError(
                exception);

            return null;
        }
    }

    public bool Confirm(
        string title,
        string message)
    {
        var dialog =
            new ConfirmationDialog(
                title,
                message);

        AssignOwnerIfAvailable(
            dialog);

        return dialog.ShowDialog()
            == true;
    }

    public void ShowError(
        string title,
        string message)
    {
        var dialog =
            new MessageDialog(
                title,
                message);

        AssignOwnerIfAvailable(
            dialog);

        dialog.ShowDialog();
    }

    public void CloseDialog(
        bool? dialogResult)
    {
        if (_currentDialog is null)
        {
            return;
        }

        _currentDialog.DialogResult =
            dialogResult;

        _currentDialog.Close();
    }

    private static void ShowDialogCreationError(
        Exception exception)
    {
        var title =
            "Abschlussdialog konnte nicht geöffnet werden";

        var message =
            "Der Kontrollscan wurde ausgeführt, aber die "
            + "Vergleichsvorschau konnte nicht geöffnet werden."
            + Environment.NewLine
            + Environment.NewLine
            + "Der laufende Kundencheckup wurde nicht verändert."
            + Environment.NewLine
            + Environment.NewLine
            + "Technische Ursache:"
            + Environment.NewLine
            + BuildExceptionDetails(
                exception);

        try
        {
            var dialog =
                new MessageDialog(
                    title,
                    message);

            AssignOwnerIfAvailable(
                dialog);

            dialog.ShowDialog();
        }
        catch
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string BuildExceptionDetails(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        var builder =
            new StringBuilder();

        Exception? currentException =
            exception;

        var level =
            0;

        while (currentException is not null
               && level < 10)
        {
            if (level > 0)
            {
                builder.AppendLine();
                builder.AppendLine();

                builder.Append(
                    "Innere Ausnahme ");

                builder.Append(
                    level);

                builder.AppendLine(
                    ":");
            }

            builder.Append(
                currentException
                    .GetType()
                    .FullName
                ?? currentException
                    .GetType()
                    .Name);

            builder.AppendLine();

            builder.Append(
                "HRESULT: 0x");

            builder.AppendLine(
                currentException
                    .HResult
                    .ToString(
                        "X8"));

            builder.Append(
                string.IsNullOrWhiteSpace(
                    currentException.Message)
                    ? "Keine Fehlermeldung verfügbar."
                    : currentException.Message.Trim());

            currentException =
                currentException.InnerException;

            level++;
        }

        return builder
            .ToString()
            .Trim();
    }

    private static void AssignOwnerIfAvailable(
        Window dialog)
    {
        var owner =
            Application.Current.MainWindow;

        if (owner is null
            || ReferenceEquals(
                owner,
                dialog))
        {
            return;
        }

        dialog.Owner =
            owner;
    }
}