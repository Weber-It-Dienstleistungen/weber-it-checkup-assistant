using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CleanupActionExecutionResult
{
    public Guid PlanId { get; init; }

    public bool WasBlocked { get; init; }

    public bool WasCancelled { get; init; }

    public string ErrorMessage { get; init; } =
        string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public List<CleanupActionCategoryExecutionResult>
        CategoryResults
    {
        get;
        init;
    } = new();

    [JsonIgnore]
    public bool WasStarted =>
        CategoryResults.Any(
            result =>
                result.WasStarted);

    [JsonIgnore]
    public bool IsSuccessful =>
        !WasBlocked
        && !WasCancelled
        && string.IsNullOrWhiteSpace(
            ErrorMessage)
        && CategoryResults.Count > 0
        && CategoryResults.All(
            result =>
                result.IsSuccessful);

    [JsonIgnore]
    public bool IsPartiallySuccessful =>
        !WasBlocked
        && !WasCancelled
        && CategoryResults.Count > 0
        && CategoryResults.Any(
            result =>
                result.IsPartiallySuccessful)
        && CategoryResults.All(
            result =>
                result.IsSuccessful
                || result.IsPartiallySuccessful);

    [JsonIgnore]
    public bool HasFailures =>
        !WasBlocked
        && !WasCancelled
        && !IsSuccessful
        && !IsPartiallySuccessful
        && (!string.IsNullOrWhiteSpace(
                ErrorMessage)
            || CategoryResults.Any(
                result =>
                    !result.IsSuccessful
                    && !result.IsPartiallySuccessful));

    [JsonIgnore]
    public int CompletedCategoryCount =>
        CategoryResults.Count(
            result =>
                result.IsSuccessful
                || result.IsPartiallySuccessful);

    [JsonIgnore]
    public long DeletedFileCount =>
        CategoryResults.Sum(
            result =>
                result.DeletedFileCount);

    [JsonIgnore]
    public long DeletedDirectoryCount =>
        CategoryResults.Sum(
            result =>
                result.DeletedDirectoryCount);

    [JsonIgnore]
    public long FailedEntryCount =>
        CategoryResults.Sum(
            result =>
                result.FailedEntryCount);

    [JsonIgnore]
    public long SkippedEntryCount =>
        CategoryResults.Sum(
            result =>
                result.SkippedEntryCount);

    [JsonIgnore]
    public ulong DeletedSizeBytes
    {
        get
        {
            ulong totalSize =
                0;

            foreach (var result
                     in CategoryResults)
            {
                if (ulong.MaxValue - totalSize
                    < result.DeletedSizeBytes)
                {
                    return ulong.MaxValue;
                }

                totalSize +=
                    result.DeletedSizeBytes;
            }

            return totalSize;
        }
    }

    [JsonIgnore]
    public TimeSpan Duration =>
        FinishedAt > StartedAt
            ? FinishedAt - StartedAt
            : TimeSpan.Zero;
}