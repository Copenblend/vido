using System.Globalization;
using System.Windows.Data;
using Vido.Core.Models.Osr2Plus;

namespace Vido.Views.Converters;

/// <summary>
/// Converts an <see cref="AxisFillMode"/> enum value to a user-friendly display string.
/// </summary>
public sealed class FillModeDisplayConverter : IValueConverter
{
    /// <summary>
    /// Converts an <see cref="AxisFillMode"/> to its display string representation.
    /// </summary>
    /// <param name="value">The <see cref="AxisFillMode"/> value to convert.</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="string"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>A friendly display name for the fill mode.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AxisFillMode mode)
            return string.Empty;

        return mode switch
        {
            AxisFillMode.SawtoothReverse => "Reverse Saw",
            AxisFillMode.EaseInOut => "Ease In/Out",
            _ => mode.ToString(),
        };
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
