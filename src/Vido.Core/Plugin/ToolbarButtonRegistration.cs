namespace Vido.Core.Plugin;

/// <summary>
/// Represents a title bar toolbar button contribution registered by a plugin.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Tooltip">Tooltip text shown for the button.</param>
/// <param name="IconPath">Optional icon path relative to the plugin directory.</param>
/// <param name="Order">Sort order among toolbar contributions.</param>
/// <param name="ClickHandler">Action executed when the button is clicked.</param>
public sealed record ToolbarButtonRegistration(
    string PluginId,
    string ContributionId,
    string Tooltip,
    string? IconPath,
    int Order,
    Action ClickHandler);
