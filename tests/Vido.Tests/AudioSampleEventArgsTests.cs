using Vido.Core.Playback;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the <see cref="AudioSampleEventArgs"/> data class.
/// </summary>
public class AudioSampleEventArgsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var args = new AudioSampleEventArgs
        {
            Buffer = new ReadOnlyMemory<byte>(data, 0, 8),
            SampleCount = 1,
            SampleRate = 48000,
            Channels = 2
        };

        Assert.Equal(8, args.Buffer.Length);
        Assert.Equal(1, args.SampleCount);
        Assert.Equal(48000, args.SampleRate);
        Assert.Equal(2, args.Channels);
    }

    [Fact]
    public void Buffer_IsZeroCopySlice_SharesUnderlyingArray()
    {
        // Verify that ReadOnlyMemory wraps the original array without copying
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var args = new AudioSampleEventArgs
        {
            Buffer = new ReadOnlyMemory<byte>(data),
            SampleCount = 1,
            SampleRate = 44100,
            Channels = 1
        };

        // The buffer should reference the same data
        var span = args.Buffer.Span;
        Assert.Equal(0xDE, span[0]);
        Assert.Equal(0xAD, span[1]);
        Assert.Equal(0xBE, span[2]);
        Assert.Equal(0xEF, span[3]);
    }

    [Fact]
    public void Buffer_CanBeSlicedWithOffset()
    {
        var data = new byte[32];
        data[8] = 0xFF;

        var args = new AudioSampleEventArgs
        {
            Buffer = new ReadOnlyMemory<byte>(data, 8, 16),
            SampleCount = 2,
            SampleRate = 48000,
            Channels = 2
        };

        Assert.Equal(16, args.Buffer.Length);
        Assert.Equal(0xFF, args.Buffer.Span[0]);
    }

    [Fact]
    public void Buffer_EmptyIsValid()
    {
        var args = new AudioSampleEventArgs
        {
            Buffer = ReadOnlyMemory<byte>.Empty,
            SampleCount = 0,
            SampleRate = 48000,
            Channels = 2
        };

        Assert.Equal(0, args.Buffer.Length);
        Assert.Equal(0, args.SampleCount);
    }

    [Fact]
    public void SampleCount_ReflectsPerChannelCount()
    {
        // 4 samples × 2 channels × 4 bytes/float = 32 bytes
        var data = new byte[32];
        var args = new AudioSampleEventArgs
        {
            Buffer = new ReadOnlyMemory<byte>(data),
            SampleCount = 4,
            SampleRate = 48000,
            Channels = 2
        };

        var expectedBytes = args.SampleCount * args.Channels * sizeof(float);
        Assert.Equal(expectedBytes, args.Buffer.Length);
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void SampleRate_AcceptsCommonRates(int sampleRate)
    {
        var args = new AudioSampleEventArgs
        {
            Buffer = ReadOnlyMemory<byte>.Empty,
            SampleCount = 0,
            SampleRate = sampleRate,
            Channels = 2
        };

        Assert.Equal(sampleRate, args.SampleRate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void Channels_AcceptsVariousLayouts(int channels)
    {
        var args = new AudioSampleEventArgs
        {
            Buffer = ReadOnlyMemory<byte>.Empty,
            SampleCount = 0,
            SampleRate = 48000,
            Channels = channels
        };

        Assert.Equal(channels, args.Channels);
    }
}
