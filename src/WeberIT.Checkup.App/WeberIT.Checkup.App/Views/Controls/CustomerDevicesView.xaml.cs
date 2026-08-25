using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Windows;

namespace WeberIT.Checkup.App.Views.Controls;

public partial class CustomerDevicesView : UserControl
{
    public CustomerDevicesView()
    {
        InitializeComponent();
    }

    protected override void OnMouseDoubleClick(
        MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(
            e);

        if (e.Handled
            || e.ChangedButton
                != MouseButton.Left)
        {
            return;
        }

        var source =
            e.OriginalSource
            as DependencyObject;

        var listBoxItem =
            FindParent<ListBoxItem>(
                source);

        if (listBoxItem?.DataContext
            is not CustomerDevice device)
        {
            return;
        }

        if (DataContext
            is not CustomerDevicesViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedDevice =
            device;

        var deviceWindow =
            new CustomerDeviceWindow(
                viewModel,
                device)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        e.Handled =
            true;

        deviceWindow.ShowDialog();
    }

    private static T? FindParent<T>(
        DependencyObject? source)
        where T : DependencyObject
    {
        var current =
            source;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current =
                GetParent(
                    current);
        }

        return null;
    }

    private static DependencyObject? GetParent(
        DependencyObject source)
    {
        if (source is Visual
            || source is Visual3D)
        {
            return VisualTreeHelper.GetParent(
                source);
        }

        return LogicalTreeHelper.GetParent(
            source);
    }
}