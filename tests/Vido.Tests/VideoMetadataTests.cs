using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the VideoMetadata model.
/// </summary>
public class VideoMetadataTests
{
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
}
