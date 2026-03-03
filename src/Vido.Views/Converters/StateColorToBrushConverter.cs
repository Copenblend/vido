using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a state color key string ("Green", "Yellow", "Grey", "Red") to a
/// frozen <see cref="SolidColorBrush"/>. Defaults to grey for unrecognised keys.
/// </summary>
public sealed class StateColorToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x4E, 0xC9, 0xB0));
    private static readonly SolidColorBrush Yellow = new(Color.FromRgb(0xDC, 0xDC, 0xAA));
    private static readonly SolidColorBrush Grey = new(Color.FromRgb(0x60, 0x60, 0x60));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xC4, 0x2B, 0x1C));

    static StateColorToBrushConverter()
    {
        Green.Freeze();
        Yellow.Freeze();
        Grey.Freeze();
        Red.Freeze();
    }

    /// <summary>
    /// Converts a state color key to a frozen <see cref="SolidColorBrush"/> instance.
    /// </summary>
    /// <param name="value">State key string (e.g. "Green", "Yellow", "Red").</param>
    /// <param name="targetType">The target type of the binding.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns>A brush matching the supplied key; defaults to grey.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Green" => Green,
            "Yellow" => Yellow,
            "Red" => Red,
            _ => Grey
        };

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
