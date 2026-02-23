using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a non-null, non-empty string to <see cref="Visibility.Visible"/>;
/// null or empty strings map to <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
