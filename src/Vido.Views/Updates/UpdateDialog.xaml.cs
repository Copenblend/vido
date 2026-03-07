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

    /// <summary>Cancellation token source for download cancellation.</summary>
    internal CancellationTokenSource? CancellationTokenSource { get; set; }

    public UpdateDialog()
    {
        InitializeComponent();
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
        UserChoseUpdate = true;
        DialogResult = true;
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        CancellationTokenSource?.Cancel();
        Close();
    }

    private void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
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
