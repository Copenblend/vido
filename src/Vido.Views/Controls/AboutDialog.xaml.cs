using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Vido.Views.Controls;

/// <summary>
/// About dialog showing application name, version, runtime info, and FFmpeg version.
/// Styled to match VS Code's Dark Modern theme.
/// </summary>
public partial class AboutDialog : Window
{
    /// <summary>
    /// Creates a new About dialog.
    /// </summary>
    /// <param name="ffmpegVersion">
    /// FFmpeg version string to display (e.g. "7.1"). Pass null or empty
    /// if FFmpeg is not initialized.
    /// </param>
    public AboutDialog(string? ffmpegVersion = null)
    {
        InitializeComponent();

        // App version from assembly
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "Unknown";

        // .NET runtime version
        DotNetVersionText.Text = RuntimeInformation.FrameworkDescription;

        // FFmpeg version
        FFmpegVersionText.Text = string.IsNullOrEmpty(ffmpegVersion)
            ? "Not available"
            : ffmpegVersion;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
