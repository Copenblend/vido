using Vido.Core.Keyboard;
using Vido.Core.Logging;

namespace Vido.Services.Keyboard;

/// <summary>
/// Implements the keyboard shortcut registry. Stores bindings in a dictionary
/// keyed by <see cref="KeyBinding"/> (value equality). Provides conflict detection
/// via the return value of <see cref="Register"/>, and logs warnings on conflicts.
/// </summary>
public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly ILogService _logService;

    /// <summary>
    /// Maps key combinations to (commandId, handler) pairs.
    /// </summary>
    private readonly Dictionary<KeyBinding, (string CommandId, Action Handler)> _bindings = new();

    /// <summary>
    /// Reverse lookup: commandId → KeyBinding.
    /// </summary>
    private readonly Dictionary<string, KeyBinding> _commandToBinding = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Creates a keyboard shortcut service that logs binding conflicts through the provided log service.
    /// </summary>
    /// <param name="logService">The logging service used to report shortcut conflicts and warnings.</param>
    public KeyboardShortcutService(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// Binds a key combination to a command, replacing any prior binding for the same command.
    /// Returns <c>false</c> if the key was already bound to a different command (that binding is overridden).
    /// </summary>
    /// <param name="binding">The key combination to bind.</param>
    /// <param name="commandId">The unique identifier of the command to associate with the key.</param>
    /// <param name="handler">The action to execute when the key combination is pressed.</param>
    public bool Register(KeyBinding binding, string commandId, Action handler)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(handler);

        var isConflict = false;

        // Remove any existing binding for this command ID (command may be re-bound to a new key)
        if (_commandToBinding.TryGetValue(commandId, out var oldBinding))
        {
            _bindings.Remove(oldBinding);
            _commandToBinding.Remove(commandId);
        }

        // Check if the key combination is already taken by a different command
        if (_bindings.TryGetValue(binding, out var existing))
        {
            _logService.Warning(
                $"Keyboard shortcut conflict: '{binding.DisplayString}' was bound to '{existing.CommandId}', " +
                $"now overridden by '{commandId}'",
                "Shortcuts");

            _commandToBinding.Remove(existing.CommandId);
            _bindings.Remove(binding);
            isConflict = true;
        }

        _bindings[binding] = (commandId, handler);
        _commandToBinding[commandId] = binding;

        return !isConflict;
    }

    /// <summary>
    /// Removes the key binding associated with the specified command.
    /// Returns <c>false</c> if no binding existed for the command.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command whose binding should be removed.</param>
    public bool Unregister(string commandId)
    {
        if (!_commandToBinding.TryGetValue(commandId, out var binding))
            return false;

        _bindings.Remove(binding);
        _commandToBinding.Remove(commandId);
        return true;
    }

    /// <summary>
    /// Looks up the handler registered for the given key combination and invokes it.
    /// Returns <c>false</c> if no binding matches.
    /// </summary>
    /// <param name="binding">The key combination that was pressed.</param>
    public bool TryExecute(KeyBinding binding)
    {
        if (!_bindings.TryGetValue(binding, out var entry))
            return false;

        entry.Handler();
        return true;
    }

    /// <summary>
    /// Returns the key combination currently assigned to the specified command, or <c>null</c> if the command has no binding.
    /// </summary>
    /// <param name="commandId">The unique identifier of the command to look up.</param>
    public KeyBinding? FindBinding(string commandId)
    {
        return _commandToBinding.TryGetValue(commandId, out var binding) ? binding : null;
    }

    /// <summary>
    /// Returns the identifiers of all commands that currently have a key binding registered.
    /// </summary>
    public IReadOnlyList<string> GetAllCommandIds()
    {
        return _commandToBinding.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns the command identifier bound to the given key combination, or <c>null</c> if the key is unbound.
    /// </summary>
    /// <param name="binding">The key combination to look up.</param>
    public string? GetCommandId(KeyBinding binding)
    {
        return _bindings.TryGetValue(binding, out var entry) ? entry.CommandId : null;
    }
}
