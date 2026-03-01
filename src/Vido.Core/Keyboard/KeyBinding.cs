namespace Vido.Core.Keyboard;

/// <summary>
/// Represents a keyboard shortcut binding — a key plus optional modifier keys.
/// Used as a dictionary key in the shortcut registry; implements value equality.
/// </summary>
public sealed class KeyBinding : IEquatable<KeyBinding>
{
    /// <summary>
    /// The primary key (e.g., "Space", "F11", "O", "B").
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Whether Ctrl must be held.
    /// </summary>
    public bool Ctrl { get; }

    /// <summary>
    /// Whether Shift must be held.
    /// </summary>
    public bool Shift { get; }

    /// <summary>
    /// Whether Alt must be held.
    /// </summary>
    public bool Alt { get; }

    private readonly string _displayString;
    
    /// <summary>
    /// Creates a key binding from a primary key and optional modifier flags,
    /// pre-computing the human-readable display string (e.g. "Ctrl+Shift+O").
    /// </summary>
    /// <param name="key">The primary key name (e.g. "Space", "F11", "O").</param>
    /// <param name="ctrl">Whether the Ctrl modifier is required.</param>
    /// <param name="shift">Whether the Shift modifier is required.</param>
    /// <param name="alt">Whether the Alt modifier is required.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
    public KeyBinding(string key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;

        _displayString = BuildDisplayString(key, ctrl, shift, alt);
    }

    private static string BuildDisplayString(string key, bool ctrl, bool shift, bool alt)
    {
        if (!ctrl && !shift && !alt)
            return key;

        return string.Create(
            null,
            stackalloc char[64],
            $"{(ctrl ? "Ctrl+" : "")}{(alt ? "Alt+" : "")}{(shift ? "Shift+" : "")}{key}");
    }

    /// <summary>
    /// Returns a human-readable display string (e.g., "Ctrl+Shift+O").
    /// Matches the format used in menu InputGestureText.
    /// </summary>
    public string DisplayString => _displayString;

    /// <summary>
    /// Determines whether this binding has the same key and modifiers as another.
    /// Comparison is case-insensitive on the key name.
    /// </summary>
    /// <param name="other">The other key binding to compare against.</param>
    public bool Equals(KeyBinding? other)
    {
        if (other is null) return false;
        return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase)
               && Ctrl == other.Ctrl
               && Shift == other.Shift
               && Alt == other.Alt;
    }

    /// <summary>
    /// Determines whether the specified object is a <see cref="KeyBinding"/> with
    /// the same key and modifiers as this instance.
    /// </summary>
    /// <param name="obj">The object to compare with this binding.</param>
    public override bool Equals(object? obj) => Equals(obj as KeyBinding);

    /// <summary>
    /// Returns a hash code derived from the upper-cased key name and modifier flags,
    /// consistent with <see cref="Equals(KeyBinding?)"/>.
    /// </summary>
    public override int GetHashCode()
        => HashCode.Combine(Key.ToUpperInvariant(), Ctrl, Shift, Alt);

    /// <summary>
    /// Returns the human-readable display string (e.g. "Ctrl+Shift+O").
    /// </summary>
    public override string ToString() => DisplayString;
}
