using System.Text.Json.Serialization;

namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// A named, persisted group of axis fill settings across all four axes.
/// </summary>
public sealed class FillProfile
{
    /// <summary>Profile display name.</summary>
    public required string Name { get; set; }

    /// <summary>Whether this is a built-in read-only profile. Not persisted to JSON.</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// Per-axis settings keyed by axis ID ("L0", "R0", "R1", "R2").
    /// </summary>
    public Dictionary<string, FillAxisSettings> Axes { get; init; } = new();

    /// <summary>
    /// Creates a deep copy of this profile's axis settings.
    /// </summary>
    public Dictionary<string, FillAxisSettings> CloneAxes()
    {
        var clone = new Dictionary<string, FillAxisSettings>(Axes.Count);
        foreach (var (key, value) in Axes)
        {
            clone[key] = new FillAxisSettings
            {
                Enabled = value.Enabled,
                Min = value.Min,
                Max = value.Max,
                FillMode = value.FillMode,
                SyncWithStroke = value.SyncWithStroke,
                FillSpeedHz = value.FillSpeedHz,
            };
        }
        return clone;
    }

    /// <summary>
    /// Checks if the given axis settings match this profile's settings.
    /// </summary>
    /// <param name="other">Axis settings to compare against.</param>
    /// <returns><c>true</c> if all axis settings match; otherwise <c>false</c>.</returns>
    public bool MatchesAxes(IReadOnlyDictionary<string, FillAxisSettings> other)
    {
        if (Axes.Count != other.Count) return false;
        foreach (var (key, mine) in Axes)
        {
            if (!other.TryGetValue(key, out var theirs)) return false;
            if (mine.Enabled != theirs.Enabled) return false;
            if (mine.Min != theirs.Min) return false;
            if (mine.Max != theirs.Max) return false;
            if (!string.Equals(mine.FillMode, theirs.FillMode, StringComparison.OrdinalIgnoreCase)) return false;
            if (mine.SyncWithStroke != theirs.SyncWithStroke) return false;
            if (Math.Abs(mine.FillSpeedHz - theirs.FillSpeedHz) > 0.001) return false;
        }
        return true;
    }
}
