using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#007ACC") to a frozen <see cref="SolidColorBrush"/>.
/// Returns <see cref="Brushes.Gray"/> if the value is not a valid hex string.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts a hex color string to a frozen <see cref="SolidColorBrush"/>.
    /// </summary>
    /// <param name="value">A hex color string (e.g. "#007ACC").</param>
    /// <param name="targetType">The target type of the binding.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>A frozen <see cref="SolidColorBrush"/> matching the hex value, or <see cref="Brushes.Gray"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                // Fall through to default.
            }
        }

        return Brushes.Gray;
    }

    /// <summary>
    /// Not supported. This converter is one-way only.
    /// </summary>
    /// <param name="value">The binding target value.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="culture">Culture information for the conversion.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
