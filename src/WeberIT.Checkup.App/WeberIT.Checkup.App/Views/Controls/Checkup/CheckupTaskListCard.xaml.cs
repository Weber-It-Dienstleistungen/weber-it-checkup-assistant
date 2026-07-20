using System.Windows;
using System.Windows.Controls;

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
}