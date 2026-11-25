using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Dokumentiert einen manuell überprüften Versionsstand. Verändert weder
/// WinGet-Messung, Ausnahmen, Aufgabenstatus noch den technischen Score.
/// </summary>
public static class ManualProgramUpdateVerificationService
{
    public const string TaskCode = "task.program-updates.available";
    public const string ActionCode = "action.program-updates.manual-verification";

    public static IReadOnlyList<AvailableProgramUpdate> GetCandidates(
        CheckupSession session, CustomerCheckupVisit visit)
    {
        if (!ReferenceEquals(session.CurrentCustomerCheckupVisit, visit)
            || !visit.IsInProgress || visit.IsCompletionPrepared || visit.Id == Guid.Empty
            || visit.BeforeCheckup?.ProgramUpdateInformation is not { } info
            || info.IsWingetAvailable != true || !info.IsAnalysisPerformed
            || !info.IsAnalysisSuccessful || info.IsResultTruncated
            || !info.AnalysisDate.HasValue || info.AvailableUpdates is null
            || info.AvailableUpdateCount != info.AvailableUpdates.Count
            || session.TaskList.Tasks.Count(t => t.TaskCode == TaskCode) != 1)
            return Array.Empty<AvailableProgramUpdate>();

        var task = session.TaskList.Tasks.Single(t => t.TaskCode == TaskCode);
        if (!visit.BeforeCheckup.TaskList.Tasks.Any(t => t.Id == task.Id && t.TaskCode == TaskCode))
            return Array.Empty<AvailableProgramUpdate>();

        return info.AvailableUpdates.Where(update => update is not null
                && !string.IsNullOrWhiteSpace(update.PackageId)
                && !string.IsNullOrWhiteSpace(update.Source)
                && !string.IsNullOrWhiteSpace(update.InstalledVersion)
                && !string.IsNullOrWhiteSpace(update.AvailableVersion)
                && !Same(update.InstalledVersion, update.AvailableVersion))
            .GroupBy(update => (update.PackageId.Trim().ToUpperInvariant(),
                update.Source.Trim().ToUpperInvariant()))
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .Where(update => !task.ActionResults.Any(action =>
                action.ActionCode == ActionCode
                && action.Status == CheckupTaskActionStatus.Successful
                && action.ManualProgramUpdateVerification is { } proof
                && proof.VisitId == visit.Id && Same(proof.PackageId, update.PackageId)
                && Same(proof.Source, update.Source)
                && Same(proof.PreviousVersion, update.InstalledVersion)
                && Same(proof.TargetVersion, update.AvailableVersion)))
            .ToList();
    }

    public static CheckupTaskActionResult CreateResult(CheckupSession session,
        CustomerCheckupVisit visit, AvailableProgramUpdate update,
        string observedVersion, string evidence, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(update);
        var matches = GetCandidates(session, visit).Where(candidate =>
            Same(candidate.PackageId, update.PackageId)
            && Same(candidate.Source, update.Source)
            && Same(candidate.InstalledVersion, update.InstalledVersion)
            && Same(candidate.AvailableVersion, update.AvailableVersion)).Take(2).ToList();

        if (matches.Count != 1 || !Same(observedVersion, matches[0].AvailableVersion)
            || string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length > 1000
            || visit.BeforeCheckup.ScanDate is not DateTime entrance
            || observedAt <= new DateTimeOffset(entrance))
            throw new InvalidOperationException(
                "Die manuelle Bestätigung benötigt eine eindeutige Paketidentität, die exakte Zielversion und eine nachvollziehbare Prüfquelle im laufenden Checkup.");

        var package = matches[0];
        var proof = new ManualProgramUpdateVerification
        {
            VisitId = visit.Id, PackageId = package.PackageId, Source = package.Source,
            PreviousVersion = package.InstalledVersion, TargetVersion = package.AvailableVersion,
            ObservedVersion = observedVersion.Trim(), Evidence = evidence.Trim()
        };

        return new CheckupTaskActionResult
        {
            ActionCode = ActionCode,
            ActionTitle = "Programmversion manuell bestätigt",
            TargetDescription = $"{package.Name} ({proof.PackageId}, {proof.Source}): {proof.PreviousVersion} → {proof.TargetVersion}",
            Status = CheckupTaskActionStatus.Successful,
            Summary = $"Techniker bestätigt die installierte Version {proof.ObservedVersion}. Prüfquelle: {proof.Evidence}. Der WinGet-Befund wird erst durch eine erneute zuverlässige Prüfung technisch bewertet.",
            Details = "Manuelle Versionskontrolle; keine WinGet-Installation ausgeführt.",
            ManualProgramUpdateVerification = proof,
            RestartStatusWasConclusive = false,
            StartedAt = observedAt, FinishedAt = observedAt
        };
    }

    private static bool Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
