using Vido.Core.Formatting;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="TimeFormatter"/> — shared time formatting utilities.
/// </summary>
public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(65, "01:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3661, "1:01:01")]
    [InlineData(36000, "10:00:00")]
    public void Format_ProducesExpectedOutput(int seconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(3661, "01:01:01")]
    [InlineData(3723, "01:02:03")]
    [InlineData(36000, "10:00:00")]
    public void FormatPadded_ProducesExpectedOutput(int seconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.FormatPadded(TimeSpan.FromSeconds(seconds)));
    }
}
