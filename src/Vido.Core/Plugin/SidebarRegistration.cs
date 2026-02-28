namespace Vido.Core.Plugin;

/// <summary>
/// Represents a sidebar panel contribution registered by a plugin.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Title">Display title shown in the UI.</param>
/// <param name="IconPath">Optional icon path relative to the plugin directory.</param>
/// <param name="Order">Sort order among sidebar contributions.</param>
/// <param name="ViewFactory">Factory that creates the sidebar panel view.</param>
public sealed record SidebarRegistration(
    string PluginId,
    string ContributionId,
    string Title,
    string? IconPath,
    int Order,
    Func<object> ViewFactory);
