using System.Windows;

namespace WeberIT.Checkup.App.Views.Dialogs;

public enum MessageDialogKind
{
    Error = 0,
    Information = 1
}

public partial class MessageDialog : Window
{
    public MessageDialog(
        string dialogTitle,
        string dialogMessage,
        MessageDialogKind dialogKind =
            MessageDialogKind.Error,
        string? footerText = null)
    {
        InitializeComponent();

        DialogTitle =
            dialogTitle;

        DialogMessage =
            dialogMessage;

        DialogKind =
            dialogKind;

        DialogStatusText =
            GetDialogStatusText(
                dialogKind);

        DialogFooterText =
            string.IsNullOrWhiteSpace(
                footerText)
                ? GetDefaultFooterText(
                    dialogKind)
                : footerText.Trim();

        IconGlyph =
            GetIconGlyph(
                dialogKind);

        DataContext =
            this;
    }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public MessageDialogKind DialogKind { get; }

    public string DialogStatusText { get; }

    public string DialogFooterText { get; }

    public string IconGlyph { get; }

    private static string GetDialogStatusText(
        MessageDialogKind dialogKind)
    {
        return dialogKind switch
        {
            MessageDialogKind.Information =>
                "Der Kontrollscan wurde vollständig abgeschlossen.",

            _ =>
                "Der Vorgang konnte nicht abgeschlossen werden."
        };
    }

    private static string GetDefaultFooterText(
        MessageDialogKind dialogKind)
    {
        return dialogKind switch
        {
            MessageDialogKind.Information =>
                "Das Ergebnis wurde gespeichert.",

            _ =>
                "Es wurden keine neuen Scandaten übernommen."
        };
    }

    private static string GetIconGlyph(
        MessageDialogKind dialogKind)
    {
        return dialogKind switch
        {
            MessageDialogKind.Information =>
                "\uE946",

            _ =>
                "\uEA39"
        };
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}