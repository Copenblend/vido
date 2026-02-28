using System.Globalization;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Returns <c>true</c> when the value is non-null and (for strings) non-empty; <c>false</c> otherwise.
/// Useful for toggling visibility based on whether a property has been set.
/// </summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    /// <summary>
    /// Returns <c>true</c> if the value is non-null (and non-empty for strings); <c>false</c> otherwise.
    /// </summary>
    /// <param name="value">The binding source value to evaluate.</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="bool"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true,
        };
    }
    
    /// <summary>
    /// Not supported. This converter is one-way only.
    /// </summary>
    /// <param name="value">The binding target value (unused).</param>
    /// <param name="targetType">The source type to convert back to (unused).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <exception cref="NotSupportedException">Always thrown; reverse conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
