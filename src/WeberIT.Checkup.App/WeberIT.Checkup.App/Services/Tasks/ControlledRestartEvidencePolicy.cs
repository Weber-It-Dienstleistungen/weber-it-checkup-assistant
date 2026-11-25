using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>Reine Nachweislogik; keine Systemaktion, kein Dateisystemzugriff.</summary>
public static class ControlledRestartEvidencePolicy
{
    public const string TaskCode = "task.restart.perform-controlled-restart";
    public const string RequestCode = "action.restart.controlled-request";
    public const string VerificationCode = "action.restart.controlled-verification";

    public static CheckupTaskActionResult? GetPendingRequest(CheckupTask task, Guid visitId)
    {
        if (visitId == Guid.Empty || task.Status != CheckupTaskStatus.Open
            || task.TaskCode != TaskCode)
            return null;

        var latest = task.ActionResults
            .Where(action => action.ActionCode == RequestCode
                && action.ControlledRestartVisitId == visitId)
            .OrderByDescending(action => action.StartedAt)
            .FirstOrDefault();
        if (latest is null || latest.Id == Guid.Empty
            || latest.Status != CheckupTaskActionStatus.Unknown
            || !latest.StartedAt.HasValue || !latest.ControlledRestartBootTimeUtc.HasValue
            || task.ActionResults.Any(action =>
                action.ActionCode == VerificationCode
                && action.ControlledRestartVisitId == visitId
                && action.ControlledRestartRequestId == latest.Id))
            return null;

        return latest;
    }

    public static bool IsRebootVerified(
        DateTimeOffset bootBefore, DateTimeOffset requestedAt,
        DateTimeOffset currentBoot, DateTimeOffset observedAt) =>
        bootBefore < requestedAt
        && requestedAt < observedAt
        && currentBoot > bootBefore
        && currentBoot >= requestedAt
        && currentBoot <= observedAt;
}
