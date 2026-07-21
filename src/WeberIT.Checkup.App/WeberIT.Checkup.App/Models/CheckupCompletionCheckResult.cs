using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CheckupCompletionCheckResult
{
    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public DateTimeOffset VerificationScanDate { get; init; }

    public int CurrentFindingCount { get; init; }

    public int CurrentTaskCount { get; init; }

    public List<CheckupTaskCompletionCheckResult>
        TaskResults
    {
        get;
        init;
    } = new();

    [JsonIgnore]
    public int ResolvedTaskCount =>
        TaskResults.Count(
            result =>
                !result.FindingStillPresent);

    [JsonIgnore]
    public int RemainingTaskCount =>
        TaskResults.Count(
            result =>
                result.FindingStillPresent);

    [JsonIgnore]
    public TimeSpan Duration =>
        FinishedAt > StartedAt
            ? FinishedAt - StartedAt
            : TimeSpan.Zero;
}

public sealed class CheckupTaskCompletionCheckResult
{
    public Guid TaskId { get; init; }

    public string TaskCode { get; init; } =
        string.Empty;

    public string TaskTitle { get; init; } =
        string.Empty;

    public bool FindingStillPresent { get; init; }
}