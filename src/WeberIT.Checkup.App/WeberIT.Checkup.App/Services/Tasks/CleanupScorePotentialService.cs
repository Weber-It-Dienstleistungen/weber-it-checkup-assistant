using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.44: Bewertungsgrundlage fuer den Abschluss-Scan.
/// Der gemessene Restbestand bleibt unveraendert. Nur eindeutig
/// abgeschlossene Kategorien desselben Vorgangs koennen von der
/// erneuten Bereinigungsempfehlung ausgenommen werden.
/// Keine Dateisystemzugriffe und keine Aenderung des Systemscore.
/// </summary>
public static class CleanupScorePotentialService
{
    public static ulong GetScoreRelevantSafePotentialBytes(
        CleanupPotentialInformation measurement,
        CheckupTaskList taskList,
        Guid visitId,
        DateTimeOffset entranceScanAt,
        DateTimeOffset measurementStartedAt)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(taskList);

        if (visitId == Guid.Empty
            || entranceScanAt >= measurementStartedAt
            || measurement.AnalysisStatus != CleanupMeasurementStatus.Measured
            || CleanupCategoryExecutionHistoryGuard
                .HasUnattributedCleanupExecution(taskList))
        {
            return measurement.SafePotentialBytes;
        }

        var actions = CleanupCategoryExecutionHistoryGuard
            .GetCleanupActions(taskList)
            .Where(action => !action.CleanupCustomerCheckupVisitId.HasValue
                || action.CleanupCustomerCheckupVisitId == Guid.Empty
                || action.CleanupCustomerCheckupVisitId == visitId)
            .ToList();

        ulong total = 0;
        foreach (var category in measurement.Categories)
        {
            if (category.Classification != CleanupCategoryClassification.SafePotential
                || !category.HasMeasuredSize || !category.SizeBytes.HasValue)
                continue;

            // Mehrdeutige Messungen werden nicht von der Bewertung ausgenommen.
            var uniqueMeasurement = measurement.Categories.Count(
                item => item.Category == category.Category) == 1;

            if (uniqueMeasurement && HasCompletedWork(
                actions, category.Category, visitId,
                entranceScanAt, measurementStartedAt))
                continue;

            var bytes = category.SizeBytes.Value;
            if (ulong.MaxValue - total < bytes)
                return ulong.MaxValue;

            total += bytes;
        }

        return total;
    }

    private static bool HasCompletedWork(
        IReadOnlyCollection<CheckupTaskActionResult> actions,
        CleanupCategoryType category,
        Guid visitId,
        DateTimeOffset entranceScanAt,
        DateTimeOffset measurementStartedAt)
    {
        if (!Enum.IsDefined(typeof(CleanupCategoryType), category))
            return false;

        var matches = actions.Where(action =>
            action.CleanupExecutionResult?.CategoryResults?.Any(result =>
                result is not null && result.WasStarted
                && result.Category == category) == true)
            .Take(2).ToList();

        if (matches.Count != 1)
            return false;

        var action = matches[0];
        if (action.Id == Guid.Empty
            || action.CleanupCustomerCheckupVisitId != visitId
            || !action.HasCorrelatableCleanupExecution
            || action.Status != CheckupTaskActionStatus.Successful
            || action.StartedAt!.Value <= entranceScanAt
            || action.FinishedAt!.Value < action.StartedAt.Value
            || action.FinishedAt.Value > measurementStartedAt)
            return false;

        var execution = action.CleanupExecutionResult!;
        var result = execution.CategoryResults[0];

        return execution.IsWorkCompleted
            && result.IsWorkCompleted
            && (execution.IsSuccessful || execution.IsPartiallySuccessful)
            && result.HasOnlyExpectedSafetySkips;
    }
}
