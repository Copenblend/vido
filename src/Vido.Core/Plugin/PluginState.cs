namespace Vido.Core.Plugin;

/// <summary>
/// Lifecycle state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>Plugin directory discovered but not yet validated.</summary>
    Discovered,

    /// <summary>Manifest parsed and validated successfully.</summary>
    Validated,

    /// <summary>Assembly loaded into the runtime.</summary>
    Loaded,

    /// <summary>Plugin activated — <see cref="IVidoPlugin.Activate"/> called successfully.</summary>
    Active,

    /// <summary>Plugin deactivated — <see cref="IVidoPlugin.Deactivate"/> called.</summary>
    Deactivated,

    /// <summary>Plugin disabled by the user (will not be loaded on startup).</summary>
    Disabled,

    /// <summary>Plugin failed to load or activate.</summary>
    Error
}
