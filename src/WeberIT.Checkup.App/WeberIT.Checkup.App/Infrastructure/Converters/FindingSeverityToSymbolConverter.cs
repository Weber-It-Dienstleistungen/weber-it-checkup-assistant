using System;
using System.Globalization;
using System.Windows.Data;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Infrastructure.Converters;

public class FindingSeverityToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FindingSeverity severity)
        {
            return "ℹ";
        }

        return severity switch
        {
            FindingSeverity.Information => "ℹ",
            FindingSeverity.Recommendation => "💡",
            FindingSeverity.Warning => "⚠",
            FindingSeverity.Critical => "❌",
            _ => "ℹ"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}