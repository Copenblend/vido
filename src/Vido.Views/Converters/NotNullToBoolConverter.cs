using System.Globalization;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Returns <c>true</c> when the value is non-null and (for strings) non-empty; <c>false</c> otherwise.
/// Useful for toggling visibility based on whether a property has been set.
/// </summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
