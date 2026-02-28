namespace Vido.Core.Formatting;

/// <summary>
/// Shared time-formatting utilities used across ViewModels.
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// Formats a <see cref="TimeSpan"/> for transport display (position/duration).
    /// Uses "mm:ss" or "h:mm:ss" (no leading zero on hours).
    /// </summary>
    /// <param name="ts">The time value to format.</param>
    public static string Format(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> for metadata display (status bar, details panel).
    /// Uses "mm:ss" or "hh:mm:ss" (leading zero on hours).
    /// </summary>
    /// <param name="ts">The time value to format.</param>
    public static string FormatPadded(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }
}
