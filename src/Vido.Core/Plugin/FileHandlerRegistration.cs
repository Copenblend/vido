using Vido.Core.FileSystem;

namespace Vido.Core.Plugin;

/// <summary>
/// Represents a plugin-provided file open handler registration.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="Extensions">Handled file extensions.</param>
/// <param name="Handler">Callback invoked for matching file nodes.</param>
public sealed record FileHandlerRegistration(
    string PluginId,
    string[] Extensions,
    Action<FileNode> Handler);
