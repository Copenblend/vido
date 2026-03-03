using System.Globalization;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a double fraction (0.0–1.0) to a percentage (0–100) and back.
/// Useful for progress bar bindings where the source value is normalised.
/// </summary>
public sealed class FractionToPercentConverter : IValueConverter
{
    /// <summary>
    /// Converts a fraction from 0.0–1.0 to a percentage from 0–100.
    /// </summary>
    /// <param name="value">Input fraction.</param>
    /// <param name="targetType">The target type of the binding.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>Percentage value as double.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? d * 100.0 : 0.0;

    /// <summary>
    /// Converts a percentage from 0–100 back to a fraction from 0.0–1.0.
    /// </summary>
    /// <param name="value">Input percentage.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>Fraction value as double.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? d / 100.0 : 0.0;
}
