using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Vido.Core.Models.Osr2Plus;

/// <summary>
/// Per-axis configuration for the OSR2+ haptic device.
/// Observable for UI binding via <see cref="INotifyPropertyChanged"/>.
/// Persisted to settings (except ephemeral fields marked with <see cref="JsonIgnoreAttribute"/>).
/// </summary>
public class AxisConfig : INotifyPropertyChanged
{
    // ===== Identity (non-persisted, set at construction) =====

    /// <summary>Axis identifier: "L0", "R0", "R1", or "R2".</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-readable axis name: "Stroke", "Twist", "Roll", or "Pitch".</summary>
    public string Name { get; set; } = "";

    /// <summary>Axis motion type: "linear" or "rotation".</summary>
    public string Type { get; set; } = "linear";

    /// <summary>Hex color used for axis visualization (e.g. "#007ACC").</summary>
    public string Color { get; set; } = "#007ACC";

    // ===== Persisted Settings =====
    private int _min;
    private int _max = 100;
    private bool _enabled = true;
    private AxisFillMode _fillMode = AxisFillMode.None;
    private bool _syncWithStroke = true;
    private double _fillSpeedHz = 1.0;

    /// <summary>Minimum amplitude (0 to 99). Must be strictly less than <see cref="Max"/>.</summary>
    public int Min
    {
        get => _min;
        set { if (value < Max && Set(ref _min, Math.Clamp(value, 0, 99))) OnPropertyChanged(nameof(RangeLabel)); }
    }

    /// <summary>Maximum amplitude (1 to 100). Must be strictly greater than <see cref="Min"/>.</summary>
    public int Max
    {
        get => _max;
        set { if (value > Min && Set(ref _max, Math.Clamp(value, 1, 100))) OnPropertyChanged(nameof(RangeLabel)); }
    }

    /// <summary>Whether this axis sends T-Code instructions to the device.</summary>
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    /// <summary>Active fill mode for this axis.</summary>
    public AxisFillMode FillMode { get => _fillMode; set => Set(ref _fillMode, value); }

    /// <summary>
    /// When <c>true</c>, fill pattern ticks only when L0 is moving and speed-matches L0.
    /// Not applicable to L0 itself.
    /// </summary>
    public bool SyncWithStroke { get => _syncWithStroke; set => Set(ref _syncWithStroke, value); }

    /// <summary>Independent fill pattern speed in Hz (0.1–3.0). Used when <see cref="SyncWithStroke"/> is <c>false</c>.</summary>
    public double FillSpeedHz { get => _fillSpeedHz; set => Set(ref _fillSpeedHz, Math.Clamp(value, 0.1, 3.0)); }

    // ===== Ephemeral (NOT persisted, reset each session) =====
    private double _positionOffset;

    /// <summary>
    /// Manual position offset applied to this axis.
    /// L0: -50 to +50 (%), R0/R1/R2: 0–359 (degrees).
    /// Not persisted.
    /// </summary>
    [JsonIgnore]
    public double PositionOffset { get => _positionOffset; set => Set(ref _positionOffset, value); }

    // ===== Derived =====

    /// <summary>Formatted string showing the current Min–Max range.</summary>
    [JsonIgnore]
    public string RangeLabel => $"{Min}-{Max}";

    /// <summary>Whether this axis supports a position offset control.</summary>
    [JsonIgnore]
    public bool HasPositionOffset => Id is "L0" or "R0" or "R1" or "R2";

    /// <summary>
    /// Zero-based index into per-axis state arrays in <see cref="TCodeService"/> and <see cref="InterpolationService"/>.
    /// Assigned dynamically by list index in <c>TCodeService.SetAxisConfigs</c>.
    /// Not persisted — ephemeral runtime value.
    /// </summary>
    [JsonIgnore]
    public int Ordinal { get; internal set; }

    /// <summary>Whether this is the primary stroke axis (L0).</summary>
    [JsonIgnore]
    public bool IsStroke => Id == "L0";

    /// <summary>Whether this is the pitch axis (R2).</summary>
    [JsonIgnore]
    public bool IsPitch => Id == "R2";

    /// <summary>Returns the fill modes available for this axis type.</summary>
    [JsonIgnore]
    public AxisFillMode[] AvailableFillModes => Id switch
    {
        "L0" => [AxisFillMode.None],
        _ =>
        [
            AxisFillMode.None, AxisFillMode.Random,
            AxisFillMode.Triangle, AxisFillMode.Sine, AxisFillMode.Saw,
            AxisFillMode.SawtoothReverse, AxisFillMode.Square, AxisFillMode.Pulse,
            AxisFillMode.EaseInOut
        ]
    };

    // ===== Funscript assignment (ephemeral) =====
    private string? _scriptFileName;
    private bool _isScriptManual;

    /// <summary>Name of the assigned funscript file, or <c>null</c> if none.</summary>
    [JsonIgnore]
    public string? ScriptFileName { get => _scriptFileName; set { Set(ref _scriptFileName, value); OnPropertyChanged(nameof(HasScript)); } }

    /// <summary>Whether the funscript was manually assigned by the user.</summary>
    [JsonIgnore]
    public bool IsScriptManual { get => _isScriptManual; set => Set(ref _isScriptManual, value); }

    /// <summary>Whether a funscript is currently assigned to this axis.</summary>
    [JsonIgnore]
    public bool HasScript => ScriptFileName != null;

    // ===== Card UI state (ephemeral) =====
    private bool _isExpanded;

    /// <summary>Whether this axis card is expanded in the UI.</summary>
    [JsonIgnore]
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    // ===== Defaults factory =====

    /// <summary>
    /// Creates the default axis configurations for L0 (Stroke), R0 (Twist), R1 (Roll), R2 (Pitch).
    /// </summary>
    /// <returns>A list of 4 <see cref="AxisConfig"/> instances with standard defaults.</returns>
    public static List<AxisConfig> CreateDefaults() =>
    [
        new() { Id = "L0", Name = "Stroke", Type = "linear",   Color = "#007ACC", Min = 0, Max = 100 },
        new() { Id = "R0", Name = "Twist",  Type = "rotation", Color = "#B800CC", Min = 0, Max = 100 },
        new() { Id = "R1", Name = "Roll",   Type = "rotation", Color = "#CC5200", Min = 0, Max = 100 },
        new() { Id = "R2", Name = "Pitch",  Type = "rotation", Color = "#14CC00", Min = 0, Max = 75  },
    ];

    // ===== INotifyPropertyChanged =====

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises the <see cref="PropertyChanged"/> event.</summary>
    /// <param name="name">Name of the property that changed.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Sets the backing field and raises <see cref="PropertyChanged"/> if the value changed.
    /// </summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="name">Property name (auto-populated by compiler).</param>
    /// <returns><c>true</c> if the value was changed; otherwise <c>false</c>.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
