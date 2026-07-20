using System.Globalization;
using System.Windows.Data;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Infrastructure.Converters;

public class TaskCodeToActionDefinitionConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var taskCode =
            value as string
            ?? string.Empty;

        return CheckupTaskActionCatalog.GetDefinition(
            taskCode);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}