using System.IO.Compression;
using System.Text.Json;
using Vido.Core.Logging;
using Vido.Core.Plugin;

namespace Vido.Services.Plugin;

/// <summary>
/// Handles downloading, extracting, validating, and removing plugins.
/// Supports both HTTPS and file:// registry URLs.
/// </summary>
public sealed class PluginInstaller : IPluginInstaller
{
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly string _pluginDirectory;

    /// <summary>
    /// Marker file placed in a plugin directory to flag deferred deletion.
    /// </summary>
    private const string UninstallMarker = ".uninstall";
    
    /// <summary>
    /// Creates a plugin installer that downloads and extracts plugins to the default plugin directory.
    /// </summary>
    /// <param name="logService">The logging service used to report installation progress and errors.</param>
    public PluginInstaller(ILogService logService)
        : this(logService, new HttpClient(), PluginPaths.DefaultPluginDirectory)
    {
    }

    /// <summary>
    /// Internal constructor for testing — allows injecting custom HttpClient and directory.
    /// </summary>
    internal PluginInstaller(ILogService logService, HttpClient httpClient, string pluginDirectory)
    {
        _logService = logService;
        _httpClient = httpClient;
        _pluginDirectory = pluginDirectory;
    }

    /// <summary>
    /// Downloads, extracts, and validates a plugin from the registry entry's download URL.
    /// Returns <c>true</c> if the plugin was installed successfully and its <c>plugin.json</c> manifest was found.
    /// </summary>
    /// <param name="entry">The registry entry containing the plugin ID and download URL.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="entry"/> has an empty ID or download URL.</exception>
    public async Task<bool> InstallAsync(PluginRegistryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Id))
            throw new ArgumentException("Plugin entry must have an ID.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.DownloadUrl))
            throw new ArgumentException("Plugin entry must have a download URL.", nameof(entry));

        var targetDir = Path.Combine(_pluginDirectory, entry.Id);

        try
        {
            _logService.Info($"Installing plugin '{entry.Id}' from {entry.DownloadUrl}...", "PluginInstaller");

            // Download the zip
            byte[] zipData;
            if (entry.DownloadUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = new Uri(entry.DownloadUrl).LocalPath;
                zipData = await File.ReadAllBytesAsync(localPath);
            }
            else
            {
                zipData = await _httpClient.GetByteArrayAsync(entry.DownloadUrl);
            }

            // Ensure target directory exists — best-effort clean of existing files.
            // Some files may be locked (AV, pending deletes) so we tolerate failures
            // and rely on ZipFile.ExtractToDirectory's overwriteFiles to replace what matters.
            if (Directory.Exists(targetDir))
            {
                try
                {
                    Directory.Delete(targetDir, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    _logService.Debug($"Could not fully clean '{targetDir}' (locked files). Will overwrite.", "PluginInstaller");
                }
                catch (IOException)
                {
                    _logService.Debug($"Could not fully clean '{targetDir}' (IO error). Will overwrite.", "PluginInstaller");
                }
            }
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // Extract
            using var zipStream = new MemoryStream(zipData);
            ZipFile.ExtractToDirectory(zipStream, targetDir, overwriteFiles: true);

            // Validate that plugin.json exists in the extracted content
            var manifestPath = Path.Combine(targetDir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                // The zip may contain a root folder (e.g. "com.vido.osr2-plus/").
                // When updating, remnant files from the old install may still
                // exist alongside the extracted subfolder, so we can't rely on
                // there being exactly one subdirectory. Search all subdirs for
                // the one that contains plugin.json.
                var subDirs = Directory.GetDirectories(targetDir);
                foreach (var subDir in subDirs)
                {
                    var innerManifest = Path.Combine(subDir, "plugin.json");
                    if (File.Exists(innerManifest))
                    {
                        // Move contents up one level
                        MoveContentsUp(subDir, targetDir);
                        manifestPath = Path.Combine(targetDir, "plugin.json");
                        break;
                    }
                }
            }

            if (!File.Exists(manifestPath))
            {
                _logService.Error($"Plugin '{entry.Id}' installation failed: plugin.json not found in archive.", "PluginInstaller");
                Directory.Delete(targetDir, recursive: true);
                return false;
            }

            // Remove any stale uninstall markers
            var markerPath = Path.Combine(targetDir, UninstallMarker);
            if (File.Exists(markerPath))
                File.Delete(markerPath);

            _logService.Info($"Plugin '{entry.Id}' installed successfully.", "PluginInstaller");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"Plugin '{entry.Id}' installation failed: {ex.Message}", "PluginInstaller");

            // Clean up partial install
            try
            {
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, recursive: true);
            }
            catch (Exception cleanupEx) { _logService.Debug($"Cleanup of partial install threw: {cleanupEx.Message}", "PluginInstaller"); }

            return false;
        }
    }

    /// <summary>
    /// Removes an installed plugin by deleting its directory.
    /// If files are locked, creates an uninstall marker for deferred cleanup on next startup.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to uninstall.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="pluginId"/> is null or whitespace.</exception>
    public Task<bool> UninstallAsync(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID must not be empty.", nameof(pluginId));

        var targetDir = Path.Combine(_pluginDirectory, pluginId);

        if (!Directory.Exists(targetDir))
        {
            _logService.Warning($"Plugin '{pluginId}' directory not found — nothing to uninstall.", "PluginInstaller");
            return Task.FromResult(true);
        }

        try
        {
            Directory.Delete(targetDir, recursive: true);
            _logService.Info($"Plugin '{pluginId}' uninstalled successfully.", "PluginInstaller");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            // DLL locking likely — create marker for deferred cleanup
            _logService.Warning(
                $"Plugin '{pluginId}' files are locked — marking for removal on next restart: {ex.Message}",
                "PluginInstaller");

            try
            {
                var markerPath = Path.Combine(targetDir, UninstallMarker);
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception markerEx)
            {
                _logService.Error($"Failed to create uninstall marker for '{pluginId}': {markerEx.Message}", "PluginInstaller");
            }

            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Scans the plugin directory for directories marked with an uninstall marker file and deletes them.
    /// Called at startup to complete deferred uninstalls that failed due to locked files.
    /// </summary>
    public void CleanupPendingUninstalls()
    {
        if (!Directory.Exists(_pluginDirectory))
            return;

        foreach (var dir in Directory.GetDirectories(_pluginDirectory))
        {
            var markerPath = Path.Combine(dir, UninstallMarker);
            if (!File.Exists(markerPath)) continue;

            var pluginId = Path.GetFileName(dir);
            try
            {
                Directory.Delete(dir, recursive: true);
                _logService.Info($"Cleaned up deferred uninstall for '{pluginId}'.", "PluginInstaller");
            }
            catch (Exception ex)
            {
                _logService.Warning($"Still unable to remove '{pluginId}': {ex.Message}", "PluginInstaller");
            }
        }
    }

    /// <summary>
    /// Downloads and deserializes the plugin registry JSON from a remote HTTPS or local file:// URL.
    /// Returns <c>null</c> if the URL is empty or the fetch fails.
    /// </summary>
    /// <param name="registryUrl">The HTTPS or file:// URL pointing to a <c>registry.json</c> file.</param>
    public async Task<PluginRegistry?> FetchRegistryAsync(string registryUrl)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
            return null;

        try
        {
            string json;
            if (registryUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = new Uri(registryUrl).LocalPath;
                json = await File.ReadAllTextAsync(localPath);
            }
            else
            {
                // Ensure URL ends with registry.json if it doesn't have a file extension
                var fetchUrl = registryUrl;
                if (!fetchUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fetchUrl = fetchUrl.TrimEnd('/') + "/registry.json";
                }
                json = await _httpClient.GetStringAsync(fetchUrl);
            }

            var registry = JsonSerializer.Deserialize<PluginRegistry>(json);
            return registry;
        }
        catch (Exception ex)
        {
            _logService.Warning($"Failed to fetch registry from '{registryUrl}': {ex.Message}", "PluginInstaller");
            return null;
        }
    }

    /// <summary>
    /// Moves all files and directories from <paramref name="sourceDir"/> into
    /// <paramref name="targetDir"/>, then removes <paramref name="sourceDir"/>.
    /// Used when a zip has a single root folder wrapping the plugin files.
    /// </summary>
    private static void MoveContentsUp(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Move(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destDir = Path.Combine(targetDir, Path.GetFileName(dir));
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, recursive: true);
            Directory.Move(dir, destDir);
        }

        Directory.Delete(sourceDir, recursive: false);
    }
}
