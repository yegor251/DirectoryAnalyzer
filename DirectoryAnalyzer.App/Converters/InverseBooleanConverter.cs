using System.Globalization;
using Avalonia.Data.Converters;

namespace DirectoryAnalyzer.App.Converters;

public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

