using Vido.Core.Events;
using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PlaybackStateChangedEvent"/>.
/// </summary>
public sealed class PlaybackStateChangedEventTests
{
    /// <summary>
    /// Verifies that default state is safe.
    /// </summary>
    [Fact]
    public void DefaultState_IsNone()
    {
        var evt = default(PlaybackStateChangedEvent);

        Assert.Equal(PlaybackState.None, evt.State);
    }

    /// <summary>
    /// Verifies init assignment for each supported enum state.
    /// </summary>
    [Theory]
    [InlineData(PlaybackState.None)]
    [InlineData(PlaybackState.Playing)]
    [InlineData(PlaybackState.Paused)]
    [InlineData(PlaybackState.Stopped)]
    public void InitState_AssignedCorrectly(PlaybackState state)
    {
        var evt = new PlaybackStateChangedEvent { State = state };

        Assert.Equal(state, evt.State);
    }

    /// <summary>
    /// Verifies equality for same value.
    /// </summary>
    [Fact]
    public void Equality_SameState_ReturnsTrue()
    {
        var a = new PlaybackStateChangedEvent { State = PlaybackState.Paused };
        var b = new PlaybackStateChangedEvent { State = PlaybackState.Paused };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies inequality for different values.
    /// </summary>
    [Fact]
    public void Equality_DifferentState_ReturnsFalse()
    {
        var a = new PlaybackStateChangedEvent { State = PlaybackState.Playing };
        var b = new PlaybackStateChangedEvent { State = PlaybackState.Stopped };

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Verifies with-expression copy behavior.
    /// </summary>
    [Fact]
    public void WithExpression_CopiesAndMutates()
    {
        var original = new PlaybackStateChangedEvent { State = PlaybackState.Playing };
        var copy = original with { State = PlaybackState.Paused };

        Assert.Equal(PlaybackState.Playing, original.State);
        Assert.Equal(PlaybackState.Paused, copy.State);
    }
}
