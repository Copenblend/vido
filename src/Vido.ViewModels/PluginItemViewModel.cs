using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Vido.Core.Plugin;

namespace Vido.ViewModels;

/// <summary>
/// Represents a single plugin in the Plugin Manager sidebar.
/// Tracks installed/available state, enabled/disabled state, and provides
/// command targets for install/uninstall/enable/disable actions.
/// </summary>
public partial class PluginItemViewModel : ObservableObject
{
    /// <summary>Unique plugin identifier.</summary>
    public string Id { get; }

    /// <summary>Display name of the plugin.</summary>
    public string DisplayName { get; }

    /// <summary>Short description of the plugin.</summary>
    public string Description { get; }

    /// <summary>Publisher / author name.</summary>
    public string Publisher { get; }

    /// <summary>Plugin version string.</summary>
    public string Version { get; }

    /// <summary>License identifier.</summary>
    public string License { get; }

    /// <summary>Tags for search.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Last updated date string.</summary>
    public string? LastUpdated { get; }

    /// <summary>Which registry this plugin came from.</summary>
    public string RegistryName { get; }

    /// <summary>Whether this plugin is from the official Vido registry (shows verified badge).</summary>
    [ObservableProperty]
    private bool _isOfficial;

    /// <summary>Whether the plugin is currently installed locally.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isInstalled;

    /// <summary>Whether the plugin is enabled (only relevant when installed).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isEnabled = true;

    /// <summary>Whether an install/uninstall operation is in progress.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Status text: "Enabled", "Disabled", or empty for available plugins.</summary>
    public string StatusText => IsInstalled ? (IsEnabled ? "Enabled" : "Disabled") : string.Empty;

    /// <summary>Absolute file-system path or URL to the plugin icon image.</summary>
    [ObservableProperty]
    private string? _iconSource;

    /// <summary>URL to the plugin README.md content (for available-but-not-installed plugins).</summary>
    public string? ReadmeUrl { get; init; }

    /// <summary>URL to the plugin CHANGELOG.md content (for available-but-not-installed plugins).</summary>
    public string? ChangelogUrl { get; init; }

    /// <summary>The PluginInfo from the host (set when installed, null when only in registry).</summary>
    public PluginInfo? PluginInfo { get; set; }

    /// <summary>The registry entry (set when the plugin is known in a registry).</summary>
    public PluginRegistryEntry? RegistryEntry { get; set; }

    /// <summary>
    /// Creates a PluginItemViewModel from a registry entry (available plugin).
    /// </summary>
    public static PluginItemViewModel FromRegistryEntry(PluginRegistryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new PluginItemViewModel(
            id: entry.Id,
            displayName: entry.DisplayName,
            description: entry.Description,
            publisher: entry.Author,
            version: entry.Version,
            license: entry.License,
            tags: entry.Tags,
            lastUpdated: entry.LastUpdated,
            registryName: entry.RegistryName,
            isOfficial: entry.IsOfficial,
            isInstalled: false)
        {
            RegistryEntry = entry,
            IconSource = entry.IconUrl,
            ReadmeUrl = entry.ReadmeUrl,
            ChangelogUrl = entry.ChangelogUrl,
        };
    }

    /// <summary>
    /// Creates a PluginItemViewModel from an installed PluginInfo.
    /// </summary>
    public static PluginItemViewModel FromPluginInfo(PluginInfo info, PluginRegistryEntry? registryEntry = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        var manifest = info.Manifest;
        var vm = new PluginItemViewModel(
            id: manifest.Id,
            displayName: manifest.DisplayName,
            description: manifest.Description,
            publisher: manifest.Author,
            version: manifest.Version,
            license: manifest.License,
            tags: manifest.Tags,
            lastUpdated: registryEntry?.LastUpdated,
            registryName: registryEntry?.RegistryName ?? string.Empty,
            isOfficial: registryEntry?.IsOfficial ?? false,
            isInstalled: true)
        {
            PluginInfo = info,
            RegistryEntry = registryEntry,
            IsEnabled = info.State != PluginState.Disabled && info.State != PluginState.Error,
            IconSource = ResolveIconPath(info, registryEntry),
        };

        return vm;
    }

    private PluginItemViewModel(
        string id,
        string displayName,
        string description,
        string publisher,
        string version,
        string license,
        IReadOnlyList<string> tags,
        string? lastUpdated,
        string registryName,
        bool isOfficial,
        bool isInstalled)
    {
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        Description = description;
        Publisher = publisher;
        Version = version;
        License = license;
        Tags = tags;
        LastUpdated = lastUpdated;
        RegistryName = registryName;
        _isOfficial = isOfficial;
        _isInstalled = isInstalled;
    }

    /// <summary>
    /// Resolves the icon path from the installed manifest or falls back to the registry URL.
    /// </summary>
    private static string? ResolveIconPath(PluginInfo info, PluginRegistryEntry? registryEntry)
    {
        // Prefer the local manifest icon (installed plugin)
        if (!string.IsNullOrWhiteSpace(info.Manifest.Icon))
            return Path.Combine(info.Directory, info.Manifest.Icon);

        // Fall back to the registry icon URL
        return registryEntry?.IconUrl;
    }

    /// <summary>
    /// Returns true if this item matches the given search query (title or tags).
    /// </summary>
    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        if (DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var tag in Tags)
        {
            if (tag.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
