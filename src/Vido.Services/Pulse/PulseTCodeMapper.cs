using Vido.Core.Models.Pulse;

namespace Vido.Services.Pulse;

/// <summary>
/// Maps pre-analyzed beats + live amplitude to L0 axis positions (0–100 scale).
/// Hybrid mode only: beats set timing, amplitude sets intensity.
/// Applies exponential smoothing to prevent jerky jumps when the beat divisor changes.
/// </summary>
internal sealed class PulseTCodeMapper
{
    /// <summary>Minimum position (bottom of stroke). 0–100 scale.</summary>
    private const double MinPosition = 5.0;

    /// <summary>Maximum position (top of stroke). 0–100 scale.</summary>
    private const double MaxPosition = 95.0;

    /// <summary>Resting position when no beats are nearby.</summary>
    private const double RestPosition = 50.0;

    /// <summary>
    /// Fraction of the inter-beat interval used for the upstroke.
    /// The rest is the downstroke (return).
    /// </summary>
    private const double UpstrokeFraction = 0.4;

    /// <summary>Minimum amplitude scaling — even at zero amplitude, still move a bit.</summary>
    private const double MinAmplitudeScale = 0.15;

    /// <summary>
    /// Smoothing time constant in milliseconds. Controls how quickly the output
    /// converges to the raw target after a beat-grid change. Lower = more responsive,
    /// higher = smoother transitions. 40 ms ≈ 2–3 ticks at 60 Hz.
    /// </summary>
    private const double SmoothingTimeConstantMs = 40.0;

    // ── Smoothing state ──
    private double _lastPosition = RestPosition;
    private double _lastTimeMs = -1;

    // ── Stroke settings ──
    private PulseStrokeSettings _settings = PulseStrokeSettings.Default;

    /// <summary>
    /// Updates the stroke control settings applied during position calculation.
    /// </summary>
    /// <param name="settings">New stroke settings, or null to reset to defaults.</param>
    public void SetStrokeSettings(PulseStrokeSettings? settings)
    {
        _settings = settings ?? PulseStrokeSettings.Default;
    }

    /// <summary>
    /// Given the current playback position, pre-analyzed BeatMap, and live amplitude,
    /// return the desired L0 axis position on a 0–100 scale.
    /// </summary>
    /// <param name="beatMap">Pre-analyzed beat map. Null or empty beats returns rest position.</param>
    /// <param name="currentTimeMs">Current playback position in milliseconds.</param>
    /// <param name="currentAmplitude">Live RMS amplitude (0.0–1.0) from LiveAmplitudeService.</param>
    /// <returns>L0 axis position (0–100).</returns>
    public double MapToPosition(BeatMap? beatMap, double currentTimeMs, double currentAmplitude)
    {
        return MapToPosition(beatMap, currentTimeMs, currentAmplitude, out _);
    }

    /// <summary>
    /// Given the current playback position, pre-analyzed BeatMap, and live amplitude,
    /// return the desired L0 axis position on a 0–100 scale and output the current beat index.
    /// </summary>
    /// <param name="beatMap">Pre-analyzed beat map. Null or empty beats returns rest position.</param>
    /// <param name="currentTimeMs">Current playback position in milliseconds.</param>
    /// <param name="currentAmplitude">Live RMS amplitude (0.0–1.0) from LiveAmplitudeService.</param>
    /// <param name="beatIndex">Index of most recent beat at or before <paramref name="currentTimeMs"/>, or -1.</param>
    /// <returns>L0 axis position (0–100).</returns>
    public double MapToPosition(BeatMap? beatMap, double currentTimeMs, double currentAmplitude, out int beatIndex)
    {
        double rawPosition = ComputeRawPosition(beatMap, currentTimeMs, currentAmplitude, out beatIndex);

        // Apply exponential smoothing to prevent jerky jumps when the beat
        // divisor changes (the beat grid shifts and the raw target can jump
        // to a very different phase position).
        if (_lastTimeMs < 0)
        {
            // First call — no smoothing, just adopt the position.
            _lastPosition = rawPosition;
            _lastTimeMs = currentTimeMs;
            return rawPosition;
        }

        double deltaMs = currentTimeMs - _lastTimeMs;
        _lastTimeMs = currentTimeMs;

        if (deltaMs <= 0)
            return _lastPosition;

        double alpha = 1.0 - Math.Exp(-deltaMs / SmoothingTimeConstantMs);
        double smoothed = _lastPosition + alpha * (rawPosition - _lastPosition);
        _lastPosition = smoothed;

        return Math.Clamp(smoothed, MinPosition, MaxPosition);
    }

