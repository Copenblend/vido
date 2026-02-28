using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a string geometry path to a <see cref="Geometry"/> object.
/// Returns <see cref="DependencyProperty.UnsetValue"/> for null or empty strings,
/// which prevents the WPF binding engine from attempting an invalid conversion.
/// </summary>
public sealed class StringToGeometryConverter : IValueConverter
{
    /// <summary>
    /// Parses a string path into a <see cref="Geometry"/> object.
    /// Returns <see cref="DependencyProperty.UnsetValue"/> if the string is null, empty, or unparseable.
    /// </summary>
    /// <param name="value">The binding source value, expected to be a geometry path string.</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="Geometry"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                return Geometry.Parse(s);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
        return DependencyProperty.UnsetValue;
    }
    
    /// <summary>
    /// Not supported. Returns <see cref="DependencyProperty.UnsetValue"/>.
    /// </summary>
    /// <param name="value">The binding target value (unused).</param>
    /// <param name="targetType">The source type to convert back to (unused).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
