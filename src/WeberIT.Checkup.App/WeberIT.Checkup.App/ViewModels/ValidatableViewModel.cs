using System.Collections;
using System.ComponentModel;

namespace WeberIT.Checkup.App.ViewModels;

public abstract class ValidatableViewModel : BaseViewModel, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errorsByPropertyName = new();

    public bool HasErrors => _errorsByPropertyName.Any();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return _errorsByPropertyName.SelectMany(error => error.Value);
        }

        return _errorsByPropertyName.TryGetValue(propertyName, out var errors)
            ? errors
            : Enumerable.Empty<string>();
    }

    protected void ValidateProperty(string propertyName, params string[] errors)
    {
        var validErrors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error));

        SetErrors(propertyName, validErrors);
    }

    protected void SetErrors(string propertyName, IEnumerable<string> errors)
    {
        var errorList = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct()
            .ToList();

        if (errorList.Count == 0)
        {
            ClearErrors(propertyName);
            return;
        }

        _errorsByPropertyName[propertyName] = errorList;
        RaiseErrorsChanged(propertyName);
        OnPropertyChanged(nameof(HasErrors));
    }

    protected void ClearErrors(string propertyName)
    {
        if (_errorsByPropertyName.Remove(propertyName))
        {
            RaiseErrorsChanged(propertyName);
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    protected void ClearAllErrors()
    {
        var propertyNames = _errorsByPropertyName.Keys.ToList();

        _errorsByPropertyName.Clear();

        foreach (var propertyName in propertyNames)
        {
            RaiseErrorsChanged(propertyName);
        }

        OnPropertyChanged(nameof(HasErrors));
    }

    protected void RaiseErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}