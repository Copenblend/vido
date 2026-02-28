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
    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> for non-null, non-empty strings;
    /// <see cref="Visibility.Collapsed"/> otherwise.
    /// </summary>
    /// <param name="value">The binding source value, expected to be a string or null.</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="Visibility"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
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
