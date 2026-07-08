using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CheckupViewModel : BaseViewModel
{
    private readonly IDeviceScanner _deviceScanner;

    private DeviceInformation _deviceInformation = new();
    private DateTime? _scanDate;

    public string Title => "Gerät / Checkup";

    public string Subtitle =>
        "Systeminformationen auslesen und für den späteren Checkup vorbereiten.";

    public DeviceInformation DeviceInformation
    {
        get => _deviceInformation;
        private set
        {
            _deviceInformation = value;
            OnPropertyChanged();
        }
    }

    public DateTime? ScanDate
    {
        get => _scanDate;
        private set
        {
            _scanDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScanStatusText));
        }
    }

    public string ScanStatusText =>
        ScanDate.HasValue
            ? $"Letzter Scan: {ScanDate.Value:dd.MM.yyyy HH:mm}"
            : "Noch kein Systemscan durchgeführt.";

    public ICommand ReadSystemCommand { get; }

    public CheckupViewModel(IDeviceScanner deviceScanner)
    {
        _deviceScanner = deviceScanner;

        ReadSystemCommand = new RelayCommand(_ => ReadSystem());
    }

    private void ReadSystem()
    {
        var scanResult = _deviceScanner.Scan();

        DeviceInformation = scanResult.DeviceInformation;
        ScanDate = scanResult.ScanDate;
    }
}