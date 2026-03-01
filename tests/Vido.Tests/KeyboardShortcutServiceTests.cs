using NSubstitute;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Services.Keyboard;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="KeyboardShortcutService"/> — registration, execution,
/// conflict detection, unregistration, and lookup operations.
/// </summary>
public class KeyboardShortcutServiceTests
{
    private readonly ILogService _logService;
    private readonly KeyboardShortcutService _sut;

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public KeyboardShortcutServiceTests()
    {
        _logService = Substitute.For<ILogService>();
        _sut = new KeyboardShortcutService(_logService);
    }

    // ── Registration ──

    /// <summary>
    /// Verifies that Register new binding returns true.
    /// </summary>
    [Fact]
    public void Register_NewBinding_ReturnsTrue()
    {
        var result = _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });
        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Register same command id updates binding.
    /// </summary>
    [Fact]
    public void Register_SameCommandId_UpdatesBinding()
    {
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });
        var result = _sut.Register(new KeyBinding("S"), "test.cmd", () => { });

        Assert.True(result);
        var binding = _sut.FindBinding("test.cmd");
        Assert.NotNull(binding);
        Assert.Equal("S", binding.Key);
    }

    /// <summary>
    /// Verifies that Register conflicting key returns false.
    /// </summary>
    [Fact]
    public void Register_ConflictingKey_ReturnsFalse()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });
        var result = _sut.Register(new KeyBinding("Space"), "cmd.b", () => { });

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Register conflicting key logs warning.
    /// </summary>
    [Fact]
    public void Register_ConflictingKey_LogsWarning()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });
        _sut.Register(new KeyBinding("Space"), "cmd.b", () => { });

        _logService.Received(1).Warning(
            Arg.Is<string>(s => s.Contains("cmd.a") && s.Contains("cmd.b")),
            "Shortcuts");
    }

    /// <summary>
    /// Verifies that Register conflicting key new command takes precedence.
    /// </summary>
    [Fact]
    public void Register_ConflictingKey_NewCommandTakesPrecedence()
    {
        var executed = "";
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => executed = "a");
        _sut.Register(new KeyBinding("Space"), "cmd.b", () => executed = "b");

        _sut.TryExecute(new KeyBinding("Space"));
        Assert.Equal("b", executed);
    }

    /// <summary>
    /// Verifies that Register with modifiers returns true.
    /// </summary>
    [Fact]
    public void Register_WithModifiers_ReturnsTrue()
    {
        var result = _sut.Register(new KeyBinding("O", ctrl: true, shift: true), "test.open", () => { });
        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Register null binding throws argument null exception.
    /// </summary>
    [Fact]
    public void Register_NullBinding_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.Register(null!, "cmd", () => { }));
    }

    /// <summary>
    /// Verifies that Register null command id throws argument null exception.
    /// </summary>
    [Fact]
    public void Register_NullCommandId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.Register(new KeyBinding("Space"), null!, () => { }));
    }

    /// <summary>
    /// Verifies that Register null handler throws argument null exception.
    /// </summary>
    [Fact]
    public void Register_NullHandler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.Register(new KeyBinding("Space"), "cmd", null!));
    }

    // ── Execution ──

    /// <summary>
    /// Verifies that Try Execute registered binding executes and returns true.
    /// </summary>
    [Fact]
    public void TryExecute_RegisteredBinding_ExecutesAndReturnsTrue()
    {
        var executed = false;
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => executed = true);

        var result = _sut.TryExecute(new KeyBinding("Space"));

        Assert.True(result);
        Assert.True(executed);
    }

    /// <summary>
    /// Verifies that Try Execute unregistered binding returns false.
    /// </summary>
    [Fact]
    public void TryExecute_UnregisteredBinding_ReturnsFalse()
    {
        var result = _sut.TryExecute(new KeyBinding("Space"));
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Try Execute with modifiers matches correct binding.
    /// </summary>
    [Fact]
    public void TryExecute_WithModifiers_MatchesCorrectBinding()
    {
        var executed = "";
        _sut.Register(new KeyBinding("B"), "cmd.b", () => executed = "b");
        _sut.Register(new KeyBinding("B", ctrl: true), "cmd.ctrlb", () => executed = "ctrlb");

        _sut.TryExecute(new KeyBinding("B", ctrl: true));
        Assert.Equal("ctrlb", executed);
    }

    /// <summary>
    /// Verifies that Try Execute wrong modifiers returns false.
    /// </summary>
    [Fact]
    public void TryExecute_WrongModifiers_ReturnsFalse()
    {
        _sut.Register(new KeyBinding("B", ctrl: true), "cmd.ctrlb", () => { });

        var result = _sut.TryExecute(new KeyBinding("B"));
        Assert.False(result);
    }

    // ── Unregistration ──

    /// <summary>
    /// Verifies that Unregister existing command returns true.
    /// </summary>
    [Fact]
    public void Unregister_ExistingCommand_ReturnsTrue()
    {
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });
        var result = _sut.Unregister("test.cmd");
        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Unregister nonexistent command returns false.
    /// </summary>
    [Fact]
    public void Unregister_NonexistentCommand_ReturnsFalse()
    {
        var result = _sut.Unregister("nonexistent");
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Unregister binding no longer executes.
    /// </summary>
    [Fact]
    public void Unregister_BindingNoLongerExecutes()
    {
        var executed = false;
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => executed = true);
        _sut.Unregister("test.cmd");

        var result = _sut.TryExecute(new KeyBinding("Space"));
        Assert.False(result);
        Assert.False(executed);
    }

    /// <summary>
    /// Verifies that Unregister frees key for reuse.
    /// </summary>
    [Fact]
    public void Unregister_FreesKeyForReuse()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });
        _sut.Unregister("cmd.a");

        var result = _sut.Register(new KeyBinding("Space"), "cmd.b", () => { });
        Assert.True(result); // No conflict since cmd.a was unregistered
    }

    // ── Lookup ──

    /// <summary>
    /// Verifies that Find Binding registered command returns binding.
    /// </summary>
    [Fact]
    public void FindBinding_RegisteredCommand_ReturnsBinding()
    {
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });

        var binding = _sut.FindBinding("test.cmd");

        Assert.NotNull(binding);
        Assert.Equal("Space", binding.Key);
    }

    /// <summary>
    /// Verifies that Find Binding nonexistent command returns null.
    /// </summary>
    [Fact]
    public void FindBinding_NonexistentCommand_ReturnsNull()
    {
        Assert.Null(_sut.FindBinding("nonexistent"));
    }

    /// <summary>
    /// Verifies that Get Command Id registered binding returns id.
    /// </summary>
    [Fact]
    public void GetCommandId_RegisteredBinding_ReturnsId()
    {
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });

        var id = _sut.GetCommandId(new KeyBinding("Space"));
        Assert.Equal("test.cmd", id);
    }

    /// <summary>
    /// Verifies that Get Command Id unregistered binding returns null.
    /// </summary>
    [Fact]
    public void GetCommandId_UnregisteredBinding_ReturnsNull()
    {
        Assert.Null(_sut.GetCommandId(new KeyBinding("Space")));
    }

    /// <summary>
    /// Verifies that Get All Command Ids returns registered ids.
    /// </summary>
    [Fact]
    public void GetAllCommandIds_ReturnsRegisteredIds()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });
        _sut.Register(new KeyBinding("S"), "cmd.b", () => { });
        _sut.Register(new KeyBinding("M"), "cmd.c", () => { });

        var ids = _sut.GetAllCommandIds();
        Assert.Equal(3, ids.Count);
        Assert.Contains("cmd.a", ids);
        Assert.Contains("cmd.b", ids);
        Assert.Contains("cmd.c", ids);
    }

    /// <summary>
    /// Verifies that Get All Command Ids after unregister excludes removed.
    /// </summary>
    [Fact]
    public void GetAllCommandIds_AfterUnregister_ExcludesRemoved()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });
        _sut.Register(new KeyBinding("S"), "cmd.b", () => { });
        _sut.Unregister("cmd.a");

        var ids = _sut.GetAllCommandIds();
        Assert.Single(ids);
        Assert.Equal("cmd.b", ids[0]);
    }

    /// <summary>
    /// Verifies that GetAllCommandIds returns the same cached snapshot instance until bindings change.
    /// </summary>
    [Fact]
    public void GetAllCommandIds_ReturnsCachedSnapshot()
    {
        _sut.Register(new KeyBinding("Space"), "cmd.a", () => { });

        var first = _sut.GetAllCommandIds();
        var second = _sut.GetAllCommandIds();

        Assert.Same(first, second);
    }

    // ── Case insensitivity ──

    /// <summary>
    /// Verifies that Key Binding is case insensitive.
    /// </summary>
    [Fact]
    public void KeyBinding_IsCaseInsensitive()
    {
        _sut.Register(new KeyBinding("space"), "test.cmd", () => { });

        var result = _sut.TryExecute(new KeyBinding("Space"));
        Assert.True(result);
    }

    /// <summary>
    /// Verifies that Command Id is case insensitive.
    /// </summary>
    [Fact]
    public void CommandId_IsCaseInsensitive()
    {
        _sut.Register(new KeyBinding("Space"), "Test.Cmd", () => { });

        var binding = _sut.FindBinding("test.cmd");
        Assert.NotNull(binding);
    }

    // ── Re-binding same command to new key ──

    /// <summary>
    /// Verifies that Register same command new key old key freed.
    /// </summary>
    [Fact]
    public void Register_SameCommandNewKey_OldKeyFreed()
    {
        _sut.Register(new KeyBinding("Space"), "test.cmd", () => { });
        _sut.Register(new KeyBinding("S"), "test.cmd", () => { });

        // Old key should no longer execute
        var result = _sut.TryExecute(new KeyBinding("Space"));
        Assert.False(result);

        // New key should work
        result = _sut.TryExecute(new KeyBinding("S"));
        Assert.True(result);
    }
}