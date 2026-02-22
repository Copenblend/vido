using System.Globalization;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Converts "True"/"False" strings to/from boolean values.
/// Used for binding CheckBox.IsChecked to string-based boolean settings.
/// </summary>
public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "True" : "False";
    }
}
