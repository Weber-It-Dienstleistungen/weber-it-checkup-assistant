using System.Windows;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(
        string dialogTitle,
        string dialogMessage)
    {
        InitializeComponent();

        DialogTitle = dialogTitle;
        DialogMessage = dialogMessage;

        DataContext = this;
    }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    private void ConfirmButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }
}