using Vido.Core.Keyboard;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="KeyBinding"/> — equality, hashing, display string,
/// and constructor validation.
/// </summary>
public class KeyBindingTests
{
    // ── Equality ──

    [Fact]
    public void Equals_SameKeyAndModifiers_ReturnsTrue()
    {
        var a = new KeyBinding("Space");
        var b = new KeyBinding("Space");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentKey_ReturnsFalse()
    {
        var a = new KeyBinding("Space");
        var b = new KeyBinding("S");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_CaseInsensitive()
    {
        var a = new KeyBinding("space");
        var b = new KeyBinding("Space");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_SameKeyDifferentModifiers_ReturnsFalse()
    {
        var a = new KeyBinding("B");
        var b = new KeyBinding("B", ctrl: true);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_WithAllModifiers_ReturnsTrue()
    {
        var a = new KeyBinding("O", ctrl: true, shift: true, alt: true);
        var b = new KeyBinding("O", ctrl: true, shift: true, alt: true);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var binding = new KeyBinding("Space");
        Assert.False(binding.Equals(null));
    }

    [Fact]
    public void Equals_ObjectOverload_Works()
    {
        var a = new KeyBinding("Space");
        object b = new KeyBinding("Space");
        Assert.True(a.Equals(b));
    }

    // ── GetHashCode ──

    [Fact]
    public void GetHashCode_EqualBindings_SameHashCode()
    {
        var a = new KeyBinding("Space");
        var b = new KeyBinding("Space");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_CaseInsensitive_SameHashCode()
    {
        var a = new KeyBinding("space");
        var b = new KeyBinding("SPACE");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentModifiers_DifferentHashCode()
    {
        var a = new KeyBinding("B");
        var b = new KeyBinding("B", ctrl: true);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    // ── DisplayString ──

    [Fact]
    public void DisplayString_KeyOnly()
    {
        Assert.Equal("Space", new KeyBinding("Space").DisplayString);
    }

    [Fact]
    public void DisplayString_CtrlKey()
    {
        Assert.Equal("Ctrl+B", new KeyBinding("B", ctrl: true).DisplayString);
    }

    [Fact]
    public void DisplayString_CtrlShiftKey()
    {
        Assert.Equal("Ctrl+Shift+O", new KeyBinding("O", ctrl: true, shift: true).DisplayString);
    }

    [Fact]
    public void DisplayString_AltKey()
    {
        Assert.Equal("Alt+F4", new KeyBinding("F4", alt: true).DisplayString);
    }

    [Fact]
    public void DisplayString_AllModifiers()
    {
        Assert.Equal("Ctrl+Alt+Shift+X", new KeyBinding("X", ctrl: true, shift: true, alt: true).DisplayString);
    }

    // ── ToString ──

    [Fact]
    public void ToString_MatchesDisplayString()
    {
        var binding = new KeyBinding("B", ctrl: true);
        Assert.Equal(binding.DisplayString, binding.ToString());
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new KeyBinding(null!));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var binding = new KeyBinding("O", ctrl: true, shift: true, alt: true);
        Assert.Equal("O", binding.Key);
        Assert.True(binding.Ctrl);
        Assert.True(binding.Shift);
        Assert.True(binding.Alt);
    }

    [Fact]
    public void Constructor_DefaultModifiersFalse()
    {
        var binding = new KeyBinding("Space");
        Assert.False(binding.Ctrl);
        Assert.False(binding.Shift);
        Assert.False(binding.Alt);
    }

    // ── Dictionary key behavior ──

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<KeyBinding, string>
        {
            [new KeyBinding("Space")] = "playPause",
            [new KeyBinding("B", ctrl: true)] = "toggleSidebar"
        };

        Assert.Equal("playPause", dict[new KeyBinding("Space")]);
        Assert.Equal("toggleSidebar", dict[new KeyBinding("B", ctrl: true)]);
    }

    [Fact]
    public void DictionaryKey_CaseInsensitive()
    {
        var dict = new Dictionary<KeyBinding, string>
        {
            [new KeyBinding("space")] = "cmd"
        };

        Assert.True(dict.ContainsKey(new KeyBinding("SPACE")));
    }
}
