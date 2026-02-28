namespace Vido.Core.Plugin;

/// <summary>
/// Represents a status bar item contribution registered by a plugin.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Name">Display name shown in status bar visibility menus.</param>
/// <param name="Position">Desired position in the status bar (typically left or right).</param>
/// <param name="Order">Sort order among status bar contributions.</param>
/// <param name="ViewFactory">Factory that creates the status bar view element.</param>
public sealed record StatusBarRegistration(
    string PluginId,
    string ContributionId,
    string Name,
    string Position,
    int Order,
    Func<object> ViewFactory);
