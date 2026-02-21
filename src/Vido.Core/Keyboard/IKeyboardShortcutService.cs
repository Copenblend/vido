namespace Vido.Core.Keyboard;

/// <summary>
/// Manages keyboard shortcut registrations. Allows registering, unregistering,
/// and executing key bindings. Supports conflict detection and text-input suppression.
/// </summary>
public interface IKeyboardShortcutService
{
    /// <summary>
    /// Registers a keyboard shortcut. If a binding with the same key combination
    /// already exists, it is replaced and the previous handler is discarded.
    /// </summary>
    /// <param name="binding">The key combination to bind.</param>
    /// <param name="commandId">A unique identifier for this shortcut (e.g., "vido.playPause").</param>
    /// <param name="handler">The action to execute when the shortcut is triggered.</param>
    /// <returns>True if registered successfully; false if it replaced an existing binding (conflict).</returns>
    bool Register(KeyBinding binding, string commandId, Action handler);

    /// <summary>
    /// Unregisters a keyboard shortcut by its command ID.
    /// </summary>
    /// <param name="commandId">The command ID to remove.</param>
    /// <returns>True if the command was found and removed.</returns>
    bool Unregister(string commandId);

    /// <summary>
    /// Attempts to execute a shortcut matching the given key combination.
    /// </summary>
    /// <param name="binding">The key combination pressed.</param>
    /// <returns>True if a matching shortcut was found and executed.</returns>
    bool TryExecute(KeyBinding binding);

    /// <summary>
    /// Finds the key binding associated with a given command ID, or null if none.
    /// </summary>
    KeyBinding? FindBinding(string commandId);

    /// <summary>
    /// Gets all registered shortcut command IDs.
    /// </summary>
    IReadOnlyList<string> GetAllCommandIds();

    /// <summary>
    /// Gets the command ID associated with a key binding, or null if not bound.
    /// </summary>
    string? GetCommandId(KeyBinding binding);
}
