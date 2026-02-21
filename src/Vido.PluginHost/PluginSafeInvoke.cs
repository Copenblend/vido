using Vido.Core.Logging;

namespace Vido.PluginHost;

/// <summary>
/// Utility methods for safe interaction with plugin-provided code.
/// All methods catch exceptions from plugin code to prevent host crashes.
/// </summary>
public static class PluginSafeInvoke
{
    /// <summary>
    /// Safely invokes a plugin-provided view factory. If the factory throws,
    /// logs the error and returns a fallback placeholder string.
    /// </summary>
    /// <param name="viewFactory">Factory function provided by a plugin.</param>
    /// <param name="pluginId">Plugin id for logging.</param>
    /// <param name="contributionId">Contribution id for logging.</param>
    /// <param name="logger">Logger service.</param>
    /// <returns>The view object, or a fallback error placeholder.</returns>
    public static object SafeCreateView(
        Func<object> viewFactory,
        string pluginId,
        string contributionId,
        ILogService logger)
    {
        try
        {
            return viewFactory();
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Plugin '{pluginId}' view factory for '{contributionId}' threw: {ex.Message}",
                "PluginHost");
            return $"[Plugin Error: {pluginId}/{contributionId}]";
        }
    }

    /// <summary>
    /// Safely invokes a plugin-provided action. If it throws, logs the error and swallows it.
    /// </summary>
    public static void SafeInvoke(
        Action action,
        string pluginId,
        string context,
        ILogService logger)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Plugin '{pluginId}' threw in {context}: {ex.Message}",
                "PluginHost");
        }
    }
}
