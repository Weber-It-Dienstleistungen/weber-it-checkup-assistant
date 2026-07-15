namespace WeberIT.Checkup.App.Models;

public class WindowsUpdateInformation
{
    public bool IsUpdateSearchPerformed { get; set; }

    public bool IsUpdateSearchSuccessful { get; set; }

    public DateTime? UpdateSearchDate { get; set; }

    public string UpdateSearchDetails { get; set; } =
        string.Empty;

    public List<PendingWindowsUpdate> PendingUpdates { get; set; } =
        new();

    public bool? IsRestartRequired { get; set; }

    public List<string> RestartReasons { get; set; } =
        new();

    public DateTime? LastSuccessfulInstallationDate { get; set; }

    public List<WindowsUpdateFailure> RecentFailures { get; set; } =
        new();

    public WindowsUpdateServiceState ServiceState { get; set; } =
        WindowsUpdateServiceState.Unknown;

    public string ServiceStartMode { get; set; } =
        string.Empty;

    public string ServiceStatusDetails { get; set; } =
        string.Empty;
}