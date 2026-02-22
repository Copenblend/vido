namespace Vido.Core.Plugin;

/// <summary>
/// Shared plugin directory path constants.
/// </summary>
public static class PluginPaths
{
    /// <summary>
    /// Default plugin installation directory: <c>%APPDATA%/Vido/plugins/</c>.
    /// </summary>
    public static string DefaultPluginDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Vido", "plugins");
        }
    }
}
