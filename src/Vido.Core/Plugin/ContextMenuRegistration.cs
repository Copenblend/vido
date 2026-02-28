using Vido.Core.FileSystem;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents a plugin-contributed file context menu item registration.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="ContributionId">Contribution identifier within the plugin manifest.</param>
/// <param name="Label">Display label shown in the context menu.</param>
/// <param name="FileExtensions">File extensions that this menu item applies to.</param>
/// <param name="Order">Sort order among context menu contributions.</param>
/// <param name="Handler">Callback invoked with the target file node.</param>
public sealed record ContextMenuRegistration(
    string PluginId,
    string ContributionId,
    string Label,
    string[] FileExtensions,
    int Order,
    Action<FileNode> Handler);
