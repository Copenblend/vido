using System.Buffers;

namespace Vido.Core.Playback;

/// <summary>
/// Represents a decoded video frame ready for display.
/// Contains raw BGRA32 pixel data that can be copied to a WriteableBitmap.
/// Implements <see cref="IDisposable"/> to return pooled buffers to
/// <see cref="ArrayPool{T}.Shared"/> after rendering.
/// </summary>
public sealed class FrameData : IDisposable
{
    private byte[]? _pixelData;
    private readonly bool _pooled;

    /// <summary>
    /// Raw BGRA32 pixel data. Throws if already disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the frame has already been disposed and the buffer returned to the pool.</exception>
    public byte[] PixelData => _pixelData ?? throw new ObjectDisposedException(nameof(FrameData));

    /// <summary>
    /// Number of valid bytes in <see cref="PixelData"/>.
    /// The backing array may be larger when rented from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public int PixelDataLength { get; }

    /// <summary>
    /// Frame width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Frame height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Number of bytes per row (may include padding).
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Presentation timestamp for this frame.
    /// </summary>
    public TimeSpan Pts { get; }

    /// <summary>
    /// Creates a new FrameData.
    /// </summary>
    /// <param name="pixelData">Raw pixel buffer (may be from ArrayPool).</param>
    /// <param name="pixelDataLength">Number of valid bytes in the buffer.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="stride">Bytes per row.</param>
    /// <param name="pts">Presentation timestamp.</param>
    /// <param name="pooled">True if the buffer was rented from ArrayPool and should be returned on Dispose.</param>
    public FrameData(byte[] pixelData, int pixelDataLength, int width, int height, int stride, TimeSpan pts, bool pooled)
    {
        _pixelData = pixelData;
        _pooled = pooled;
        PixelDataLength = pixelDataLength;
        Width = width;
        Height = height;
        Stride = stride;
        Pts = pts;
    }

    /// <summary>
    /// Returns the pixel buffer to the pool (if pooled). Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        var data = Interlocked.Exchange(ref _pixelData, null);
        if (data is not null && _pooled)
            ArrayPool<byte>.Shared.Return(data);
    }
}
