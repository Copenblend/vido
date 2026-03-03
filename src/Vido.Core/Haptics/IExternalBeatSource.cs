using SkiaSharp;

namespace Vido.Core.Haptics;

/// <summary>
/// A beat source that can appear as an option in the BeatBar ComboBox.
/// Implementations are registered via the <c>IEventBus</c> using <see cref="ExternalBeatSourceRegistration"/>
/// to contribute custom BeatBar modes with their own labels, beat rendering, and indicator rendering.
/// The haptic transport delegates rendering to the registered source — it has no knowledge
/// of the specific feature providing the beats.
/// </summary>
public interface IExternalBeatSource
{
    /// <summary>Unique ID for this beat source (e.g., "com.vido.pulse").</summary>
    string Id { get; }

    /// <summary>Display label for the BeatBar ComboBox dropdown (e.g., "Pulse").</summary>
    string DisplayName { get; }

    /// <summary>Whether this source is currently active and ready to provide beats.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Whether the built-in BeatBar modes (On Peak, On Valley) should be hidden
    /// when this source is active. Allows features to fully replace BeatBar behavior.
    /// </summary>
    bool HidesBuiltInModes { get; }

    /// <summary>
    /// Called by the BeatBar SkiaSharp overlay to render a single beat marker.
    /// The feature provides its own shapes/colors (the transport just calls this).
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="centerX">X center of where the beat should be drawn.</param>
    /// <param name="centerY">Y center of where the beat should be drawn.</param>
    /// <param name="size">Suggested size (diameter/width) for the beat marker.</param>
    /// <param name="progress">0.0–1.0 animation progress for glow/pulse effects.</param>
    void RenderBeat(SKCanvas canvas, float centerX, float centerY, float size, float progress);

    /// <summary>
    /// Called by the BeatBar SkiaSharp overlay to render the indicator (the ring/shape
    /// that active beats pass through). The feature provides its own shape/color.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="centerX">X center of the indicator.</param>
    /// <param name="centerY">Y center of the indicator.</param>
    /// <param name="size">Suggested size for the indicator.</param>
    void RenderIndicator(SKCanvas canvas, float centerX, float centerY, float size);
}
