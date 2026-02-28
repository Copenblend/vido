namespace Vido.Core.Plugin;

/// <summary>
/// Represents a bottom or right panel tab contribution registered by a plugin.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Title">Display title shown on the panel tab.</param>
/// <param name="Order">Sort order among panel contributions.</param>
/// <param name="ViewFactory">Factory that creates the panel view.</param>
public sealed record PanelRegistration(
    string PluginId,
    string ContributionId,
    string Title,
    int Order,
    Func<object> ViewFactory);
