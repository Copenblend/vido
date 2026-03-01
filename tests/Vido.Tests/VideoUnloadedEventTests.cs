using Vido.Core.Events;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="VideoUnloadedEvent"/>.
/// </summary>
public sealed class VideoUnloadedEventTests
{
    /// <summary>
    /// Verifies default value is safe and usable.
    /// </summary>
    [Fact]
    public void DefaultValue_IsUsable()
    {
        var evt = default(VideoUnloadedEvent);

        Assert.IsType<VideoUnloadedEvent>(evt);
    }

    /// <summary>
    /// Verifies all instances are equal for zero-field record struct.
    /// </summary>
    [Fact]
    public void Equality_AllInstancesAreEqual()
    {
        var a = new VideoUnloadedEvent();
        var b = default(VideoUnloadedEvent);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Verifies hash code is stable for zero-field record struct.
    /// </summary>
    [Fact]
    public void GetHashCode_InstancesMatch()
    {
        var a = new VideoUnloadedEvent();
        var b = default(VideoUnloadedEvent);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// Verifies with-expression is supported and yields an equal instance.
    /// </summary>
    [Fact]
    public void WithExpression_ProducesEqualInstance()
    {
        var original = new VideoUnloadedEvent();
        var copy = original with { };

        Assert.Equal(original, copy);
    }
}
