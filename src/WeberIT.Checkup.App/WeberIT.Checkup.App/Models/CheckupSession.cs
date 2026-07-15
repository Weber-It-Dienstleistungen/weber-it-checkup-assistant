namespace WeberIT.Checkup.App.Models;

public class CheckupSession
{
    public DateTime? ScanDate { get; set; }

    public DeviceInformation DeviceInformation { get; set; } =
        new();

    public HardwareInformation HardwareInformation { get; set; } =
        new();

    public OperatingSystemInformation OperatingSystemInformation { get; set; } =
        new();

    public StorageInformation StorageInformation { get; set; } =
        new();

    public SecurityInformation SecurityInformation { get; set; } =
        new();

    public WindowsUpdateInformation WindowsUpdateInformation { get; set; } =
        new();

    public ProgramUpdateInformation ProgramUpdateInformation { get; set; } =
        new();

    public RestartInformation RestartInformation { get; set; } =
        new();

    public CheckupAssessment Assessment { get; set; } =
        new();
}