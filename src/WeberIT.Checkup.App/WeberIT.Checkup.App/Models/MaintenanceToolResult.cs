namespace WeberIT.Checkup.App.Models;

public class MaintenanceToolResult
{
    public MaintenanceToolStatus Status { get; init; } =
        MaintenanceToolStatus.NotRun;

    public string Summary { get; init; } =
        "Dieses Werkzeug wurde noch nicht ausgeführt.";

    public string Details { get; init; } = string.Empty;

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public int? ExitCode { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public TimeSpan Duration =>
        StartedAt.HasValue
        && FinishedAt.HasValue
        && FinishedAt.Value > StartedAt.Value
            ? FinishedAt.Value - StartedAt.Value
            : TimeSpan.Zero;
}