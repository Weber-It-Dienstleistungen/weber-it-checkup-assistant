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

    // Nur eine bereits nachgewiesene Neustartaktion mit weiterem Bedarf
    // ist von der allgemeinen Verifikationssperre ausgenommen.
    // Alte, fremde, unvollstaendige oder nicht passende Protokolle bleiben
    // unter der bisherigen Schutzlogik.
    public static bool IsVerifiedFurtherRestart(CheckupTask task, CheckupTaskActionResult verification)
    {
        if (task.TaskCode != TaskCode
            || verification.ActionCode != VerificationCode
            || verification.Status != CheckupTaskActionStatus.Successful
            || !verification.RestartRequired || !verification.RestartStatusWasConclusive
            || verification.ControlledRestartVisitId is not Guid visitId || visitId == Guid.Empty
            || verification.ControlledRestartRequestId is not Guid requestId || requestId == Guid.Empty
            || !verification.ControlledRestartBootTimeUtc.HasValue
            || !verification.StartedAt.HasValue || !verification.FinishedAt.HasValue
            || verification.FinishedAt.Value < verification.StartedAt.Value)
            return false;

        var requests = task.ActionResults.Where(action =>
            action.Id == requestId && action.ActionCode == RequestCode
            && action.ControlledRestartVisitId == visitId
            && action.Status == CheckupTaskActionStatus.Unknown
            && action.StartedAt.HasValue && action.FinishedAt.HasValue
            && action.FinishedAt.Value >= action.StartedAt.Value
            && action.ControlledRestartBootTimeUtc.HasValue).Take(2).ToList();
        if (requests.Count != 1)
            return false;

        var request = requests[0];
        return request.FinishedAt!.Value <= verification.StartedAt.Value
            && IsRebootVerified(request.ControlledRestartBootTimeUtc!.Value,
                request.StartedAt!.Value, verification.ControlledRestartBootTimeUtc.Value,
                verification.StartedAt.Value);
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
