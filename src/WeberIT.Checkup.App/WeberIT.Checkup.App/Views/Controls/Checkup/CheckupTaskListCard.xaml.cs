using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskListCard : UserControl
{
    public static readonly DependencyProperty CheckupSessionProperty =
        DependencyProperty.Register(
            nameof(CheckupSession),
            typeof(CheckupSession),
            typeof(CheckupTaskListCard),
            new PropertyMetadata(null));

    public CheckupTaskListCard()
    {
        InitializeComponent();

        AddStatusEditor();
    }

    public CheckupSession? CheckupSession
    {
        get =>
            (CheckupSession?)GetValue(
                CheckupSessionProperty);

        set =>
            SetValue(
                CheckupSessionProperty,
                value);
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

    private void ProgramUpdateSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.CommandParameter
                is not CheckupTask task)
        {
            ShowPreparationError(
                "Die zugehörige Aufgabe konnte nicht "
                + "eindeutig bestimmt werden.");

            return;
        }

        if (CheckupSession is null)
        {
            ShowPreparationError(
                "Der zugehörige Checkup-Kontext ist "
                + "nicht verfügbar.");

            return;
        }

        var dialog =
            new ProgramUpdateSelectionDialog(
                task,
                CheckupSession.ProgramUpdateInformation,
                CheckupSession.TaskList)
            {
                Owner =
                    Window.GetWindow(this)
            };

        dialog.ShowDialog();
    }

    private void ShowPreparationError(
        string message)
    {
        var dialog =
            new MessageDialog(
                "Aktionsvorbereitung nicht möglich",
                message)
            {
                Owner =
                    Window.GetWindow(this)
            };

        dialog.ShowDialog();
    }
}