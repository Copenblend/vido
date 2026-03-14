namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Controls the beat bar overlay behavior. Supports built-in modes (Off, OnPeak, OnValley)
/// and dynamically registered external modes from features.
/// </summary>
public sealed class BeatBarMode : IEquatable<BeatBarMode>
{
    /// <summary>No beat bar displayed.</summary>
    public static readonly BeatBarMode Off = new("Off", "Off", isExternal: false);

    /// <summary>Beat markers appear at peaks (up→down direction changes).</summary>
    public static readonly BeatBarMode OnPeak = new("OnPeak", "OnPeak", isExternal: false);

    /// <summary>Beat markers appear at valleys (down→up direction changes).</summary>
    public static readonly BeatBarMode OnValley = new("OnValley", "OnValley", isExternal: false);

    /// <summary>Beat markers appear at both peaks and valleys (any direction change).</summary>
    public static readonly BeatBarMode OnPeakAndValley = new("OnPeakAndValley", "On Peak & Valley", isExternal: false);

    /// <summary>Beat markers appear at the midpoint of descending strokes (50-crossing).</summary>
    public static readonly BeatBarMode MidStroke = new("MidStroke", "Mid Stroke", isExternal: false);

    /// <summary>All built-in modes in display order.</summary>
    public static readonly IReadOnlyList<BeatBarMode> BuiltInModes = [Off, OnPeak, OnValley, OnPeakAndValley, MidStroke];

    /// <summary>
    /// Unique identifier for this mode.
    /// Built-in: "Off", "OnPeak", "OnValley". External: the source Id.
    /// </summary>
    public string Id { get; }

    /// <summary>Display name shown in the beat bar mode selector.</summary>
    public string DisplayName { get; }

    /// <summary><c>true</c> if this mode is provided by an external beat source.</summary>
    public bool IsExternal { get; }

    private BeatBarMode(string id, string displayName, bool isExternal)
    {
        Id = id;
        DisplayName = displayName;
        IsExternal = isExternal;
    }

    /// <summary>
    /// Creates an external beat bar mode from a feature-provided beat source.
    /// </summary>
    /// <param name="sourceId">Unique identifier of the external beat source.</param>
    /// <param name="displayName">Human-readable name for the mode selector.</param>
    /// <returns>A new <see cref="BeatBarMode"/> instance with <see cref="IsExternal"/> set to <c>true</c>.</returns>
    public static BeatBarMode CreateExternal(string sourceId, string displayName)
        => new(sourceId, displayName, isExternal: true);

    /// <inheritdoc />
    public bool Equals(BeatBarMode? other) => other is not null && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as BeatBarMode);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Id;

    /// <summary>Equality operator comparing by <see cref="Id"/>.</summary>
    public static bool operator ==(BeatBarMode? left, BeatBarMode? right)
        => ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>Inequality operator comparing by <see cref="Id"/>.</summary>
    public static bool operator !=(BeatBarMode? left, BeatBarMode? right)
        => !(left == right);
}
