namespace Vido.Core.Plugin;

/// <summary>
/// Entry point for all Vido plugins. Implement this interface and declare
/// the fully-qualified class name in plugin.json's "pluginClass" field.
/// </summary>
public interface IVidoPlugin
{
    /// <summary>
    /// Called when the plugin is activated. Use the context to register
    /// event handlers, contribute UI elements, and access Vido services.
    /// </summary>
    void Activate(IPluginContext context);

    /// <summary>
    /// Called when the plugin is deactivated (app shutdown or manual disable).
    /// Clean up any resources, unsubscribe from events, close connections.
    /// </summary>
    void Deactivate();
}
