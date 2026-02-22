namespace Vido.Core.Plugin;

/// <summary>
/// Handles downloading, extracting, validating, and removing plugins.
/// </summary>
public interface IPluginInstaller
{
    /// <summary>
    /// Installs a plugin by downloading from the given URL and extracting
    /// to the default plugin directory.
    /// </summary>
    /// <param name="entry">The registry entry to install.</param>
    /// <returns>True if the installation succeeded; false otherwise.</returns>
    Task<bool> InstallAsync(PluginRegistryEntry entry);

    /// <summary>
    /// Uninstalls a plugin. Attempts to delete the plugin directory.
    /// If files are locked, creates a <c>.uninstall</c> marker for cleanup on next restart.
    /// </summary>
    /// <param name="pluginId">The plugin ID to uninstall.</param>
    /// <returns>True if fully removed; false if marked for deferred removal.</returns>
    Task<bool> UninstallAsync(string pluginId);

    /// <summary>
    /// Cleans up any plugins marked with <c>.uninstall</c> markers from a previous session.
    /// Should be called early during application startup.
    /// </summary>
    void CleanupPendingUninstalls();

    /// <summary>
    /// Fetches all plugin entries from the given registry URL.
    /// Supports <c>https://</c> and <c>file://</c> URLs.
    /// </summary>
    /// <param name="registryUrl">The registry URL to fetch from.</param>
    /// <returns>The parsed registry, or null if fetching/parsing failed.</returns>
    Task<PluginRegistry?> FetchRegistryAsync(string registryUrl);
}
