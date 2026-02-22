using System.IO.Compression;
using System.Net.Http;
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

    /// <summary>Marker file placed in a plugin directory to flag deferred deletion.</summary>
    private const string UninstallMarker = ".uninstall";

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

    /// <inheritdoc/>
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

            // Ensure target directory exists and is clean
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
            Directory.CreateDirectory(targetDir);

            // Extract
            using var zipStream = new MemoryStream(zipData);
            ZipFile.ExtractToDirectory(zipStream, targetDir, overwriteFiles: true);

            // Validate that plugin.json exists in the extracted content
            var manifestPath = Path.Combine(targetDir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                // Check if the zip had a single root folder
                var subDirs = Directory.GetDirectories(targetDir);
                if (subDirs.Length == 1)
                {
                    var innerManifest = Path.Combine(subDirs[0], "plugin.json");
                    if (File.Exists(innerManifest))
                    {
                        // Move contents up one level
                        MoveContentsUp(subDirs[0], targetDir);
                        manifestPath = Path.Combine(targetDir, "plugin.json");
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
            catch { /* best effort */ }

            return false;
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
