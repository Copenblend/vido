using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the FrameData model.
/// </summary>
public class FrameDataTests
{
    [Fact]
    public void RequiredProperties_AreSet()
    {
        var pixels = new byte[] { 0, 0, 255, 255 };

        var frame = new FrameData
        {
            PixelData = pixels,
            Width = 1,
            Height = 1,
            Stride = 4
        };

        Assert.Same(pixels, frame.PixelData);
        Assert.Equal(1, frame.Width);
        Assert.Equal(1, frame.Height);
        Assert.Equal(4, frame.Stride);
    }

    [Fact]
    public void Pts_DefaultsToZero()
    {
        var frame = new FrameData
        {
            PixelData = new byte[4],
            Width = 1,
            Height = 1,
            Stride = 4
        };

        Assert.Equal(TimeSpan.Zero, frame.Pts);
    }

    [Fact]
    public void Pts_CanBeSet()
    {
        var pts = TimeSpan.FromSeconds(5.5);

        var frame = new FrameData
        {
            PixelData = new byte[4],
            Width = 1,
            Height = 1,
            Stride = 4,
            Pts = pts
        };

        Assert.Equal(pts, frame.Pts);
    }
}
