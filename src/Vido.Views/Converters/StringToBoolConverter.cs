using System.Globalization;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Converts "True"/"False" strings to/from boolean values.
/// Used for binding CheckBox.IsChecked to string-based boolean settings.
/// </summary>
public sealed class StringToBoolConverter : IValueConverter
{
    /// <summary>
    /// Returns <c>true</c> if the string value equals "True" (case-insensitive); <c>false</c> otherwise.
    /// </summary>
    /// <param name="value">The binding source value, expected to be a string "True" or "False".</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="bool"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Converts a boolean value back to the string "True" or "False".
    /// </summary>
    /// <param name="value">The binding target boolean value.</param>
    /// <param name="targetType">The source type to convert back to (expected: <see cref="string"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "True" : "False";
    }
}
