using System.ComponentModel;

namespace WeberIT.Checkup.App.ViewModels;

public abstract class ValidatableViewModel : BaseViewModel, INotifyDataErrorInfo
{
    public bool HasErrors => false;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        return Enumerable.Empty<string>();
    }

    protected void RaiseErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}