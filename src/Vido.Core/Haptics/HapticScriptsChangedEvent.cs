namespace Vido.Core.Haptics;

/// <summary>
/// Published when funscripts are loaded or cleared for the current video.
/// The haptic transport publishes this event so other features can observe script state.
/// </summary>
/// <remarks>
/// Emitted when script availability changes for one or more axes.
/// </remarks>
public readonly record struct HapticScriptsChangedEvent
{
    private static readonly IReadOnlyDictionary<string, bool> EmptyAxisScriptLoaded = new Dictionary<string, bool>();
    private readonly IReadOnlyDictionary<string, bool>? _axisScriptLoaded;

    /// <summary>Whether any funscripts are loaded for the current video.</summary>
    public bool HasAnyScripts { get; init; }

    /// <summary>
    /// Per-axis script load state. Keys are axis IDs (e.g. "L0", "R0"), values indicate
    /// whether a script is loaded for that axis.
    /// </summary>
    public IReadOnlyDictionary<string, bool> AxisScriptLoaded
    {
        get => _axisScriptLoaded ?? EmptyAxisScriptLoaded;
        init => _axisScriptLoaded = value;
    }
}
