namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Enge Freigabeliste fuer ausdruecklich akzeptierte, fachlich vertretbare
/// Empfehlungen. Keine pauschale Entlastung durch Erledigt/Uebersprungen.
/// </summary>
public static class CheckupRiskAcceptancePolicy
{
    public static bool SupportsTaskCode(string? taskCode) =>
        taskCode is "task.security.drive-encryption"
            or "task.hardware.memory-capacity"
            or "task.performance.startup-background-load";

    public static bool IsAllowedFinding(string? taskCode, string? findingCode) =>
        taskCode switch
        {
            "task.security.drive-encryption" => findingCode is
                "system.security.mobile-drive-not-encrypted"
                or "system.security.stationary-drive-not-encrypted",
            "task.hardware.memory-capacity" =>
                findingCode == "hardware.memory.upgrade-recommended",
            "task.performance.startup-background-load" => findingCode is
                "system.startup.optional-entries" or "system.startup.extensive",
            _ => false
        };

    public static bool CanAccept(CheckupTask task) =>
        task.Priority != CheckupTaskPriority.Required
        && SupportsTaskCode(task.TaskCode)
        && task.SourceFindingCodes is { Count: > 0 }
        && task.SourceFindingCodes.All(code => IsAllowedFinding(task.TaskCode, code));

    public static bool CanConfirmStartupReview(CheckupTask task) =>
        task.TaskCode == "task.performance.startup-background-load" && CanAccept(task);

    // Verglichen wird die vollständige gespeicherte Darstellung der aktiven
    // Einträge, nicht allein die Anzahl. Neue oder geänderte Einträge benötigen
    // eine erneute Prüfung. Die unterstützten Quellen bleiben begrenzt.
    public static bool IsStartupReviewConfirmed(
        CheckupFinding finding, CheckupTaskList workingTasks,
        CheckupTaskList entranceTasks, StartupInformation entrance,
        StartupInformation after, DateTimeOffset entranceAt,
        DateTimeOffset scanStartedAt)
    {
        if (finding.Code is not ("system.startup.optional-entries" or "system.startup.extensive")
            || !IsAcceptedForFinding(finding, workingTasks, entranceTasks, entranceAt,
                scanStartedAt, CheckupTaskStatus.NoOptimizationRequired)
            || entrance.AnalysisStatus != StartupAnalysisStatus.Analyzed
            || after.AnalysisStatus != StartupAnalysisStatus.Analyzed
            || !entrance.AnalysisDate.HasValue || !after.AnalysisDate.HasValue
            || new DateTimeOffset(after.AnalysisDate.Value) < scanStartedAt
            || entrance.Entries is null || after.Entries is null
            || after.ConspicuousEntryCount != 0
            || after.ActiveNotEvaluableEntryCount != 0
            || after.UnknownStateEntryCount != 0)
            return false;

        var previous = entrance.Entries.Where(e => e.State == StartupEntryState.Enabled)
            .Select(e => System.Text.Json.JsonSerializer.Serialize(e))
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var entry in after.Entries.Where(e => e.State == StartupEntryState.Enabled))
        {
            if (string.IsNullOrWhiteSpace(entry.SourceIdentifier))
                return false;
            var key = System.Text.Json.JsonSerializer.Serialize(entry);
            if (!previous.TryGetValue(key, out var count) || count == 0)
                return false;
            previous[key] = count - 1;
        }
        return after.EnabledEntryCount > 0;
    }

    public static bool IsAcceptedForFinding(
        CheckupFinding finding,
        CheckupTaskList workingTasks,
        CheckupTaskList entranceTasks,
        DateTimeOffset entranceAt,
        DateTimeOffset scanStartedAt,
        CheckupTaskStatus requiredStatus = CheckupTaskStatus.RiskAccepted)
    {
        if (requiredStatus is not (CheckupTaskStatus.RiskAccepted or CheckupTaskStatus.NoOptimizationRequired)
            || finding.Severity != FindingSeverity.Recommendation
            || entranceAt >= scanStartedAt)
            return false;

        var matches = workingTasks.Tasks.Where(task => task is not null
            && task.SourceFindingCodes is not null
            && task.SourceFindingCodes.Contains(finding.Code, StringComparer.Ordinal))
            .Take(2).ToList();

        if (matches.Count != 1)
            return false;

        var task = matches[0];
        if (task.Id == Guid.Empty || task.Status != requiredStatus
            || !CanAccept(task) || !IsAllowedFinding(task.TaskCode, finding.Code)
            || (requiredStatus == CheckupTaskStatus.NoOptimizationRequired && !CanConfirmStartupReview(task))
            || string.IsNullOrWhiteSpace(task.StatusReason)
            || !task.StatusChangedAt.HasValue)
            return false;

        var decisionAt = new DateTimeOffset(task.StatusChangedAt.Value);
        if (decisionAt <= entranceAt || decisionAt > scanStartedAt)
            return false;

        var original = entranceTasks.Tasks.Where(item => item is not null
            && item.Id == task.Id && item.TaskCode == task.TaskCode)
            .Take(2).ToList();
        return original.Count == 1
            && CanAccept(original[0])
            && original[0].SourceFindingCodes.Contains(finding.Code, StringComparer.Ordinal);
    }
}
