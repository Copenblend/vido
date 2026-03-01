namespace Vido.Core.Formatting;

/// <summary>
/// Shared time-formatting utilities used across ViewModels.
/// Uses stack-allocated buffers to minimize intermediate allocations.
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// Formats a <see cref="TimeSpan"/> for transport display (position/duration).
    /// Uses "mm:ss" or "h:mm:ss" (no leading zero on hours).
    /// </summary>
    /// <param name="ts">The time value to format.</param>
    /// <returns>The formatted time string for transport display.</returns>
    public static string Format(TimeSpan ts)
    {
        ReadOnlySpan<char> format = ts.TotalHours >= 1
            ? @"h\:mm\:ss"
            : @"mm\:ss";

        Span<char> buffer = stackalloc char[16];
        if (ts.TryFormat(buffer, out var written, format))
            return new string(buffer[..written]);

        return ts.ToString(format.ToString());
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> for metadata display (status bar, details panel).
    /// Uses "mm:ss" or "hh:mm:ss" (leading zero on hours).
    /// </summary>
    /// <param name="ts">The time value to format.</param>
    /// <returns>The formatted time string for metadata display.</returns>
    public static string FormatPadded(TimeSpan ts)
    {
        ReadOnlySpan<char> format = ts.TotalHours >= 1
            ? @"hh\:mm\:ss"
            : @"mm\:ss";

        Span<char> buffer = stackalloc char[16];
        if (ts.TryFormat(buffer, out var written, format))
            return new string(buffer[..written]);

        return ts.ToString(format.ToString());
    }
}
