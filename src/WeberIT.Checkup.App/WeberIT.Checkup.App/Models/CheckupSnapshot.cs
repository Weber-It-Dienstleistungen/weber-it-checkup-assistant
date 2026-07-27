using System.Text.Json;

namespace WeberIT.Checkup.App.Models;

public sealed class CheckupSnapshot
{
    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };

    public DateTime? ScanDate { get; set; }

    public DeviceInformation DeviceInformation { get; set; } =
        new();

    public HardwareInformation HardwareInformation { get; set; } =
        new();

    public OperatingSystemInformation OperatingSystemInformation
    {
        get;
        set;
    } = new();

    public StorageInformation StorageInformation { get; set; } =
        new();

    public CleanupPotentialInformation CleanupPotentialInformation
    {
        get;
        set;
    } = new();

    public StartupInformation StartupInformation
    {
        get;
        set;
    } = new();

    public DeviceDriverInformation DeviceDriverInformation
    {
        get;
        set;
    } = new();

    public SecurityInformation SecurityInformation { get; set; } =
        new();

    public WindowsUpdateInformation WindowsUpdateInformation
    {
        get;
        set;
    } = new();

    public ProgramUpdateInformation ProgramUpdateInformation
    {
        get;
        set;
    } = new();

    public RestartInformation RestartInformation { get; set; } =
        new();

    public CheckupAssessment Assessment { get; set; } =
        new();

    public CheckupTaskList TaskList { get; set; } =
        new();

    public static CheckupSnapshot Capture(
        CheckupSession checkupSession)
    {
        ArgumentNullException.ThrowIfNull(
            checkupSession);

        return new CheckupSnapshot
        {
            ScanDate =
                checkupSession.ScanDate,

            DeviceInformation =
                Clone(
                    checkupSession.DeviceInformation),

            HardwareInformation =
                Clone(
                    checkupSession.HardwareInformation),

            OperatingSystemInformation =
                Clone(
                    checkupSession.OperatingSystemInformation),

            StorageInformation =
                Clone(
                    checkupSession.StorageInformation),

            CleanupPotentialInformation =
                Clone(
                    checkupSession.CleanupPotentialInformation),

            StartupInformation =
                Clone(
                    checkupSession.StartupInformation),

            DeviceDriverInformation =
                Clone(
                    checkupSession.DeviceDriverInformation),

            SecurityInformation =
                Clone(
                    checkupSession.SecurityInformation),

            WindowsUpdateInformation =
                Clone(
                    checkupSession.WindowsUpdateInformation),

            ProgramUpdateInformation =
                Clone(
                    checkupSession.ProgramUpdateInformation),

            RestartInformation =
                Clone(
                    checkupSession.RestartInformation),

            Assessment =
                Clone(
                    checkupSession.Assessment),

            TaskList =
                Clone(
                    checkupSession.TaskList)
        };
    }

    public CheckupSession RestoreAsSession()
    {
        return new CheckupSession
        {
            ScanDate =
                ScanDate,

            DeviceInformation =
                Clone(
                    DeviceInformation),

            HardwareInformation =
                Clone(
                    HardwareInformation),

            OperatingSystemInformation =
                Clone(
                    OperatingSystemInformation),

            StorageInformation =
                Clone(
                    StorageInformation),

            CleanupPotentialInformation =
                Clone(
                    CleanupPotentialInformation),

            StartupInformation =
                Clone(
                    StartupInformation),

            DeviceDriverInformation =
                Clone(
                    DeviceDriverInformation),

            SecurityInformation =
                Clone(
                    SecurityInformation),

            WindowsUpdateInformation =
                Clone(
                    WindowsUpdateInformation),

            ProgramUpdateInformation =
                Clone(
                    ProgramUpdateInformation),

            RestartInformation =
                Clone(
                    RestartInformation),

            Assessment =
                Clone(
                    Assessment),

            TaskList =
                Clone(
                    TaskList)
        };
    }

    private static T Clone<T>(
        T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(
            value);

        var json =
            JsonSerializer.Serialize(
                value,
                SerializerOptions);

        var clone =
            JsonSerializer.Deserialize<T>(
                json,
                SerializerOptions);

        return clone
               ?? throw new InvalidOperationException(
                   $"Der Checkup-Bestandteil "
                   + $"\"{typeof(T).Name}\" konnte nicht "
                   + "als unabhängige Momentaufnahme "
                   + "gesichert werden.");
    }
}