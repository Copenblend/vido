namespace Vido.Core.Plugin;

/// <summary>
/// Manages the plugin lifecycle: discovery, loading, activation, and deactivation.
/// Registered as a singleton in the DI container.
/// </summary>
public interface IPluginHost
{
    /// <summary>All discovered plugins (regardless of state).</summary>
    IReadOnlyList<PluginInfo> Plugins { get; }

    /// <summary>
    /// Discovers, validates, loads, and activates all plugins from configured directories.
    /// Should be called once during startup after all services are available.
    /// </summary>
    void ActivateAll();

    /// <summary>
    /// Deactivates all active plugins. Called during application shutdown.
    /// </summary>
    void DeactivateAll();

    /// <summary>
    /// Gets the <see cref="PluginInfo"/> for a specific plugin by ID, or null.
    /// </summary>
    PluginInfo? GetPlugin(string pluginId);

    /// <summary>
    /// Enables or disables a plugin. Disabled plugins are not loaded on startup.
    /// Changes take effect on the next application restart.
    /// </summary>
    void SetEnabled(string pluginId, bool enabled);

    /// <summary>
    /// Returns the list of disabled plugin IDs from persisted settings.
    /// </summary>
    IReadOnlyList<string> GetDisabledPluginIds();

    /// <summary>
    /// Gets or creates a settings store for the specified plugin.
    /// Used by the Plugin Manager UI to display and edit plugin settings.
    /// </summary>
    IPluginSettingsStore GetSettingsStore(string pluginId);

    /// <summary>
    /// Removes all runtime state for a plugin (from the discovered list,
    /// contexts, settings stores, and disabled-plugin list).
    /// Called during uninstall so that a subsequent install discovers the plugin fresh.
    /// </summary>
    void RemovePlugin(string pluginId);
}
