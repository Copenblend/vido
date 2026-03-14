namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Controls the beat bar overlay behavior with built-in modes (Off, OnPeak, OnValley,
/// OnPeakAndValley, MidStroke).
/// </summary>
public sealed class BeatBarMode : IEquatable<BeatBarMode>
{
    /// <summary>No beat bar displayed.</summary>
    public static readonly BeatBarMode Off = new("Off", "Off");

    /// <summary>Beat markers appear at peaks (up→down direction changes).</summary>
    public static readonly BeatBarMode OnPeak = new("OnPeak", "OnPeak");

    /// <summary>Beat markers appear at valleys (down→up direction changes).</summary>
    public static readonly BeatBarMode OnValley = new("OnValley", "OnValley");

    /// <summary>Beat markers appear at both peaks and valleys (any direction change).</summary>
    public static readonly BeatBarMode OnPeakAndValley = new("OnPeakAndValley", "On Peak & Valley");

    /// <summary>Beat markers appear at the midpoint of descending strokes (50-crossing).</summary>
    public static readonly BeatBarMode MidStroke = new("MidStroke", "Mid Stroke");

    /// <summary>All built-in modes in display order.</summary>
    public static readonly IReadOnlyList<BeatBarMode> BuiltInModes = [Off, OnPeak, OnValley, OnPeakAndValley, MidStroke];

    /// <summary>
    /// Unique identifier for this mode (e.g. "Off", "OnPeak", "OnValley").
    /// </summary>
    public string Id { get; }

    /// <summary>Display name shown in the beat bar mode selector.</summary>
    public string DisplayName { get; }

    private BeatBarMode(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

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
