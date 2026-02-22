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

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
