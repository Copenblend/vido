using System.Globalization;
using System.Windows.Data;
using Vido.Core.Models.Osr2Plus;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a <see cref="BeatBarMode"/> to a user-friendly display string.
/// Built-in modes are mapped to specific labels; external modes use their
/// <see cref="BeatBarMode.DisplayName"/> property.
/// </summary>
public sealed class BeatBarModeDisplayConverter : IValueConverter
{
    /// <summary>
    /// Converts a <see cref="BeatBarMode"/> to its display string representation.
    /// </summary>
    /// <param name="value">The <see cref="BeatBarMode"/> value to convert.</param>
    /// <param name="targetType">The target type of the binding (expected: <see cref="string"/>).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>A display string for the beat bar mode.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BeatBarMode mode)
            return string.Empty;

        if (mode == BeatBarMode.Off) return "No Beat Bar";
        if (mode == BeatBarMode.OnPeak) return "On Peak";
        if (mode == BeatBarMode.OnValley) return "On Valley";

        // External mode — use the display name from the registered source.
        return mode.DisplayName;
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
