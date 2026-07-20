using System.Windows;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class TaskActionDetailsDialog : Window
{
    public TaskActionDetailsDialog(
        CheckupTaskActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        InitializeComponent();

        DataContext =
            definition;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}