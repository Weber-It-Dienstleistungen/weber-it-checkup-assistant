namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class CheckupTaskActionExecutionLease :
    IDisposable
{
    private readonly Action<Guid> _releaseAction;
    private int _isReleased;

    internal CheckupTaskActionExecutionLease(
        Guid executionId,
        string actionCode,
        string actionTitle,
        Action<Guid> releaseAction)
    {
        ExecutionId =
            executionId;

        ActionCode =
            actionCode;

        ActionTitle =
            actionTitle;

        _releaseAction =
            releaseAction;
    }

    public Guid ExecutionId { get; }

    public string ActionCode { get; }

    public string ActionTitle { get; }

    public bool IsReleased =>
        Volatile.Read(
            ref _isReleased)
        != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _isReleased,
                1)
            != 0)
        {
            return;
        }

        _releaseAction(
            ExecutionId);
    }
}