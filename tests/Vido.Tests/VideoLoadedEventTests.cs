using Vido.Core.Events;
using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="VideoLoadedEvent"/>.
/// </summary>
public sealed class VideoLoadedEventTests
{
    /// <summary>
    /// Verifies default value safety.
    /// </summary>
    [Fact]
    public void DefaultValue_UsesSafeFallbacks()
    {
        var evt = default(VideoLoadedEvent);

        Assert.Equal(string.Empty, evt.FilePath);
        Assert.Equal(string.Empty, evt.Metadata.FilePath);
        Assert.Equal(string.Empty, evt.Metadata.FileName);
    }

    /// <summary>
    /// Verifies assigned values are returned.
    /// </summary>
    [Fact]
    public void InitProperties_AssignedCorrectly()
    {
        var metadata = new VideoMetadata
        {
            FilePath = @"C:\media\sample.mp4",
            FileName = "sample.mp4"
        };

        var evt = new VideoLoadedEvent
        {
            FilePath = @"C:\media\sample.mp4",
            Metadata = metadata
        };

        Assert.Equal(@"C:\media\sample.mp4", evt.FilePath);
        Assert.Same(metadata, evt.Metadata);
    }

    /// <summary>
    /// Verifies null-coalescing behavior.
    /// </summary>
    [Fact]
    public void NullInit_UsesFallbacks()
    {
        var evt = new VideoLoadedEvent
        {
            FilePath = null!,
            Metadata = null!
        };

        Assert.Equal(string.Empty, evt.FilePath);
        Assert.Equal(string.Empty, evt.Metadata.FilePath);
        Assert.Equal(string.Empty, evt.Metadata.FileName);
    }

    /// <summary>
    /// Verifies value equality with same values.
    /// </summary>
    [Fact]
    public void Equality_SameValues_ReturnsTrue()
    {
        var metadata = new VideoMetadata
        {
            FilePath = "video.mp4",
            FileName = "video.mp4"
        };

        var a = new VideoLoadedEvent { FilePath = "video.mp4", Metadata = metadata };
        var b = new VideoLoadedEvent { FilePath = "video.mp4", Metadata = metadata };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies with-expression copy behavior.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var metadataA = new VideoMetadata { FilePath = "a.mp4", FileName = "a.mp4" };
        var metadataB = new VideoMetadata { FilePath = "b.mp4", FileName = "b.mp4" };

        var original = new VideoLoadedEvent { FilePath = "a.mp4", Metadata = metadataA };
        var copy = original with { FilePath = "b.mp4", Metadata = metadataB };

        Assert.Equal("a.mp4", original.FilePath);
        Assert.Same(metadataA, original.Metadata);
        Assert.Equal("b.mp4", copy.FilePath);
        Assert.Same(metadataB, copy.Metadata);
    }
}
