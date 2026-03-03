namespace Vido.Core.Haptics;

/// <summary>
/// Published when an external source provides beat times for the BeatBar overlay.
/// The haptic transport should display these using the registered beat source's renderer.
/// </summary>
/// <remarks>
/// High-frequency publishers can pass a slice of a reusable beat buffer via
/// <see cref="ReadOnlyMemory{T}"/> to reduce per-event allocations.
/// </remarks>
public readonly record struct ExternalBeatEvent
{
    private readonly ReadOnlyMemory<double>? _beatTimesMs;
    private readonly string? _sourceId;

    /// <summary>Beat timestamps in milliseconds relative to media start.</summary>
    public ReadOnlyMemory<double> BeatTimesMs
    {
        get => _beatTimesMs ?? ReadOnlyMemory<double>.Empty;
        init => _beatTimesMs = value;
    }

    /// <summary>
    /// ID matching the registered <see cref="IExternalBeatSource"/> that should render these beats.
    /// Defaults to <see cref="string.Empty"/> for <c>default(ExternalBeatEvent)</c>.
    /// </summary>
    public string SourceId
    {
        get => _sourceId ?? string.Empty;
        init => _sourceId = value;
    }
}
