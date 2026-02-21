namespace Vido.Core.Playback;

/// <summary>
/// Represents a decoded video frame ready for display.
/// Contains raw BGRA32 pixel data that can be copied to a WriteableBitmap.
/// </summary>
public sealed class FrameData
{
    /// <summary>Raw BGRA32 pixel data.</summary>
    public required byte[] PixelData { get; init; }

    /// <summary>Frame width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Frame height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Number of bytes per row (may include padding).</summary>
    public required int Stride { get; init; }

    /// <summary>Presentation timestamp for this frame.</summary>
    public TimeSpan Pts { get; init; }
}
