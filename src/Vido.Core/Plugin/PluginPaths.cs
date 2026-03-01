namespace Vido.Core.Plugin;

/// <summary>
/// Shared plugin directory path constants.
/// </summary>
public static class PluginPaths
{
    private static readonly string _defaultPluginDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vido",
            "plugins");

    /// <summary>
    /// Default plugin installation directory: <c>%APPDATA%/Vido/plugins/</c>.
    /// Cached on first access because <c>%APPDATA%</c> does not change during process lifetime.
    /// </summary>
    public static string DefaultPluginDirectory => _defaultPluginDirectory;
}
