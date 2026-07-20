using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskListCard : UserControl
{
    public CheckupTaskListCard()
    {
        InitializeComponent();

        AddStatusEditor();
    }

    private void AddStatusEditor()
    {
        if (Content is not Border rootBorder
            || rootBorder.Child
                is not StackPanel rootStackPanel)
        {
            return;
        }

        var statusEditor =
            new CheckupTaskStatusEditor
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        rootStackPanel.Children.Add(
            statusEditor);
    }

    private void ActionDetailsButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.DataContext
                is not CheckupTaskActionDefinition definition)
        {
            return;
        }

        var dialog =
            new TaskActionDetailsDialog(
                definition)
            {
                Owner =
                    Window.GetWindow(this)
            };

        dialog.ShowDialog();
    }
}