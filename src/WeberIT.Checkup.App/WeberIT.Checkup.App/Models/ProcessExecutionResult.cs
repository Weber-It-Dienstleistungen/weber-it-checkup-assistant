namespace WeberIT.Checkup.App.Models;

public class ProcessExecutionResult
{
    public bool WasStarted { get; init; }

    public bool WasElevated { get; init; }

    public bool ElevationWasCancelled { get; init; }

    public int? ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public TimeSpan Duration =>
        FinishedAt > StartedAt
            ? FinishedAt - StartedAt
            : TimeSpan.Zero;
}