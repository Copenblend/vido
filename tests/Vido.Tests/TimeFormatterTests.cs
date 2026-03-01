using Vido.Core.Formatting;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="TimeFormatter"/> — shared time formatting utilities.
/// </summary>
public class TimeFormatterTests
{
    /// <summary>
    /// Verifies that Format produces expected output.
    /// </summary>
    /// <param name="seconds">The number of seconds.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(65, "01:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3661, "1:01:01")]
    [InlineData(9000, "2:30:00")]
    [InlineData(35999, "9:59:59")]
    [InlineData(36000, "10:00:00")]
    [InlineData(35999.9, "9:59:59")]
    [InlineData(-5, "00:05")]
    [InlineData(-3661, "01:01")]
    public void Format_ProducesExpectedOutput(double seconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }

    /// <summary>
    /// Verifies that Format handles floating-point seconds as expected (no sub-second digits).
    /// </summary>
    [Fact]
    public void Format_SubSecondInput_DoesNotShowFractionalDigits()
    {
        Assert.Equal("05:30", TimeFormatter.Format(TimeSpan.FromMinutes(5.5)));
    }

    /// <summary>
    /// Verifies that Format Padded produces expected output.
    /// </summary>
    /// <param name="seconds">The number of seconds.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3661, "01:01:01")]
    [InlineData(3723, "01:02:03")]
    [InlineData(9000, "02:30:00")]
    [InlineData(35999, "09:59:59")]
    [InlineData(36000, "10:00:00")]
    [InlineData(35999.9, "09:59:59")]
    [InlineData(-5, "00:05")]
    [InlineData(-3661, "01:01")]
    public void FormatPadded_ProducesExpectedOutput(double seconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.FormatPadded(TimeSpan.FromSeconds(seconds)));
    }

    /// <summary>
    /// Verifies that FormatPadded handles floating-point seconds as expected (no sub-second digits).
    /// </summary>
    [Fact]
    public void FormatPadded_SubSecondInput_DoesNotShowFractionalDigits()
    {
        Assert.Equal("05:30", TimeFormatter.FormatPadded(TimeSpan.FromMinutes(5.5)));
    }
}