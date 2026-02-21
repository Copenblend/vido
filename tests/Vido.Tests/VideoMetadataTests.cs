using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the VideoMetadata model.
/// </summary>
public class VideoMetadataTests
{
    [Fact]
    public void RequiredProperties_AreSet()
    {
        var metadata = new VideoMetadata
        {
            FilePath = @"C:\videos\test.mp4",
            FileName = "test.mp4"
        };

        Assert.Equal(@"C:\videos\test.mp4", metadata.FilePath);
        Assert.Equal("test.mp4", metadata.FileName);
    }

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

    [Fact]
    public void AllProperties_CanBeInitialized()
    {
        var duration = TimeSpan.FromMinutes(5);

        var metadata = new VideoMetadata
        {
            FilePath = @"C:\videos\movie.mkv",
            FileName = "movie.mkv",
            FileSize = 1_073_741_824,
            Duration = duration,
            Width = 1920,
            Height = 1080,
            VideoCodec = "h264",
            AudioCodec = "aac",
            FrameRate = 29.97,
            Bitrate = 5_000_000,
            ContainerFormat = "matroska,webm",
            AudioChannels = 2,
            AudioSampleRate = 48000
        };

        Assert.Equal(@"C:\videos\movie.mkv", metadata.FilePath);
        Assert.Equal("movie.mkv", metadata.FileName);
        Assert.Equal(1_073_741_824, metadata.FileSize);
        Assert.Equal(duration, metadata.Duration);
        Assert.Equal(1920, metadata.Width);
        Assert.Equal(1080, metadata.Height);
        Assert.Equal("h264", metadata.VideoCodec);
        Assert.Equal("aac", metadata.AudioCodec);
        Assert.Equal(29.97, metadata.FrameRate);
        Assert.Equal(5_000_000, metadata.Bitrate);
        Assert.Equal("matroska,webm", metadata.ContainerFormat);
        Assert.Equal(2, metadata.AudioChannels);
        Assert.Equal(48000, metadata.AudioSampleRate);
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
