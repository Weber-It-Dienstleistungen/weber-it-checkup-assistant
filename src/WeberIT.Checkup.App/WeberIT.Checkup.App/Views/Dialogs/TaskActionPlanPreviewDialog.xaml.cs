using System.Windows;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class TaskActionPlanPreviewDialog : Window
{
    public bool IsExecutionConfirmed { get; private set; }

    public TaskActionPlanPreviewDialog(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        plan.Validate();

        InitializeComponent();

        DataContext =
            plan;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        IsExecutionConfirmed =
            false;

        Close();
    }

    private void ConfirmExecutionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (ExecutionConfirmationCheckBox.IsChecked != true)
        {
            return;
        }

        IsExecutionConfirmed =
            true;

        DialogResult =
            true;
    }
}