using System.Windows;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog(
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

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}