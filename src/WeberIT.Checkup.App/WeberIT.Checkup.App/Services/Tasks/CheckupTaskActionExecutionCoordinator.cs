using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class CheckupTaskActionExecutionCoordinator :
    ICheckupTaskActionExecutionCoordinator
{
    private readonly object _synchronizationLock =
        new();

    private Guid? _activeExecutionId;

    private string _activeActionCode =
        string.Empty;

    private string _activeActionTitle =
        string.Empty;

    public event EventHandler? StateChanged;

    public bool IsExecutionRunning
    {
        get
        {
            lock (_synchronizationLock)
            {
                return
                    _activeExecutionId.HasValue;
            }
        }
    }

    public string ActiveActionCode
    {
        get
        {
            lock (_synchronizationLock)
            {
                return
                    _activeActionCode;
            }
        }
    }

    public string ActiveActionTitle
    {
        get
        {
            lock (_synchronizationLock)
            {
                return
                    _activeActionTitle;
            }
        }
    }

    public CheckupTaskActionExecutionLease? TryBeginExecution(
        string actionCode,
        string actionTitle)
    {
        if (string.IsNullOrWhiteSpace(
                actionCode))
        {
            throw new ArgumentException(
                "Für eine Systemaktion ist ein stabiler "
                + "Aktionscode erforderlich.",
                nameof(actionCode));
        }

        if (string.IsNullOrWhiteSpace(
                actionTitle))
        {
            throw new ArgumentException(
                "Für eine Systemaktion ist eine "
                + "verständliche Bezeichnung erforderlich.",
                nameof(actionTitle));
        }

        CheckupTaskActionExecutionLease lease;

        lock (_synchronizationLock)
        {
            if (_activeExecutionId.HasValue)
            {
                return null;
            }

            var executionId =
                Guid.NewGuid();

            _activeExecutionId =
                executionId;

            _activeActionCode =
                actionCode.Trim();

            _activeActionTitle =
                actionTitle.Trim();

            lease =
                new CheckupTaskActionExecutionLease(
                    executionId,
                    _activeActionCode,
                    _activeActionTitle,
                    ReleaseExecution);
        }

        NotifyStateChanged();

        return lease;
    }

    private void ReleaseExecution(
        Guid executionId)
    {
        var wasReleased =
            false;

        lock (_synchronizationLock)
        {
            if (_activeExecutionId
                != executionId)
            {
                return;
            }

            _activeExecutionId =
                null;

            _activeActionCode =
                string.Empty;

            _activeActionTitle =
                string.Empty;

            wasReleased =
                true;
        }

        if (wasReleased)
        {
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        var handlers =
            StateChanged?
                .GetInvocationList()
                .Cast<EventHandler>()
                .ToList()
            ?? new List<EventHandler>();

        foreach (var handler in handlers)
        {
            try
            {
                handler(
                    this,
                    EventArgs.Empty);
            }
            catch
            {
                // Eine fehlerhafte Statusanzeige darf weder
                // die Sperre noch ihre Freigabe beschädigen.
            }
        }
    }
}