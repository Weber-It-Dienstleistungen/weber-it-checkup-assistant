using System.ComponentModel;
using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Views.Windows;

public partial class CustomerDeviceWindow : Window
{
    private readonly CustomerDevicesViewModel
        _viewModel;

    private readonly Guid
        _openedDeviceId;

    public CustomerDeviceWindow(
        CustomerDevicesViewModel viewModel,
        CustomerDevice device)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        ArgumentNullException.ThrowIfNull(
            device);

        _viewModel =
            viewModel;

        _openedDeviceId =
            device.Id;

        if (_viewModel.SelectedDevice?.Id
            != device.Id)
        {
            _viewModel.SelectedDevice =
                device;
        }

        InitializeComponent();

        DataContext =
            _viewModel;

        UpdateWindowTitle();

        _viewModel.PropertyChanged +=
            ViewModel_OnPropertyChanged;

        Closed +=
            CustomerDeviceWindow_OnClosed;
    }

    private void ViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(
                e.PropertyName)
            && !string.Equals(
                e.PropertyName,
                nameof(
                    CustomerDevicesViewModel
                        .SelectedDevice),
                StringComparison.Ordinal))
        {
            return;
        }

        var selectedDevice =
            _viewModel.SelectedDevice;

        /*
         * Wird das Gerät gelöscht oder wechselt der aktive
         * Gerätekontext während eines Vorgangs auf ein anderes
         * gespeichertes Gerät, gehört dieses Fenster nicht mehr
         * zum aktiven Kontext und wird geschlossen.
         */
        if (selectedDevice is null
            || selectedDevice.Id
                != _openedDeviceId)
        {
            Close();

            return;
        }

        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var displayName =
            _viewModel
                .SelectedDevice?
                .DisplayName;

        Title =
            string.IsNullOrWhiteSpace(
                displayName)
                ? "Gerät"
                : $"Gerät – {displayName}";
    }

    private void CustomerDeviceWindow_OnClosed(
        object? sender,
        EventArgs e)
    {
        _viewModel.PropertyChanged -=
            ViewModel_OnPropertyChanged;

        Closed -=
            CustomerDeviceWindow_OnClosed;
    }
}