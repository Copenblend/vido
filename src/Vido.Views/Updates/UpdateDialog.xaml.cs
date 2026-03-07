using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

using Vido.Core.Updates;

namespace Vido.Views.Updates;

/// <summary>
/// Branded update dialog showing version info, release notes,
/// download progress, and restart prompt.
/// Replaces all <c>MessageBox.Show</c> calls in the update flow.
/// </summary>
public partial class UpdateDialog : Window
{
    /// <summary>Dialog visual states.</summary>
    public enum DialogState { Info, Downloading, Downloaded, Error, UpToDate }

    private readonly IUpdateService? _updateService;
    private CancellationTokenSource? _cts;

    /// <summary>The currently running version string.</summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>The latest available version string.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Release notes text for the new version.</summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>Current dialog state controlling which panel is visible.</summary>
    public DialogState State { get; private set; }

    /// <summary>Download progress percentage (0–100).</summary>
    public double DownloadProgress { get; set; }

    /// <summary>Error message to display in the Error state.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether the user chose to update.</summary>
    public bool UserChoseUpdate { get; private set; }

    /// <summary>Whether the user chose to restart after download.</summary>
    public bool UserChoseRestart { get; private set; }

    /// <summary>URL to the release page (for manual download fallback).</summary>
    public string? ReleaseUrl { get; set; }

    /// <summary>Direct download URL for the update package.</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Local path to the downloaded update file.</summary>
    public string? DownloadedFilePath { get; set; }

    /// <summary>
    /// Creates an UpdateDialog without an update service (for non-download states).
    /// </summary>
    public UpdateDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates an UpdateDialog with an update service for download and apply operations.
    /// </summary>
    public UpdateDialog(IUpdateService updateService) : this()
    {
        _updateService = updateService;
    }

    /// <summary>
    /// Shows the dialog in the Error state with the given message.
    /// </summary>
    internal void ShowError(string errorMessage, string? releaseUrl = null)
    {
        ErrorMessage = errorMessage;
        ReleaseUrl = releaseUrl;
        SetState(DialogState.Error);
    }

    /// <summary>
    /// Shows the dialog in the UpToDate state.
    /// </summary>
    internal void ShowUpToDate(string currentVersion)
    {
        CurrentVersion = currentVersion;
        SetState(DialogState.UpToDate);
    }

    /// <summary>
    /// Shows the dialog in the Info state with update details.
    /// </summary>
    internal void ShowUpdateAvailable(UpdateCheckResult result)
    {
        CurrentVersion = result.CurrentVersion;
        LatestVersion = result.LatestVersion;
        ReleaseNotes = result.ReleaseNotes;
        ReleaseUrl = result.ReleaseUrl;
        DownloadUrl = result.InstallerDownloadUrl;
        SetState(DialogState.Info);
    }

    /// <summary>
    /// Transitions the dialog to the Downloading state.
    /// </summary>
    internal void ShowDownloading()
    {
        SetState(DialogState.Downloading);
    }

    /// <summary>
    /// Transitions the dialog to the Downloaded state.
    /// </summary>
    internal void ShowDownloaded()
    {
        SetState(DialogState.Downloaded);
    }

    /// <summary>
    /// Updates the download progress bar.
    /// </summary>
    internal void UpdateProgress(double progressPercent)
    {
        DownloadProgress = progressPercent;
        DownloadProgressBar.Value = progressPercent;
        DownloadPercentText.Text = $"{progressPercent:F0}%";
    }

    internal void SetState(DialogState state)
    {
        State = state;

        InfoPanel.Visibility = state == DialogState.Info ? Visibility.Visible : Visibility.Collapsed;
        DownloadingPanel.Visibility = state == DialogState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        DownloadedPanel.Visibility = state == DialogState.Downloaded ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = state == DialogState.Error ? Visibility.Visible : Visibility.Collapsed;
        UpToDatePanel.Visibility = state == DialogState.UpToDate ? Visibility.Visible : Visibility.Collapsed;

        switch (state)
        {
            case DialogState.Info:
                InfoHeading.Text = $"Vido v{LatestVersion} is available!";
                InfoSubtext.Text = $"Current: v{CurrentVersion}";
                ReleaseNotesText.Text = ReleaseNotes ?? string.Empty;
                ReleaseNotesScroller.Visibility = string.IsNullOrWhiteSpace(ReleaseNotes)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                break;

            case DialogState.Downloading:
                DownloadProgressBar.Value = 0;
                DownloadPercentText.Text = "0%";
                break;

            case DialogState.Error:
                ErrorMessageText.Text = ErrorMessage ?? "An unknown error occurred.";
                OpenReleasePageButton.Visibility = string.IsNullOrEmpty(ReleaseUrl)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                break;

            case DialogState.UpToDate:
                UpToDateVersionText.Text = $"v{CurrentVersion}";
                break;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateService is null || string.IsNullOrEmpty(DownloadUrl))
        {
            UserChoseUpdate = true;
            DialogResult = true;
            return;
        }

        _ = DownloadUpdateAsync();
    }

    /// <summary>
    /// Downloads the update package, showing progress in the Downloading state.
    /// Transitions to Downloaded on success, Error on failure, or Info on cancellation.
    /// </summary>
    internal async Task DownloadUpdateAsync()
    {
        SetState(DialogState.Downloading);
        _cts = new CancellationTokenSource();

        try
        {
            var fileName = $"Vido-{LatestVersion}-win-x64-portable.zip";
            var progress = new Progress<double>(p => UpdateProgress(p * 100));

            DownloadedFilePath = await _updateService!.DownloadUpdateAsync(
                DownloadUrl!, fileName, progress, _cts.Token);

            SetState(DialogState.Downloaded);
        }
        catch (OperationCanceledException)
        {
            SetState(DialogState.Info);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, ReleaseUrl);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateService is not null && DownloadedFilePath is not null)
            _updateService.ApplyUpdate(DownloadedFilePath);

        UserChoseRestart = true;
        DialogResult = true;
    }

    private void OpenReleasePageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ReleaseUrl))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleaseUrl,
                UseShellExecute = true
            });
        }

        Close();
    }
}
