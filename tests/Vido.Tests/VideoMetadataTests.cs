using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the VideoMetadata model.
/// </summary>
public sealed class VideoMetadataTests
{
    /// <summary>
    /// Verifies that Empty returns the same reference and has expected defaults.
    /// </summary>
    [Fact]
    public void Empty_ReturnsSameReferenceWithExpectedDefaults()
    {
        var first = VideoMetadata.Empty;
        var second = VideoMetadata.Empty;

        Assert.Same(first, second);
        Assert.Equal(string.Empty, first.FilePath);
        Assert.Equal(string.Empty, first.FileName);
        Assert.Equal(0, first.Width);
        Assert.Equal(0, first.Height);
    }

    /// <summary>
    /// Verifies that Optional Properties default to zero or null.
    /// </summary>
    [Fact]
    public void OptionalProperties_DefaultToZeroOrNull()
    {
        var metadata = new VideoMetadata
        {
            FilePath = @"C:\test.mp4",
            FileName = "test.mp4"
        };

        Assert.Equal(0, metadata.FileSize);
        Assert.Equal(TimeSpan.Zero, metadata.Duration);
        Assert.Equal(0, metadata.Width);
        Assert.Equal(0, metadata.Height);
        Assert.Null(metadata.VideoCodec);
        Assert.Null(metadata.AudioCodec);
        Assert.Equal(0, metadata.FrameRate);
        Assert.Equal(0, metadata.Bitrate);
        Assert.Null(metadata.ContainerFormat);
        Assert.Equal(0, metadata.AudioChannels);
        Assert.Equal(0, metadata.AudioSampleRate);
    }

    /// <summary>
    /// Verifies that Resolution returns formatted string.
    /// </summary>
    /// <param name="width">The video width in pixels.</param>
    /// <param name="height">The video height in pixels.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(1920, 1080, "1920x1080")]
    [InlineData(3840, 2160, "3840x2160")]
    [InlineData(1280, 720, "1280x720")]
    [InlineData(0, 0, "0x0")]
    public void Resolution_ReturnsFormattedString(int width, int height, string expected)
    {
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = width,
            Height = height
        };

        Assert.Equal(expected, metadata.Resolution);
    }

    /// <summary>
    /// Verifies that Resolution is cached after first access.
    /// </summary>
    [Fact]
    public void Resolution_CachesComputedString()
    {
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            FileName = "test.mp4",
            Width = 1920,
            Height = 1080
        };

        var first = metadata.Resolution;
        var second = metadata.Resolution;

        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies that two instances with identical values are equal.
    /// </summary>
    [Fact]
    public void Equality_SameValues_ReturnsTrue()
    {
        var first = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1920,
            Height = 1080,
            Duration = TimeSpan.FromMinutes(3)
        };

        var second = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1920,
            Height = 1080,
            Duration = TimeSpan.FromMinutes(3)
        };

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Verifies that two instances with different values are not equal.
    /// </summary>
    [Fact]
    public void Equality_DifferentValues_ReturnsFalse()
    {
        var first = new VideoMetadata
        {
            FilePath = "video-a.mp4",
            FileName = "video-a.mp4"
        };

        var second = new VideoMetadata
        {
            FilePath = "video-b.mp4",
            FileName = "video-b.mp4"
        };

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Verifies that equal instances produce the same hash code.
    /// </summary>
    [Fact]
    public void GetHashCode_EqualInstances_ReturnSameHashCode()
    {
        var first = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1280,
            Height = 720
        };

        var second = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1280,
            Height = 720
        };

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Verifies that with-expression creates a copy with updated values.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var original = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1280,
            Height = 720
        };

        var copy = original with { Width = 1920, Height = 1080 };

        Assert.Equal(1280, original.Width);
        Assert.Equal(720, original.Height);
        Assert.Equal(1920, copy.Width);
        Assert.Equal(1080, copy.Height);
        Assert.Equal(original.FilePath, copy.FilePath);
    }

    /// <summary>
    /// Verifies that ToString contains key property values.
    /// </summary>
    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        var metadata = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4",
            Width = 1920,
            Height = 1080
        };

        var text = metadata.ToString();

        Assert.Contains("video.mp4", text);
        Assert.Contains("1920", text);
        Assert.Contains("1080", text);
    }
}