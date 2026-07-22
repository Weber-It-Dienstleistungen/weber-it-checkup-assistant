namespace WeberIT.Checkup.App.Models;

public enum CheckupScanStepStatus
{
    Pending = 0,
    Running = 1,
    Successful = 2,
    Warning = 3,
    Failed = 4
}

public sealed record CheckupScanStepDefinition(
    string StepCode,
    string Title,
    int StepNumber,
    int StartPercentage,
    int CompletionPercentage);

public static class CheckupScanStepCatalog
{
    public const int TotalStepCount =
        12;

    public static CheckupScanStepDefinition
        DeviceInformation
    { get; } =
        new(
            "device-information",
            "Geräteinformationen",
            1,
            1,
            5);

    public static CheckupScanStepDefinition
        HardwareInformation
    { get; } =
        new(
            "hardware-information",
            "Hardwareinformationen",
            2,
            6,
            13);

    public static CheckupScanStepDefinition
        OperatingSystemInformation
    { get; } =
        new(
            "operating-system-information",
            "Windows und Betriebssystem",
            3,
            14,
            20);

    public static CheckupScanStepDefinition
        StorageInformation
    { get; } =
        new(
            "storage-information",
            "Datenträger und Speicher",
            4,
            21,
            30);

    public static CheckupScanStepDefinition
        CleanupPotential
    { get; } =
        new(
            "cleanup-potential",
            "Bereinigungspotenzial",
            5,
            31,
            48);

    public static CheckupScanStepDefinition
        StartupInformation
    { get; } =
        new(
            "startup-information",
            "Autostart",
            6,
            49,
            58);

    public static CheckupScanStepDefinition
        DeviceDriverInformation
    { get; } =
        new(
            "device-driver-information",
            "Geräte und Treiber",
            7,
            59,
            70);

    public static CheckupScanStepDefinition
        SecurityInformation
    { get; } =
        new(
            "security-information",
            "Windows-Sicherheit",
            8,
            71,
            78);

    public static CheckupScanStepDefinition
        WindowsUpdateInformation
    { get; } =
        new(
            "windows-update-information",
            "Windows Update",
            9,
            79,
            86);

    public static CheckupScanStepDefinition
        ProgramUpdateInformation
    { get; } =
        new(
            "program-update-information",
            "Programmupdates",
            10,
            87,
            94);

    public static CheckupScanStepDefinition
        RestartInformation
    { get; } =
        new(
            "restart-information",
            "Neustartstatus",
            11,
            95,
            97);

    public static CheckupScanStepDefinition
        Assessment
    { get; } =
        new(
            "assessment",
            "Bewertung und Aufgaben",
            12,
            98,
            100);

    private static readonly IReadOnlyList<
        CheckupScanStepDefinition>
        AllStepsInternal =
            new List<CheckupScanStepDefinition>
            {
                DeviceInformation,
                HardwareInformation,
                OperatingSystemInformation,
                StorageInformation,
                CleanupPotential,
                StartupInformation,
                DeviceDriverInformation,
                SecurityInformation,
                WindowsUpdateInformation,
                ProgramUpdateInformation,
                RestartInformation,
                Assessment
            };

    public static IReadOnlyList<
        CheckupScanStepDefinition>
        AllSteps =>
            AllStepsInternal;
}

public sealed record CheckupScanProgress
{
    public string StepCode { get; init; } =
        string.Empty;

    public string StepTitle { get; init; } =
        string.Empty;

    public int StepNumber { get; init; }

    public int TotalStepCount { get; init; } =
        CheckupScanStepCatalog.TotalStepCount;

    public int ProgressPercentage { get; init; }

    public CheckupScanStepStatus Status { get; init; } =
        CheckupScanStepStatus.Pending;

    public string Message { get; init; } =
        string.Empty;

    public string StatusText =>
        Status switch
        {
            CheckupScanStepStatus.Running =>
                "WIRD AUSGEFÜHRT",

            CheckupScanStepStatus.Successful =>
                "ABGESCHLOSSEN",

            CheckupScanStepStatus.Warning =>
                "MIT HINWEIS",

            CheckupScanStepStatus.Failed =>
                "FEHLER",

            _ =>
                "AUSSTEHEND"
        };

    public string DisplayMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    Message))
            {
                return Message.Trim();
            }

            return Status switch
            {
                CheckupScanStepStatus.Running =>
                    "Dieser Scanbereich wird aktuell ausgelesen.",

                CheckupScanStepStatus.Successful =>
                    "Der Scanbereich wurde erfolgreich abgeschlossen.",

                CheckupScanStepStatus.Warning =>
                    "Der Scanbereich konnte nicht vollständig "
                    + "ausgewertet werden.",

                CheckupScanStepStatus.Failed =>
                    "Der Scanbereich konnte nicht abgeschlossen werden.",

                _ =>
                    "Dieser Scanbereich wurde noch nicht gestartet."
            };
        }
    }

    public static CheckupScanProgress CreatePending(
        CheckupScanStepDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        return Create(
            definition,
            0,
            CheckupScanStepStatus.Pending,
            string.Empty);
    }

    public static CheckupScanProgress CreateRunning(
        CheckupScanStepDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        return Create(
            definition,
            definition.StartPercentage,
            CheckupScanStepStatus.Running,
            "Der Scanbereich wird aktuell ausgelesen.");
    }

    public static CheckupScanProgress CreateSuccessful(
        CheckupScanStepDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        return Create(
            definition,
            definition.CompletionPercentage,
            CheckupScanStepStatus.Successful,
            "Der Scanbereich wurde erfolgreich abgeschlossen.");
    }

    public static CheckupScanProgress CreateWarning(
        CheckupScanStepDefinition definition,
        string message)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        return Create(
            definition,
            definition.CompletionPercentage,
            CheckupScanStepStatus.Warning,
            string.IsNullOrWhiteSpace(
                message)
                ? "Der Scanbereich konnte nicht vollständig "
                  + "ausgewertet werden. Der Systemscan wird "
                  + "mit den verfügbaren Daten fortgesetzt."
                : message.Trim());
    }

    public static CheckupScanProgress CreateFailed(
        CheckupScanStepDefinition definition,
        string message)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        return Create(
            definition,
            definition.StartPercentage,
            CheckupScanStepStatus.Failed,
            string.IsNullOrWhiteSpace(
                message)
                ? "Der Scanbereich konnte nicht abgeschlossen werden."
                : message.Trim());
    }

    private static CheckupScanProgress Create(
        CheckupScanStepDefinition definition,
        int progressPercentage,
        CheckupScanStepStatus status,
        string message)
    {
        return new CheckupScanProgress
        {
            StepCode =
                definition.StepCode,

            StepTitle =
                definition.Title,

            StepNumber =
                definition.StepNumber,

            TotalStepCount =
                CheckupScanStepCatalog.TotalStepCount,

            ProgressPercentage =
                Math.Clamp(
                    progressPercentage,
                    0,
                    100),

            Status =
                status,

            Message =
                message
        };
    }
}