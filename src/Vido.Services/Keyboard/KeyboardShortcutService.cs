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

    /// <summary>Maps key combinations to (commandId, handler) pairs.</summary>
    private readonly Dictionary<KeyBinding, (string CommandId, Action Handler)> _bindings = new();

    /// <summary>Reverse lookup: commandId → KeyBinding.</summary>
    private readonly Dictionary<string, KeyBinding> _commandToBinding = new(StringComparer.OrdinalIgnoreCase);

    public KeyboardShortcutService(ILogService logService)
    {
        _logService = logService;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool Unregister(string commandId)
    {
        if (!_commandToBinding.TryGetValue(commandId, out var binding))
            return false;

        _bindings.Remove(binding);
        _commandToBinding.Remove(commandId);
        return true;
    }

    /// <inheritdoc/>
    public bool TryExecute(KeyBinding binding)
    {
        if (!_bindings.TryGetValue(binding, out var entry))
            return false;

        entry.Handler();
        return true;
    }

    /// <inheritdoc/>
    public KeyBinding? FindBinding(string commandId)
    {
        return _commandToBinding.TryGetValue(commandId, out var binding) ? binding : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAllCommandIds()
    {
        return _commandToBinding.Keys.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public string? GetCommandId(KeyBinding binding)
    {
        return _bindings.TryGetValue(binding, out var entry) ? entry.CommandId : null;
    }
}
