using NSubstitute;
using Vido.Core.Logging;
using Vido.PluginHost;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="PluginSafeInvoke"/> — safe invocation of plugin-provided code.
/// </summary>
public class PluginSafeInvokeTests
{
    private readonly ILogService _logger = Substitute.For<ILogService>();

    [Fact]
    public void SafeCreateView_SuccessfulFactory_ReturnsView()
    {
        var result = PluginSafeInvoke.SafeCreateView(
            () => "my-view", "plugin1", "panel1", _logger);

        Assert.Equal("my-view", result);
    }

    [Fact]
    public void SafeCreateView_ThrowingFactory_ReturnsFallback()
    {
        var result = PluginSafeInvoke.SafeCreateView(
            () => throw new InvalidOperationException("boom"),
            "plugin1", "panel1", _logger);

        Assert.IsType<string>(result);
        Assert.Contains("Plugin Error", (string)result);
    }

    [Fact]
    public void SafeCreateView_ThrowingFactory_LogsError()
    {
        PluginSafeInvoke.SafeCreateView(
            () => throw new Exception("oops"),
            "plugin1", "panel1", _logger);

        _logger.Received(1).Error(
            Arg.Is<string>(s => s.Contains("plugin1") && s.Contains("panel1")),
            "PluginHost");
    }

    [Fact]
    public void SafeInvoke_SuccessfulAction_Executes()
    {
        var executed = false;

        PluginSafeInvoke.SafeInvoke(
            () => executed = true, "plugin1", "onClick", _logger);

        Assert.True(executed);
    }

    [Fact]
    public void SafeInvoke_ThrowingAction_SwallowsAndLogs()
    {
        PluginSafeInvoke.SafeInvoke(
            () => throw new Exception("crash"),
            "plugin1", "onClick", _logger);

        _logger.Received(1).Error(
            Arg.Is<string>(s => s.Contains("plugin1") && s.Contains("onClick")),
            "PluginHost");
    }
}
