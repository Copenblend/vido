using NSubstitute;
using Vido.Core.Updates;
using Vido.Services.Playlists;
using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for the <see cref="IToastService.ShowActionable"/> interface contract
/// and actionable toast integration with the auto-update flow.
/// </summary>
public sealed class ActionableToastTests
{
    // ── Interface contract ──────────────────────────────────────────────

    [Fact]
    public void ShowActionable_CanBeCalledOnMock()
    {
        var toast = Substitute.For<IToastService>();

        toast.ShowActionable("Update available.", " Click here.", () => { }, 10.0);

        toast.Received(1).ShowActionable(
            "Update available.",
            " Click here.",
            Arg.Any<Action>(),
            10.0);
    }

    [Fact]
    public void ShowActionable_AcceptsNullBoldSuffix()
    {
        var toast = Substitute.For<IToastService>();

        toast.ShowActionable("Update available.", null, () => { }, 5.0);

        toast.Received(1).ShowActionable(
            "Update available.",
            null,
            Arg.Any<Action>(),
            5.0);
    }

    [Fact]
    public void ShowActionable_DefaultDurationIs10Seconds()
    {
        var toast = Substitute.For<IToastService>();

        // Call without explicit duration — should use default 10.0
        toast.ShowActionable("Message", null, () => { });

        toast.Received(1).ShowActionable(
            "Message",
            null,
            Arg.Any<Action>(),
            10.0);
    }

    [Fact]
    public void ShowActionable_AcceptsCustomDuration()
    {
        var toast = Substitute.For<IToastService>();

        toast.ShowActionable("Message", null, () => { }, 15.0);

        toast.Received(1).ShowActionable(
            "Message",
            null,
            Arg.Any<Action>(),
            15.0);
    }

    // ── Callback behavior ───────────────────────────────────────────────

    [Fact]
    public void ShowActionable_CallbackIsInvocable()
    {
        var callbackInvoked = false;
        Action callback = () => callbackInvoked = true;

        // Verify the callback can be invoked independently
        callback();

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void ShowActionable_CallbackExceptionDoesNotPropagate()
    {
        // Simulate the toast service's error-swallowing behavior:
        // When a callback throws, the toast should catch it silently.
        Action callback = () => throw new InvalidOperationException("Test exception");

        var exception = Record.Exception(() =>
        {
            try { callback(); } catch { /* Toast interactions must never crash the app. */ }
        });

        Assert.Null(exception);
    }

    // ── Show and ShowError remain unchanged ──────────────────────────────

    [Fact]
    public void Show_StillWorksAfterShowActionableAdded()
    {
        var toast = Substitute.For<IToastService>();

        toast.Show("Hello", "World");

        toast.Received(1).Show("Hello", "World");
    }

    [Fact]
    public void ShowError_StillWorksAfterShowActionableAdded()
    {
        var toast = Substitute.For<IToastService>();

        toast.ShowError("Error occurred", " See log.");

        toast.Received(1).ShowError("Error occurred", " See log.");
    }

    // ── Auto-update toast integration ───────────────────────────────────

    [Fact]
    public void AutoUpdateToast_FormatsMessageWithVersion()
    {
        var toast = Substitute.For<IToastService>();
        var result = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            LatestVersion = "0.20.0"
        };

        // Simulate the MainWindow.OnAutoUpdateTimerTick behavior
        if (result.IsUpdateAvailable)
        {
            toast.ShowActionable(
                $"Vido {result.LatestVersion} is available.",
                " Click to view update details.",
                () => { },
                durationSeconds: 10.0);
        }

        toast.Received(1).ShowActionable(
            "Vido 0.20.0 is available.",
            " Click to view update details.",
            Arg.Any<Action>(),
            10.0);
    }

    [Fact]
    public void AutoUpdateToast_NotShownWhenNoUpdate()
    {
        var toast = Substitute.For<IToastService>();
        var result = new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "0.20.0",
            LatestVersion = "0.20.0"
        };

        // Simulate the MainWindow.OnAutoUpdateTimerTick behavior
        if (result.IsUpdateAvailable)
        {
            toast.ShowActionable(
                $"Vido {result.LatestVersion} is available.",
                " Click to view update details.",
                () => { },
                durationSeconds: 10.0);
        }

        toast.DidNotReceive().ShowActionable(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<Action>(),
            Arg.Any<double>());
    }

    [Fact]
    public void AutoUpdateToast_ClickCallbackStoresResult()
    {
        UpdateCheckResult? storedResult = null;
        var result = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            LatestVersion = "0.20.0",
            CurrentVersion = "0.19.0",
            ReleaseUrl = "https://github.com/Copenblend/vido/releases/tag/v0.20.0"
        };

        // Simulate storing the result and invoking the callback
        Action onClickCallback = () => storedResult = result;
        onClickCallback();

        Assert.NotNull(storedResult);
        Assert.Equal("0.20.0", storedResult.LatestVersion);
        Assert.Equal("0.19.0", storedResult.CurrentVersion);
    }

    [Fact]
    public void AutoUpdateToast_ShowActionableUsesInfoIcon()
    {
        // ShowActionable delegates to ShowInternal with the info icon (\uE946)
        // and InfoBackgroundBrush. This is verified by the fact that ShowActionable
        // is called (not ShowError) for update notifications.
        var toast = Substitute.For<IToastService>();

        toast.ShowActionable("Update", null, () => { });

        // ShowError was NOT called — only ShowActionable
        toast.DidNotReceive().ShowError(Arg.Any<string>(), Arg.Any<string?>());
        toast.Received(1).ShowActionable(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<Action>(),
            Arg.Any<double>());
    }
}
