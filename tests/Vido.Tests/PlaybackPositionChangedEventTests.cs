using Vido.Core.Events;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PlaybackPositionChangedEvent"/>.
/// </summary>
public sealed class PlaybackPositionChangedEventTests
{
    /// <summary>
    /// Verifies that default values are safe.
    /// </summary>
    [Fact]
    public void DefaultValues_AreSafe()
    {
        var evt = default(PlaybackPositionChangedEvent);

        Assert.Equal(TimeSpan.Zero, evt.Position);
        Assert.Equal(TimeSpan.Zero, evt.Duration);
    }

    /// <summary>
    /// Verifies that init properties are assigned correctly.
    /// </summary>
    [Fact]
    public void InitProperties_AssignedCorrectly()
    {
        var evt = new PlaybackPositionChangedEvent
        {
            Position = TimeSpan.FromSeconds(10),
            Duration = TimeSpan.FromMinutes(2)
        };

        Assert.Equal(TimeSpan.FromSeconds(10), evt.Position);
        Assert.Equal(TimeSpan.FromMinutes(2), evt.Duration);
    }

    /// <summary>
    /// Verifies value equality with same values.
    /// </summary>
    [Fact]
    public void Equality_SameValues_ReturnsTrue()
    {
        var a = new PlaybackPositionChangedEvent { Position = TimeSpan.FromSeconds(1), Duration = TimeSpan.FromSeconds(2) };
        var b = new PlaybackPositionChangedEvent { Position = TimeSpan.FromSeconds(1), Duration = TimeSpan.FromSeconds(2) };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies value inequality with different values.
    /// </summary>
    [Fact]
    public void Equality_DifferentValues_ReturnsFalse()
    {
        var a = new PlaybackPositionChangedEvent { Position = TimeSpan.FromSeconds(1), Duration = TimeSpan.FromSeconds(2) };
        var b = new PlaybackPositionChangedEvent { Position = TimeSpan.FromSeconds(3), Duration = TimeSpan.FromSeconds(2) };

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies with-expression copy behavior.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var original = new PlaybackPositionChangedEvent { Position = TimeSpan.FromSeconds(5), Duration = TimeSpan.FromSeconds(20) };
        var copy = original with { Position = TimeSpan.FromSeconds(7) };

        Assert.Equal(TimeSpan.FromSeconds(5), original.Position);
        Assert.Equal(TimeSpan.FromSeconds(7), copy.Position);
        Assert.Equal(original.Duration, copy.Duration);
    }
}