    /// <summary>
    /// Computes the raw (unsmoothed) L0 position from the beat map and amplitude.
    /// </summary>
    private double ComputeRawPosition(BeatMap? beatMap, double currentTimeMs, double currentAmplitude, out int beatIndex)
    {
        if (beatMap is null || beatMap.Beats.Count == 0)
        {
            beatIndex = -1;
            return RestPosition;
        }

        // Clamp amplitude.
        double amplitude = Math.Clamp(currentAmplitude, 0.0, 1.0);

        // Find the surrounding beats.
        beatIndex = FindCurrentBeatIndex(beatMap.Beats, currentTimeMs);

        if (beatIndex < 0)
        {
            // Before the first beat — resting.
            return RestPosition;
        }

        var currentBeat = beatMap.Beats[beatIndex];
        double beatTimeMs = currentBeat.TimestampMs;
        double beatStrength = currentBeat.Strength;

        // Determine inter-beat interval.
        double intervalMs;
        if (beatIndex + 1 < beatMap.Beats.Count)
        {
            intervalMs = beatMap.Beats[beatIndex + 1].TimestampMs - beatTimeMs;
        }
        else if (beatMap.Bpm > 0)
        {
            // Last beat — use BPM to estimate interval.
            intervalMs = 60000.0 / beatMap.Bpm;
        }
        else
        {
            // No BPM info — use a default 500ms (120 BPM).
            intervalMs = 500.0;
        }

        // Ensure interval is sane.
        intervalMs = Math.Max(intervalMs, 50.0);

        // Time since the current beat.
        double elapsed = currentTimeMs - beatTimeMs;

        // If we've passed beyond the current beat's interval, rest.
        if (elapsed > intervalMs)
            return RestPosition;

        // Phase within the beat cycle (0.0 – 1.0).
        double phase = elapsed / intervalMs;

        // Compute stroke intensity from amplitude and beat strength.
        double amplitudeScale = MinAmplitudeScale + (1.0 - MinAmplitudeScale) * amplitude;
        double intensityScale = amplitudeScale * (0.5 + 0.5 * beatStrength);

        // Full stroke range scaled by intensity.
        double maxHalfRange = (MaxPosition - MinPosition) / 2.0;
        double halfRange = maxHalfRange * intensityScale;

        // Apply amplitude offset: -1.0 = zero movement, 0.0 = unchanged, +1.0 = full-range strokes.
        if (_settings.AmplitudeOffset >= 0)
        {
            // Blend toward maxHalfRange as offset increases above 0.
            halfRange = halfRange + (maxHalfRange - halfRange) * _settings.AmplitudeOffset;
        }
        else
        {
            // Scale down toward zero as offset decreases below 0.
            halfRange = Math.Clamp(halfRange * (1.0 + _settings.AmplitudeOffset), 0.0, maxHalfRange);
        }

        // Apply randomness: deterministic per-beat variation.
        if (_settings.Randomness > 0.0 && beatIndex >= 0)
        {
            double randomFactor = PseudoRandom(beatIndex * 73856093);
            // Map to 0.2–1.0 range, blend with Randomness slider.
            double variation = 0.2 + 0.8 * randomFactor;
            double blended = 1.0 + _settings.Randomness * (variation - 1.0);
            halfRange *= blended;
        }

        double top = RestPosition + halfRange;
        double bottom = RestPosition - halfRange;

        // Clamp to valid range.
        top = Math.Min(top, MaxPosition);
        bottom = Math.Max(bottom, MinPosition);

        // Compute position from stroke pattern.
        double position = ComputePatternPosition(phase, top, bottom, _settings.EasingBlend);

        return Math.Clamp(position, MinPosition, MaxPosition);
    }

    /// <summary>Reset internal tracking state (e.g., on seek, media change, or divisor change).</summary>
    public void Reset()
    {
        _lastPosition = RestPosition;
        _lastTimeMs = -1;
    }

