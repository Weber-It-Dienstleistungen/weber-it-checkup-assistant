using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskStatusEditor : UserControl
{
    public CheckupTaskStatusEditor()
    {
        InitializeComponent();

        Loaded +=
            CheckupTaskStatusEditor_OnLoaded;
    }

    private void CheckupTaskStatusEditor_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        SelectInitialValues();
    }

    private void TaskListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (TaskListBox.SelectedItem
            is not CheckupTask selectedTask)
        {
            return;
        }

        SelectStatus(
            selectedTask.Status);

        StatusReasonTextBox.Text =
            selectedTask.StatusReason;

        TechnicianNoteTextBox.Text =
            selectedTask.TechnicianNote;

        SetValidationMessage(
            string.Empty,
            false);
    }

    private void ApplyStatusButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        SetValidationMessage(
            string.Empty,
            false);

        if (DataContext
            is not CheckupTaskList taskList)
        {
            SetValidationMessage(
                "Die Aufgabenliste ist nicht verfügbar.",
                false);

            return;
        }

        if (TaskListBox.SelectedItem
            is not CheckupTask selectedTask)
        {
            SetValidationMessage(
                "Bitte zuerst eine Aufgabe auswählen.",
                false);

            return;
        }

        if (!TryGetSelectedStatus(
                out var selectedStatus))
        {
            SetValidationMessage(
                "Bitte einen neuen Status auswählen.",
                false);

            return;
        }

        var statusReason =
            StatusReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                statusReason))
        {
            SetValidationMessage(
                "Für die Statusänderung ist eine "
                + "Begründung erforderlich.",
                false);

            StatusReasonTextBox.Focus();

            return;
        }

        try
        {
            taskList.ChangeTaskStatus(
                selectedTask,
                selectedStatus,
                statusReason,
                TechnicianNoteTextBox.Text);
        }
        catch (Exception exception)
        {
            SetValidationMessage(
                exception.Message,
                false);

            return;
        }

        SetValidationMessage(
            $"Status „{selectedTask.StatusText}“ "
            + "wurde für den aktuellen Checkup übernommen.",
            true);
    }

    private void SelectInitialValues()
    {
        if (TaskListBox.Items.Count == 0)
        {
            return;
        }

        if (TaskListBox.SelectedIndex < 0)
        {
            TaskListBox.SelectedIndex =
                0;
        }
    }

    private void SelectStatus(
        CheckupTaskStatus status)
    {
        foreach (var child
                 in StatusOptionsPanel.Children)
        {
            if (child is not RadioButton radioButton)
            {
                continue;
            }

            radioButton.IsChecked =
                radioButton.Tag
                    is CheckupTaskStatus itemStatus
                && itemStatus == status;
        }
    }

    private bool TryGetSelectedStatus(
        out CheckupTaskStatus status)
    {
        foreach (var child
                 in StatusOptionsPanel.Children)
        {
            if (child is RadioButton
                {
                    IsChecked: true,
                    Tag: CheckupTaskStatus selectedStatus
                })
            {
                status =
                    selectedStatus;

                return true;
            }
        }

        status =
            CheckupTaskStatus.Open;

        return false;
    }

    private void SetValidationMessage(
        string message,
        bool isSuccess)
    {
        ValidationTextBlock.Foreground =
            FindResource(
                isSuccess
                    ? "SuccessBrush"
                    : "WarningBrush")
            as Brush;

        ValidationTextBlock.Text =
            message;
    }
}