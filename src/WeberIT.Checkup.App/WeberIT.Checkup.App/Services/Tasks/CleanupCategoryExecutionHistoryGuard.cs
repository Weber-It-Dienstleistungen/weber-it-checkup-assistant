using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.40.
///
/// Verhindert, dass eine im laufenden Checkup bereits
/// gestartete Bereinigungskategorie anhand der
/// unveränderten Eingangsmesswerte erneut ausgeführt
/// wird.
///
/// Die Historie basiert nur auf vorhandenen
/// Aufgabenprotokollen. Keine Dateisystemzugriffe,
/// keine wiederholten Löschversuche.
/// </summary>
public static class CleanupCategoryExecutionHistoryGuard
{
    private const string ActionCode =
        "action.cleanup.selected-safe-categories";

    private const string CleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string CapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    public static IReadOnlySet<CleanupCategoryType>
        GetPreviouslyStartedCategories(
            CheckupTaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(taskList);

        return GetCleanupActions(taskList)
            .Where(action =>
                action.CleanupExecutionResult is not null)
            .SelectMany(action =>
                action.CleanupExecutionResult!
                    .CategoryResults
                ?? new List<CleanupActionCategoryExecutionResult>())
            .Where(result =>
                result is not null
                && result.WasStarted
                && Enum.IsDefined(
                    typeof(CleanupCategoryType),
                    result.Category))
            .Select(result => result.Category)
            .ToHashSet();
    }

    /// <summary>
    /// Alte oder unvollständige Aktionsergebnisse
    /// ohne eindeutige Kategoriezuordnung dürfen
    /// nicht stillschweigend zum Freibrief für
    /// eine erneute Löschung werden.
    /// </summary>
    public static bool HasUnattributedCleanupExecution(
        CheckupTaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(taskList);

        return GetCleanupActions(taskList)
            .Any(action =>
            {
                var result =
                    action.CleanupExecutionResult;

                if (result is null)
                {
                    return action.StartedAt.HasValue
                        && action.Status
                            != CheckupTaskActionStatus.Unknown;
                }

                var categories =
                    result.CategoryResults;

                if (categories is null
                    || categories.Count != 1)
                {
                    return result.WasStarted
                        || action.Status ==
                            CheckupTaskActionStatus.Successful;
                }

                var category =
                    categories[0];

                return category is null
                    || category.WasStarted
                    && !Enum.IsDefined(
                        typeof(CleanupCategoryType),
                        category.Category)
                    || !category.WasStarted
                    && action.Status ==
                        CheckupTaskActionStatus.Successful;
            });
    }

    public static bool CanStartCategory(
        CheckupTaskList taskList,
        CleanupCategoryType category)
    {
        ArgumentNullException.ThrowIfNull(taskList);

        return !HasUnattributedCleanupExecution(taskList)
            && !GetPreviouslyStartedCategories(taskList)
                .Contains(category);
    }

    public static void EnsureCanStartCategory(
        CheckupTaskList taskList,
        CleanupCategoryType category)
    {
        ArgumentNullException.ThrowIfNull(taskList);

        if (HasUnattributedCleanupExecution(taskList))
        {
            throw new InvalidOperationException(
                "In diesem Checkup wurde bereits eine "
                + "Bereinigungsaktion dokumentiert, "
                + "deren Kategorie nicht eindeutig "
                + "nachvollziehbar ist. "
                + "Eine neue Löschaktion anhand des "
                + "alten Eingangsscans wird aus "
                + "Sicherheitsgründen nicht gestartet.");
        }

        if (GetPreviouslyStartedCategories(taskList)
            .Contains(category))
        {
            throw new InvalidOperationException(
                "Diese Bereinigungskategorie wurde im "
                + "aktuellen Checkup bereits gestartet. "
                + "Eine erneute Löschung anhand der "
                + "unveränderten Eingangsmessung "
                + "ist nicht zulässig. "
                + "Die lesende Zwischenprüfung bleibt "
                + "weiterhin verfügbar.");
        }
    }

    internal static IEnumerable<CheckupTaskActionResult>
        GetCleanupActions(
            CheckupTaskList taskList)
    {
        return (taskList.Tasks
                ?? new List<CheckupTask>())
            .Where(task =>
                task is not null
                && (string.Equals(
                        task.TaskCode,
                        CleanupTaskCode,
                        StringComparison.Ordinal)
                    || string.Equals(
                        task.TaskCode,
                        CapacityTaskCode,
                        StringComparison.Ordinal)))
            .SelectMany(task =>
                task.ActionResults
                ?? new List<CheckupTaskActionResult>())
            .Where(action =>
                action is not null
                && string.Equals(
                    action.ActionCode,
                    ActionCode,
                    StringComparison.Ordinal));
    }
}
