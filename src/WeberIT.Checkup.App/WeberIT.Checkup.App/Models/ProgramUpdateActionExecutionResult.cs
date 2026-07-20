using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class ProgramUpdateActionExecutionResult
{
    public Guid PlanId { get; init; }

    public bool WasBlocked { get; init; }

    public string ErrorMessage { get; init; } =
        string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public List<ProcessExecutionResult> CommandResults
    {
        get;
        init;
    } = new();

    [JsonIgnore]
    public bool WasStarted =>
        CommandResults.Any(
            result =>
                result.WasStarted);

    [JsonIgnore]
    public bool IsSuccessful =>
        !WasBlocked
        && string.IsNullOrWhiteSpace(
            ErrorMessage)
        && CommandResults.Count > 0
        && CommandResults.All(
            result =>
                result.WasStarted
                && result.ExitCode == 0);

    [JsonIgnore]
    public bool HasFailures =>
        !WasBlocked
        && (!string.IsNullOrWhiteSpace(
                ErrorMessage)
            || CommandResults.Any(
                result =>
                    !result.WasStarted
                    || result.ExitCode != 0));

    [JsonIgnore]
    public int CompletedCommandCount =>
        CommandResults.Count(
            result =>
                result.WasStarted
                && result.ExitCode == 0);

    [JsonIgnore]
    public TimeSpan Duration =>
        FinishedAt > StartedAt
            ? FinishedAt - StartedAt
            : TimeSpan.Zero;
}