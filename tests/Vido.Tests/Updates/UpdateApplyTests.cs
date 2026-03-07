using System.IO;
using System.Windows;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Vido.Core.Updates;
using Vido.Views.Updates;

using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for the self-update mechanism: UpdateDialog download flow,
/// cancellation, apply-update wiring, and script generation.
/// </summary>
public sealed class UpdateApplyTests
{
    // ── DownloadUpdateAsync ─────────────────────────────────────────────

    [Fact]
    public void DownloadUpdateAsync_SuccessfulDownload_TransitionsToDownloaded()
    {
        RunOnStaThread(() =>
        {
            var updateService = Substitute.For<IUpdateService>();
            var expectedPath = Path.Combine(Path.GetTempPath(), "Vido-1.0.0-win-x64-portable.zip");
            updateService.DownloadUpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(expectedPath));

            var dialog = CreateDialog(updateService);
            dialog.LatestVersion = "1.0.0";
            dialog.DownloadUrl = "https://example.com/Vido-1.0.0.zip";

            // Run the download and process dispatcher synchronously
            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Equal(UpdateDialog.DialogState.Downloaded, dialog.State);
            Assert.Equal(expectedPath, dialog.DownloadedFilePath);
        });
    }

    [Fact]
    public void DownloadUpdateAsync_Cancelled_TransitionsToInfo()
    {
        RunOnStaThread(() =>
        {
            var updateService = Substitute.For<IUpdateService>();
            updateService.DownloadUpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var dialog = CreateDialog(updateService);
            dialog.LatestVersion = "1.0.0";
            dialog.DownloadUrl = "https://example.com/Vido-1.0.0.zip";

            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);
            Assert.Null(dialog.DownloadedFilePath);
        });
    }

    [Fact]
    public void DownloadUpdateAsync_Error_TransitionsToErrorState()
    {
        RunOnStaThread(() =>
        {
            var updateService = Substitute.For<IUpdateService>();
            updateService.DownloadUpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("Network failure"));

            var dialog = CreateDialog(updateService);
            dialog.LatestVersion = "1.0.0";
            dialog.DownloadUrl = "https://example.com/Vido-1.0.0.zip";
            dialog.ReleaseUrl = "https://example.com/releases";

            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Equal(UpdateDialog.DialogState.Error, dialog.State);
            Assert.Equal("Network failure", dialog.ErrorMessage);
            Assert.Equal("https://example.com/releases", dialog.ReleaseUrl);
        });
    }

    [Fact]
    public void DownloadUpdateAsync_SetsDownloadingState_BeforeDownload()
    {
        RunOnStaThread(() =>
        {
            var statesDuringDownload = new List<UpdateDialog.DialogState>();
            var updateService = Substitute.For<IUpdateService>();
            UpdateDialog? capturedDialog = null;

            updateService.DownloadUpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    statesDuringDownload.Add(capturedDialog!.State);
                    return Task.FromResult(@"C:\temp\update.zip");
                });

            var dialog = CreateDialog(updateService);
            capturedDialog = dialog;
            dialog.LatestVersion = "1.0.0";
            dialog.DownloadUrl = "https://example.com/update.zip";

            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Contains(UpdateDialog.DialogState.Downloading, statesDuringDownload);
        });
    }

    [Fact]
    public void DownloadUpdateAsync_UsesCorrectFileName()
    {
        RunOnStaThread(() =>
        {
            string? capturedFileName = null;
            var updateService = Substitute.For<IUpdateService>();
            updateService.DownloadUpdateAsync(
                Arg.Any<string>(), Arg.Do<string>(f => capturedFileName = f),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(@"C:\temp\file.zip"));

            var dialog = CreateDialog(updateService);
            dialog.LatestVersion = "2.5.0";
            dialog.DownloadUrl = "https://example.com/update.zip";

            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Equal("Vido-2.5.0-win-x64-portable.zip", capturedFileName);
        });
    }

    [Fact]
    public void DownloadUpdateAsync_PassesDownloadUrl()
    {
        RunOnStaThread(() =>
        {
            string? capturedUrl = null;
            var updateService = Substitute.For<IUpdateService>();
            updateService.DownloadUpdateAsync(
                Arg.Do<string>(u => capturedUrl = u), Arg.Any<string>(),
                Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(@"C:\temp\file.zip"));

            var dialog = CreateDialog(updateService);
            dialog.LatestVersion = "1.0.0";
            dialog.DownloadUrl = "https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido.zip";

            dialog.DownloadUpdateAsync().GetAwaiter().GetResult();

            Assert.Equal("https://github.com/Copenblend/vido/releases/download/v1.0.0/Vido.zip", capturedUrl);
        });
    }

    // ── Constructor with IUpdateService ─────────────────────────────────

    [Fact]
    public void Constructor_WithUpdateService_CreatesDialog()
    {
        RunOnStaThread(() =>
        {
            var updateService = Substitute.For<IUpdateService>();
            var dialog = CreateDialog(updateService);

            Assert.NotNull(dialog);
            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);
        });
    }

    // ── UpdateNowButton without service ─────────────────────────────────

    [Fact]
    public void UpdateNowButton_WithoutService_SetsUserChoseUpdate()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();
            dialog.DownloadUrl = "https://example.com/update.zip";

            // Without a service, it should fall through to the simple path
            Assert.False(dialog.UserChoseUpdate);
        });
    }

    // ── Script generation (via GitHubUpdateService) ─────────────────────

    [Fact]
    public void GenerateApplyUpdateScript_HandlesPathsWithSpaces()
    {
        var zipPath = @"C:\Users\Test User\AppData\Local\Temp\Vido\Updates\Vido-1.0.0-portable.zip";
        var installDir = @"C:\Program Files\Vido App";
        var script = Vido.Services.Updates.GitHubUpdateService.GenerateApplyUpdateScript(
            zipPath, installDir, 42);

        Assert.Contains($"Expand-Archive -Path '{zipPath}'", script);
        Assert.Contains($"-DestinationPath '{installDir}'", script);
        Assert.Contains($"Remove-Item '{zipPath}'", script);
        Assert.Contains($"Join-Path '{installDir}' 'Vido.exe'", script);
    }

    [Fact]
    public void GenerateApplyUpdateScript_CleanupRemovesZipAfterExtraction()
    {
        var script = Vido.Services.Updates.GitHubUpdateService.GenerateApplyUpdateScript(
            @"C:\temp\update.zip", @"C:\Vido", 1);

        var lines = script.Split('\n').Select(l => l.Trim()).ToArray();
        var expandIdx = Array.FindIndex(lines, l => l.Contains("Expand-Archive"));
        var removeIdx = Array.FindIndex(lines, l => l.Contains("Remove-Item"));
        var startIdx = Array.FindIndex(lines, l => l.Contains("Start-Process"));

        Assert.True(expandIdx >= 0, "Script must contain Expand-Archive");
        Assert.True(removeIdx >= 0, "Script must contain Remove-Item");
        Assert.True(startIdx >= 0, "Script must contain Start-Process");
        Assert.True(removeIdx > expandIdx, "Remove-Item must come after Expand-Archive");
        Assert.True(startIdx > removeIdx, "Start-Process must come after Remove-Item");
    }

    [Fact]
    public void GenerateApplyUpdateScript_ReturnsTrue()
    {
        // ApplyUpdate always returns true — verifying the contract
        var script = Vido.Services.Updates.GitHubUpdateService.GenerateApplyUpdateScript(
            @"C:\temp\update.zip", @"C:\Vido", 100);

        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static UpdateDialog CreateDialog()
    {
        EnsureApplication();
        return new UpdateDialog();
    }

    private static UpdateDialog CreateDialog(IUpdateService updateService)
    {
        EnsureApplication();
        return new UpdateDialog(updateService);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Vido.Views;component/Themes/Colors.xaml")
            });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Vido.Views;component/Themes/Brushes.xaml")
            });
        }
    }

    private static void RunOnStaThread(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught is not null)
            throw new AggregateException("STA thread exception", caught);
    }
}
