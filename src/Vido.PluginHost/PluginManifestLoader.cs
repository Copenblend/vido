using System.Text.Json;
using Vido.Core.Logging;
using Vido.Core.Plugin;

namespace Vido.PluginHost;

/// <summary>
/// Loads and validates plugin manifests from <c>plugin.json</c> files.
/// </summary>
public static class PluginManifestLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads a <see cref="PluginManifest"/> from the specified plugin directory.
    /// Returns null and logs the reason if loading or validation fails.
    /// </summary>
    /// <param name="pluginDirectory">Absolute path to the directory containing <c>plugin.json</c>.</param>
    /// <param name="logger">Logger used to report warnings and errors during loading.</param>
    public static PluginManifest? Load(string pluginDirectory, ILogService logger)
    {
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");

        if (!File.Exists(manifestPath))
        {
            logger.Debug($"No plugin.json found in '{pluginDirectory}'", "PluginLoader");
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, s_jsonOptions);

            if (manifest is null)
            {
                logger.Warning($"Failed to deserialize plugin.json in '{pluginDirectory}'", "PluginLoader");
                return null;
            }

            var errors = Validate(manifest);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    logger.Warning($"Plugin '{pluginDirectory}': {error}", "PluginLoader");
                return null;
            }

            return manifest;
        }
        catch (JsonException ex)
        {
            logger.Error($"Malformed plugin.json in '{pluginDirectory}': {ex.Message}", "PluginLoader");
            return null;
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to load plugin.json from '{pluginDirectory}': {ex.Message}", "PluginLoader");
            return null;
        }
    }

    /// <summary>
    /// Validates a <see cref="PluginManifest"/> and returns a list of validation errors.
    /// An empty list means the manifest is valid.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    public static List<string> Validate(PluginManifest manifest)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
            errors.Add("Missing required field: 'id'");
        else if (!IsValidPluginId(manifest.Id))
            errors.Add($"Invalid plugin id '{manifest.Id}': must contain only lowercase letters, digits, dots, and hyphens");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            errors.Add("Missing required field: 'name'");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            errors.Add("Missing required field: 'version'");

        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
            errors.Add("Missing required field: 'entryPoint'");
        else if (!manifest.EntryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Entry point '{manifest.EntryPoint}' must be a .dll file");

        if (string.IsNullOrWhiteSpace(manifest.PluginClass))
            errors.Add("Missing required field: 'pluginClass'");

        // Validate contribution IDs are unique within the plugin
        var contributionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ValidateContributionIds(manifest.Contributes.Sidebar.Select(c => c.Id), "sidebar", contributionIds, errors);
        ValidateContributionIds(manifest.Contributes.BottomPanel.Select(c => c.Id), "bottomPanel", contributionIds, errors);
        ValidateContributionIds(manifest.Contributes.RightPanel.Select(c => c.Id), "rightPanel", contributionIds, errors);
        ValidateContributionIds(manifest.Contributes.StatusBar.Select(c => c.Id), "statusBar", contributionIds, errors);
        ValidateContributionIds(manifest.Contributes.ToolbarButtons.Select(c => c.Id), "toolbarButtons", contributionIds, errors);
        ValidateContributionIds(manifest.Contributes.ContextMenu.Select(c => c.Id), "contextMenu", contributionIds, errors);

        // Validate settings contributions
        ValidateSettings(manifest.Contributes.Settings, contributionIds, errors);

        // Validate dependencies
        ValidateDependencies(manifest.Dependencies, errors);

        return errors;
    }

    /// <summary>
    /// Checks if a plugin ID matches the allowed pattern: lowercase letters, digits, dots, hyphens.
    /// </summary>
    private static bool IsValidPluginId(string id)
    {
        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                return false;
        }
        return id.Length > 0;
    }

    private static void ValidateContributionIds(
        IEnumerable<string> ids, string section,
        HashSet<string> seen, List<string> errors)
    {
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Empty contribution id in '{section}'");
                continue;
            }

            if (!seen.Add(id))
            {
                errors.Add($"Duplicate contribution id '{id}' in '{section}'");
            }
        }
    }

    /// <summary>
    /// Validates settings contributions: id uniqueness, valid type, enum requires enumValues.
    /// </summary>
    private static void ValidateSettings(
        List<SettingContribution> settings,
        HashSet<string> seenContributionIds,
        List<string> errors)
    {
        if (settings is not { Count: > 0 })
            return;

        var seenSettingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Id))
            {
                errors.Add("Setting has empty 'id'");
                continue;
            }

            if (!seenSettingIds.Add(setting.Id))
                errors.Add($"Duplicate setting id '{setting.Id}'");

            // Settings IDs also must not collide with contribution IDs
            if (!seenContributionIds.Add(setting.Id))
                errors.Add($"Setting id '{setting.Id}' conflicts with an existing contribution id");

            if (string.IsNullOrWhiteSpace(setting.Type))
            {
                errors.Add($"Setting '{setting.Id}' has empty 'type'");
            }
            else if (!SettingContribution.ValidTypes.Contains(setting.Type))
            {
                errors.Add($"Setting '{setting.Id}' has invalid type '{setting.Type}' — must be one of: {string.Join(", ", SettingContribution.ValidTypes)}");
            }
            else if (string.Equals(setting.Type, "enum", StringComparison.OrdinalIgnoreCase)
                     && (setting.EnumValues is null || setting.EnumValues.Count == 0))
            {
                errors.Add($"Setting '{setting.Id}' is type 'enum' but has no 'enumValues'");
            }

            if (string.IsNullOrWhiteSpace(setting.Title))
                errors.Add($"Setting '{setting.Id}' has empty 'title'");
        }
    }

    /// <summary>
    /// Validates the dependencies array: each entry must have a valid plugin ID and
    /// a parseable version string, and no plugin may depend on itself.
    /// </summary>
    private static void ValidateDependencies(List<PluginDependency> dependencies, List<string> errors)
    {
        if (dependencies is not { Count: > 0 })
            return;

        var seenDepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dep in dependencies)
        {
            if (string.IsNullOrWhiteSpace(dep.Id))
            {
                errors.Add("Dependency has empty 'id'");
                continue;
            }

            if (!seenDepIds.Add(dep.Id))
                errors.Add($"Duplicate dependency on '{dep.Id}'");

            if (string.IsNullOrWhiteSpace(dep.MinVersion))
                errors.Add($"Dependency '{dep.Id}' has empty 'minVersion'");
            else if (!Version.TryParse(dep.MinVersion, out _))
                errors.Add($"Dependency '{dep.Id}' has invalid minVersion '{dep.MinVersion}' — must be a valid version (e.g. '1.0.0')");
        }
    }
}
