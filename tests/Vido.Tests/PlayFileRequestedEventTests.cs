using Vido.Core.Events;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PlayFileRequestedEvent"/>.
/// </summary>
public sealed class PlayFileRequestedEventTests
{
    /// <summary>
    /// Verifies default value safety.
    /// </summary>
    [Fact]
    public void DefaultValue_FilePathIsEmpty()
    {
        var evt = default(PlayFileRequestedEvent);

        Assert.Equal(string.Empty, evt.FilePath);
    }

    /// <summary>
    /// Verifies assigned value is returned.
    /// </summary>
    [Fact]
    public void InitFilePath_AssignedCorrectly()
    {
        var evt = new PlayFileRequestedEvent { FilePath = @"C:\media\sample.mp4" };

        Assert.Equal(@"C:\media\sample.mp4", evt.FilePath);
    }

    /// <summary>
    /// Verifies null-coalescing behavior.
    /// </summary>
    [Fact]
    public void NullInit_FilePathFallsBackToEmpty()
    {
        var evt = new PlayFileRequestedEvent { FilePath = null! };

        Assert.Equal(string.Empty, evt.FilePath);
    }

    /// <summary>
    /// Verifies value equality with same path.
    /// </summary>
    [Fact]
    public void Equality_SamePath_ReturnsTrue()
    {
        var a = new PlayFileRequestedEvent { FilePath = "a.mp4" };
        var b = new PlayFileRequestedEvent { FilePath = "a.mp4" };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies value inequality with different paths.
    /// </summary>
    [Fact]
    public void Equality_DifferentPath_ReturnsFalse()
    {
        var a = new PlayFileRequestedEvent { FilePath = "a.mp4" };
        var b = new PlayFileRequestedEvent { FilePath = "b.mp4" };

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies with-expression copy behavior.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var original = new PlayFileRequestedEvent { FilePath = "a.mp4" };
        var copy = original with { FilePath = "b.mp4" };

        Assert.Equal("a.mp4", original.FilePath);
        Assert.Equal("b.mp4", copy.FilePath);
    }
}