    /// <summary>
    /// Find the index of the most recent beat at or before <paramref name="timeMs"/>.
    /// Uses binary search on the sorted beat list.
    /// Returns -1 if timeMs is before all beats.
    /// </summary>
    /// <param name="beats">Sorted list of beat events.</param>
    /// <param name="timeMs">Time in milliseconds to search for.</param>
    /// <returns>Index of the most recent beat at or before the time, or -1.</returns>
    internal static int FindCurrentBeatIndex(IReadOnlyList<BeatEvent> beats, double timeMs)
    {
        if (beats.Count == 0) return -1;

        int lo = 0, hi = beats.Count - 1;
        int result = -1;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (beats[mid].TimestampMs <= timeMs)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Stroke Pattern Methods                                           ║
    // ╚══════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Dispatches to the appropriate stroke pattern method.
    /// </summary>
    private double ComputePatternPosition(double phase, double top, double bottom, double blend)
    {
        return _settings.Pattern switch
        {
            StrokePattern.Classic => ComputeClassicPosition(phase, top, bottom, blend),
            StrokePattern.DoubleTap => ComputeMultiTapPosition(phase, top, bottom, blend, 2),
            StrokePattern.TripleTap => ComputeMultiTapPosition(phase, top, bottom, blend, 3),
            StrokePattern.HoldTop => ComputeHoldPosition(phase, top, bottom, blend, holdAtTop: true),
            StrokePattern.HoldBottom => ComputeHoldPosition(phase, top, bottom, blend, holdAtTop: false),
            _ => ComputeClassicPosition(phase, top, bottom, blend),
        };
    }

    /// <summary>
    /// Classic stroke: upstroke during first fraction, downstroke during remainder.
    /// </summary>
    private static double ComputeClassicPosition(double phase, double top, double bottom, double blend)
    {
        if (phase < UpstrokeFraction)
        {
            double t = phase / UpstrokeFraction;
            double eased = BlendedEaseOut(t, blend);
            return bottom + (top - bottom) * eased;
        }
        else
        {
            double t = (phase - UpstrokeFraction) / (1.0 - UpstrokeFraction);
            double eased = BlendedEaseIn(t, blend);
            return top + (bottom - top) * eased;
        }
    }

    /// <summary>
    /// Multi-tap pattern: divides the beat into N equal sub-intervals, each a full Classic stroke.
    /// </summary>
    private static double ComputeMultiTapPosition(double phase, double top, double bottom, double blend, int taps)
    {
        double subPhase = (phase * taps) % 1.0;
        return ComputeClassicPosition(subPhase, top, bottom, blend);
    }

    /// <summary>
    /// Hold pattern: 30% travel to target, 40% hold at target, 30% return.
    /// </summary>
    private static double ComputeHoldPosition(double phase, double top, double bottom, double blend, bool holdAtTop)
    {
        const double travelFraction = 0.3;
        const double holdFraction = 0.4;

        double target = holdAtTop ? top : bottom;
        double start = holdAtTop ? bottom : top;

        if (phase < travelFraction)
        {
            double t = phase / travelFraction;
            double eased = BlendedEaseOut(t, blend);
            return start + (target - start) * eased;
        }
        else if (phase < travelFraction + holdFraction)
        {
            return target;
        }
        else
        {
            double t = (phase - travelFraction - holdFraction) / (1.0 - travelFraction - holdFraction);
            double eased = BlendedEaseIn(t, blend);
            return target + (start - target) * eased;
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Easing Functions                                                ║
    // ╚══════════════════════════════════════════════════════════════════════╝

    /// <summary>Quadratic ease-out: fast start, gradual stop.</summary>
    private static double EaseOutQuad(double t) => 1.0 - (1.0 - t) * (1.0 - t);

    /// <summary>Quadratic ease-in: gradual start, fast end.</summary>
    private static double EaseInQuad(double t) => t * t;

    /// <summary>
    /// Blended ease-out: interpolates between sinusoidal (gentle), quadratic (default), and linear (aggressive).
    /// blend = −1.0 → sin(t × π/2), blend = 0.0 → EaseOutQuad, blend = +1.0 → linear t.
    /// </summary>
    internal static double BlendedEaseOut(double t, double blend)
    {
        if (blend <= 0)
        {
            double sinEase = Math.Sin(t * Math.PI / 2.0);
            double quadEase = EaseOutQuad(t);
            double factor = -blend; // 0..1
            return quadEase + factor * (sinEase - quadEase);
        }
        else
        {
            double quadEase = EaseOutQuad(t);
            return quadEase + blend * (t - quadEase);
        }
    }

    /// <summary>
    /// Blended ease-in: interpolates between sinusoidal (gentle), quadratic (default), and linear (aggressive).
    /// blend = −1.0 → 1 − sin((1−t) × π/2), blend = 0.0 → EaseInQuad, blend = +1.0 → linear t.
    /// </summary>
    internal static double BlendedEaseIn(double t, double blend)
    {
        if (blend <= 0)
        {
            double sinEase = 1.0 - Math.Sin((1.0 - t) * Math.PI / 2.0);
            double quadEase = EaseInQuad(t);
            double factor = -blend;
            return quadEase + factor * (sinEase - quadEase);
        }
        else
        {
            double quadEase = EaseInQuad(t);
            return quadEase + blend * (t - quadEase);
        }
    }

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║ Pseudo-Random                                                   ║
    // ╚══════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Returns a deterministic pseudo-random value in [0, 1] for a given integer seed.
    /// Uses xorshift32 — allocation-free, reproducible, suitable for real-time audio processing.
    /// </summary>
    internal static double PseudoRandom(int seed)
    {
        uint x = unchecked((uint)seed);
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x7FFFFFFF) / (double)0x7FFFFFFF;
    }
}
