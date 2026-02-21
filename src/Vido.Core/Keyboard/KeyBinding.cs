namespace Vido.Core.Keyboard;

/// <summary>
/// Represents a keyboard shortcut binding — a key plus optional modifier keys.
/// Used as a dictionary key in the shortcut registry; implements value equality.
/// </summary>
public sealed class KeyBinding : IEquatable<KeyBinding>
{
    /// <summary>The primary key (e.g., "Space", "F11", "O", "B").</summary>
    public string Key { get; }

    /// <summary>Whether Ctrl must be held.</summary>
    public bool Ctrl { get; }

    /// <summary>Whether Shift must be held.</summary>
    public bool Shift { get; }

    /// <summary>Whether Alt must be held.</summary>
    public bool Alt { get; }

    public KeyBinding(string key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;
    }

    /// <summary>
    /// Returns a human-readable display string (e.g., "Ctrl+Shift+O").
    /// Matches the format used in menu InputGestureText.
    /// </summary>
    public string DisplayString
    {
        get
        {
            var parts = new List<string>(4);
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(Key);
            return string.Join("+", parts);
        }
    }

    public bool Equals(KeyBinding? other)
    {
        if (other is null) return false;
        return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase)
               && Ctrl == other.Ctrl
               && Shift == other.Shift
               && Alt == other.Alt;
    }

    public override bool Equals(object? obj) => Equals(obj as KeyBinding);

    public override int GetHashCode()
        => HashCode.Combine(Key.ToUpperInvariant(), Ctrl, Shift, Alt);

    public override string ToString() => DisplayString;
}
