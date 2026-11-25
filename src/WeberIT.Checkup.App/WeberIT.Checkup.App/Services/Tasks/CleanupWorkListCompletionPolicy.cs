using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>Nur die Arbeitslistenprojektion; kein neuer Status, keine Punkte,
/// keine neue Messung und keine Freigabe zur wiederholten Löschung.</summary>
internal static class CleanupWorkListCompletionPolicy
{
    internal static bool IsCleanupTask(CheckupTask task) =>
        task.TaskCode is "task.storage.controlled-cleanup"
            or "task.storage.restore-system-volume-capacity";

    internal static DateTimeOffset? GetCompletedWorkAt(CheckupSession? session, CheckupTaskList list)
    {
        if (session is null || session.ScanDate is not DateTime scanDate
            || !ReferenceEquals(session.TaskList, list))
            return null;

        var visit = session.CurrentCustomerCheckupVisit;
        if (visit is { IsCompletionPrepared: true } || visit?.Id == Guid.Empty)
            return null;
        var measured = session.CleanupPotentialInformation;
        if (measured is null || !measured.AnalysisDate.HasValue || measured.Categories is null
            || CleanupCategoryExecutionHistoryGuard.HasUnattributedCleanupExecution(list))
            return null;

        IReadOnlyList<CleanupActionCategory> selectable;
        try
        {
            selectable = new CleanupActionPlanBuilder().GetSelectableCategories(measured);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return null;
        }

        // Doppelte Eingangsmessungen dürfen nicht durch die Auswahlfilter
        // unbemerkt verschwinden. Nicht freigegebene Kategorien sind keine
        // durchgeführte Arbeit; sie bleiben in der Messung dokumentiert.
        if (selectable.Count == 0 || measured.Categories.Any(c => c is null)
            || measured.Categories.Where(c => CleanupActionCategory.IsSupportedCategory(c.Category))
                .GroupBy(c => c.Category).Any(g => g.Count() != 1))
            return null;

        var actions = CleanupCategoryExecutionHistoryGuard.GetCleanupActions(list).ToList();
        var expected = selectable.Select(c => c.Category!.Value).ToHashSet();
        var started = actions.Where(a => a.CleanupExecutionResult?.CategoryResults?
            .Any(c => c is not null && c.WasStarted) == true).ToList();
        if (started.Count != expected.Count
            || started.Select(a => a.Id).Distinct().Count() != started.Count)
            return null;

        var entrance = new DateTimeOffset(visit?.BeforeCheckup?.ScanDate ?? scanDate);
        var measurementAt = new DateTimeOffset(measured.AnalysisDate.Value);
        DateTimeOffset? latest = null;
        var seenPlans = new HashSet<Guid>();
        foreach (var action in started)
        {
            var execution = action.CleanupExecutionResult!;
            if (action.Id == Guid.Empty || action.CleanupCustomerCheckupVisitId != visit?.Id
                || action.Status != CheckupTaskActionStatus.Successful
                || execution.PlanId == Guid.Empty || !seenPlans.Add(execution.PlanId)
                || execution.CategoryResults is not { Count: 1 }
                || execution.CategoryResults[0] is not { } result
                || !expected.Remove(result.Category)
                || !action.StartedAt.HasValue || !action.FinishedAt.HasValue
                || action.StartedAt.Value <= entrance || action.StartedAt.Value < measurementAt
                || action.StartedAt.Value != execution.StartedAt
                || action.FinishedAt.Value != execution.FinishedAt
                || action.FinishedAt.Value < action.StartedAt.Value
                || !execution.IsWorkCompleted || !result.IsWorkCompleted
                || !(execution.IsSuccessful || execution.IsPartiallySuccessful)
                || !result.HasOnlyExpectedSafetySkips)
                return null;

            if (!latest.HasValue || action.FinishedAt.Value > latest.Value)
                latest = action.FinishedAt.Value;
        }

        return expected.Count == 0 ? latest : null;
    }
}
