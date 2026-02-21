namespace Vido.Core.Plugin;

/// <summary>
/// Runtime information about a loaded/discovered plugin.
/// </summary>
public sealed class PluginInfo
{
    /// <summary>The parsed plugin manifest.</summary>
    public required PluginManifest Manifest { get; init; }

    /// <summary>Absolute path to the plugin's installation directory.</summary>
    public required string Directory { get; init; }

    /// <summary>Current lifecycle state of the plugin.</summary>
    public PluginState State { get; set; } = PluginState.Discovered;

    /// <summary>
    /// The instantiated plugin instance. Null until the assembly is loaded
    /// and the entry class is instantiated.
    /// </summary>
    public IVidoPlugin? Instance { get; set; }

    /// <summary>
    /// Error message if the plugin failed to load or activate.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
