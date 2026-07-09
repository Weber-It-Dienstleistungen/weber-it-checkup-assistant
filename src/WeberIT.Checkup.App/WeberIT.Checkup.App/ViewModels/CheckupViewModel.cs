using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CheckupViewModel : BaseViewModel
{
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;

    private CheckupSession _currentCheckup = new();

    public string Title => "Gerät / Checkup";

    public string Subtitle =>
        "Systeminformationen auslesen und für den späteren Checkup vorbereiten.";

    public CheckupSession CurrentCheckup
    {
        get => _currentCheckup;
        private set
        {
            _currentCheckup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceInformation));
            OnPropertyChanged(nameof(HardwareInformation));
            OnPropertyChanged(nameof(OperatingSystemInformation));
            OnPropertyChanged(nameof(StorageInformation));
            OnPropertyChanged(nameof(Assessment));
            OnPropertyChanged(nameof(ScanDate));
            OnPropertyChanged(nameof(ScanStatusText));
        }
    }

    public DeviceInformation DeviceInformation => CurrentCheckup.DeviceInformation;

    public HardwareInformation HardwareInformation => CurrentCheckup.HardwareInformation;

    public OperatingSystemInformation OperatingSystemInformation => CurrentCheckup.OperatingSystemInformation;

    public StorageInformation StorageInformation => CurrentCheckup.StorageInformation;

    public CheckupAssessment Assessment => CurrentCheckup.Assessment;

    public DateTime? ScanDate => CurrentCheckup.ScanDate;

    public string ScanStatusText =>
        ScanDate.HasValue
            ? $"Letzter Scan: {ScanDate.Value:dd.MM.yyyy HH:mm}"
            : "Noch kein Systemscan durchgeführt.";

    public ICommand ReadSystemCommand { get; }

    public CheckupViewModel(
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService)
    {
        _checkupScanner = checkupScanner;
        _checkupAssessmentService = checkupAssessmentService;

        ReadSystemCommand = new RelayCommand(_ => ReadSystem());
    }

    private void ReadSystem()
    {
        var checkupSession = _checkupScanner.Scan();
        checkupSession.Assessment = _checkupAssessmentService.Assess(checkupSession);

        CurrentCheckup = checkupSession;
    }
}