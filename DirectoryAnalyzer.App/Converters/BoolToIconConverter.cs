using System.Globalization;
using Avalonia.Data.Converters;

namespace DirectoryAnalyzer.App.Converters;

public class BoolToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDirectory)
        {
            return isDirectory ? "📁" : "📄";
        }

        return "📄";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

