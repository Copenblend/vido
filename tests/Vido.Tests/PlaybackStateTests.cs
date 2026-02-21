using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the PlaybackState enum.
/// </summary>
public class PlaybackStateTests
{
    [Fact]
    public void PlaybackState_HasExpectedValues()
    {
        Assert.Equal(0, (int)PlaybackState.None);
        Assert.Equal(1, (int)PlaybackState.Playing);
        Assert.Equal(2, (int)PlaybackState.Paused);
        Assert.Equal(3, (int)PlaybackState.Stopped);
    }

    [Fact]
    public void PlaybackState_HasFourMembers()
    {
        var values = Enum.GetValues<PlaybackState>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(PlaybackState.None, "None")]
    [InlineData(PlaybackState.Playing, "Playing")]
    [InlineData(PlaybackState.Paused, "Paused")]
    [InlineData(PlaybackState.Stopped, "Stopped")]
    public void PlaybackState_ToString_ReturnsExpectedName(PlaybackState state, string expected)
    {
        Assert.Equal(expected, state.ToString());
    }
}
