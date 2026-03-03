using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vido.Views.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to a <see cref="Visibility"/> value.
/// <c>true</c> maps to <see cref="Visibility.Visible"/>;
/// <c>false</c> maps to <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to a <see cref="Visibility"/> value.
    /// </summary>
    /// <param name="value">Input value expected to be a boolean.</param>
    /// <param name="targetType">The target type of the binding.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns><see cref="Visibility.Visible"/> when <c>true</c>; otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Converts a <see cref="Visibility"/> value back to a boolean.
    /// </summary>
    /// <param name="value">Input value expected to be a <see cref="Visibility"/>.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">Culture information for the conversion (unused).</param>
    /// <returns><c>true</c> only when value is <see cref="Visibility.Visible"/>.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
