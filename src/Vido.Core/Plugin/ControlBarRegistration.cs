namespace Vido.Core.Plugin;

/// <summary>
/// Represents a plugin-contributed control bar item registration.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Tooltip">Tooltip text shown on hover.</param>
/// <param name="Order">Sort order among control bar contributions.</param>
/// <param name="ViewFactory">Factory that creates the control bar view.</param>
/// <param name="OverlayFactory">Optional factory for an overlay view associated with this item.</param>
public sealed record ControlBarRegistration(
    string PluginId,
    string ContributionId,
    string Tooltip,
    int Order,
    Func<object> ViewFactory,
    Func<object>? OverlayFactory);
