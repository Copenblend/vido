using System.Windows;
using System.Windows.Threading;

using Vido.Core.Updates;
using Vido.Views.Updates;

using Xunit;

namespace Vido.Tests.Updates;

/// <summary>
/// Tests for <see cref="UpdateDialog"/> state transitions, property assignment,
/// and button handler logic.
/// </summary>
public sealed class UpdateDialogTests
{
    // ── Enum ────────────────────────────────────────────────────────────

    [Fact]
    public void DialogState_HasExpectedValues()
    {
        var values = Enum.GetValues<UpdateDialog.DialogState>();
        Assert.Equal(5, values.Length);
        Assert.Contains(UpdateDialog.DialogState.Info, values);
        Assert.Contains(UpdateDialog.DialogState.Downloading, values);
        Assert.Contains(UpdateDialog.DialogState.Downloaded, values);
        Assert.Contains(UpdateDialog.DialogState.Error, values);
        Assert.Contains(UpdateDialog.DialogState.UpToDate, values);
    }

    // ── Property defaults ───────────────────────────────────────────────

    [Fact]
    public void NewDialog_HasDefaultPropertyValues()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            Assert.Equal(string.Empty, dialog.CurrentVersion);
            Assert.Equal(string.Empty, dialog.LatestVersion);
            Assert.Null(dialog.ReleaseNotes);
            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);
            Assert.Equal(0.0, dialog.DownloadProgress);
            Assert.Null(dialog.ErrorMessage);
            Assert.False(dialog.UserChoseUpdate);
            Assert.False(dialog.UserChoseRestart);
            Assert.Null(dialog.ReleaseUrl);
            Assert.Null(dialog.DownloadUrl);
            Assert.Null(dialog.DownloadedFilePath);
        });
    }

    // ── ShowError ───────────────────────────────────────────────────────

    [Fact]
    public void ShowError_SetsErrorStateAndMessage()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.ShowError("Network timeout", "https://example.com/releases");

            Assert.Equal(UpdateDialog.DialogState.Error, dialog.State);
            Assert.Equal("Network timeout", dialog.ErrorMessage);
            Assert.Equal("https://example.com/releases", dialog.ReleaseUrl);
        });
    }

    [Fact]
    public void ShowError_WithNullReleaseUrl_SetsStateAndMessage()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.ShowError("Connection refused");

            Assert.Equal(UpdateDialog.DialogState.Error, dialog.State);
            Assert.Equal("Connection refused", dialog.ErrorMessage);
            Assert.Null(dialog.ReleaseUrl);
        });
    }

    // ── ShowUpToDate ────────────────────────────────────────────────────

    [Fact]
    public void ShowUpToDate_SetsUpToDateStateAndVersion()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.ShowUpToDate("0.18.0");

            Assert.Equal(UpdateDialog.DialogState.UpToDate, dialog.State);
            Assert.Equal("0.18.0", dialog.CurrentVersion);
        });
    }

    // ── ShowUpdateAvailable ─────────────────────────────────────────────

    [Fact]
    public void ShowUpdateAvailable_SetsInfoStateAndProperties()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();
            var result = new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.17.0",
                LatestVersion = "0.18.0",
                ReleaseUrl = "https://example.com/release",
                ReleaseNotes = "Bug fixes and improvements.",
                InstallerDownloadUrl = "https://example.com/Vido-0.18.0.zip"
            };

            dialog.ShowUpdateAvailable(result);

            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);
            Assert.Equal("0.17.0", dialog.CurrentVersion);
            Assert.Equal("0.18.0", dialog.LatestVersion);
            Assert.Equal("Bug fixes and improvements.", dialog.ReleaseNotes);
            Assert.Equal("https://example.com/release", dialog.ReleaseUrl);
            Assert.Equal("https://example.com/Vido-0.18.0.zip", dialog.DownloadUrl);
        });
    }

    [Fact]
    public void ShowUpdateAvailable_WithNullReleaseNotes_SetsNullNotes()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();
            var result = new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.17.0",
                LatestVersion = "0.18.0"
            };

            dialog.ShowUpdateAvailable(result);

            Assert.Null(dialog.ReleaseNotes);
            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);
        });
    }

    // ── ShowDownloading ─────────────────────────────────────────────────

    [Fact]
    public void ShowDownloading_SetsDownloadingState()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.ShowDownloading();

            Assert.Equal(UpdateDialog.DialogState.Downloading, dialog.State);
        });
    }

    // ── ShowDownloaded ──────────────────────────────────────────────────

    [Fact]
    public void ShowDownloaded_SetsDownloadedState()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.ShowDownloaded();

            Assert.Equal(UpdateDialog.DialogState.Downloaded, dialog.State);
        });
    }

    // ── UpdateProgress ──────────────────────────────────────────────────

    [Fact]
    public void UpdateProgress_SetsProgressValue()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();
            dialog.ShowDownloading();

            dialog.UpdateProgress(42.5);

            Assert.Equal(42.5, dialog.DownloadProgress);
        });
    }

    // ── SetState transitions ────────────────────────────────────────────

    [Fact]
    public void SetState_CanTransitionThroughAllStates()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.LatestVersion = "1.0.0";
            dialog.CurrentVersion = "0.9.0";
            dialog.SetState(UpdateDialog.DialogState.Info);
            Assert.Equal(UpdateDialog.DialogState.Info, dialog.State);

            dialog.SetState(UpdateDialog.DialogState.Downloading);
            Assert.Equal(UpdateDialog.DialogState.Downloading, dialog.State);

            dialog.SetState(UpdateDialog.DialogState.Downloaded);
            Assert.Equal(UpdateDialog.DialogState.Downloaded, dialog.State);

            dialog.SetState(UpdateDialog.DialogState.Error);
            Assert.Equal(UpdateDialog.DialogState.Error, dialog.State);

            dialog.SetState(UpdateDialog.DialogState.UpToDate);
            Assert.Equal(UpdateDialog.DialogState.UpToDate, dialog.State);
        });
    }

    // ── DownloadedFilePath ──────────────────────────────────────────────

    [Fact]
    public void DownloadedFilePath_CanBeSet()
    {
        RunOnStaThread(() =>
        {
            var dialog = CreateDialog();

            dialog.DownloadedFilePath = @"C:\temp\update.zip";

            Assert.Equal(@"C:\temp\update.zip", dialog.DownloadedFilePath);
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static UpdateDialog CreateDialog()
    {
        EnsureApplication();
        return new UpdateDialog();
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
