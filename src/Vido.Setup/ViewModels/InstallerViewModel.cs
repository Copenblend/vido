using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Vido.Setup.Models;
using Vido.Setup.Services;

namespace Vido.Setup.ViewModels;

/// <summary>
/// ViewModel that orchestrates the installer's page navigation and install execution.
/// Drives the <see cref="InstallerWindow"/> via data binding.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class InstallerViewModel : ObservableObject
{
    /// <summary>
    /// The pages of the installer wizard.
    /// </summary>
    public enum InstallerPage
    {
        /// <summary>Welcome page with logo and version info.</summary>
        Welcome,
        /// <summary>Options page with shortcut and file association checkboxes.</summary>
        Options,
        /// <summary>Progress page shown during installation.</summary>
        Progress,
        /// <summary>Finish page shown after successful installation.</summary>
        Finish
    }

    private readonly InstallEngine _engine;
    private readonly Action _closeWindow;

    [ObservableProperty]
    private InstallerPage _currentPage = InstallerPage.Welcome;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _runAfterInstall = true;

    /// <summary>
    /// User-selectable install options (shortcuts, file associations).
    /// </summary>
    public InstallOptions Options { get; } = new();

    /// <summary>
    /// The version of the installer application.
    /// </summary>
    public string AppVersion { get; }

    /// <summary>
    /// The version of an existing Vido installation, or <c>null</c> if not installed.
    /// </summary>
    public string? ExistingVersion { get; }

    /// <summary>
    /// Whether the installer is upgrading an existing installation.
    /// </summary>
    public bool IsUpgrade => ExistingVersion is not null;

    /// <summary>
    /// Creates a new <see cref="InstallerViewModel"/>.
    /// </summary>
    /// <param name="engine">The install engine to use for installation operations.</param>
    /// <param name="closeWindow">Callback to close the installer window.</param>
    public InstallerViewModel(InstallEngine engine, Action closeWindow)
    {
        _engine = engine;
        _closeWindow = closeWindow;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

        ExistingVersion = _engine.DetectExistingInstall();
    }

    /// <summary>
    /// Navigates from the Welcome page to the Options page.
    /// </summary>
    [RelayCommand]
    private void GoToOptions()
    {
        CurrentPage = InstallerPage.Options;
    }

    /// <summary>
    /// Navigates back from the Options page to the Welcome page.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        CurrentPage = InstallerPage.Welcome;
    }

    /// <summary>
    /// Executes the install operation. Transitions from Options to Progress,
    /// calls <see cref="InstallEngine"/> methods in sequence with progress updates,
    /// then transitions to the Finish page.
    /// </summary>
    [RelayCommand]
    private async Task InstallAsync()
    {
        CurrentPage = InstallerPage.Progress;
        Progress = 0;
        StatusText = "Extracting files...";

        var installDir = InstallEngine.DefaultInstallDir;

        // Extract payload
        var assembly = Assembly.GetExecutingAssembly();
        using var payloadStream = assembly.GetManifestResourceStream("Vido.Payload.zip");

        if (payloadStream is not null)
        {
            var extractionProgress = new Progress<(double Progress, string Status)>(p =>
            {
                // Extraction is 0–70% of overall progress
                Progress = p.Progress * 0.7;
                StatusText = p.Status;
            });

            await _engine.ExtractPayloadAsync(payloadStream, installDir, extractionProgress);
        }

        // Create shortcuts
        Progress = 0.75;
        StatusText = "Creating shortcuts...";

        if (Options.CreateDesktopShortcut)
        {
            _engine.CreateDesktopShortcut(installDir);
        }

        if (Options.CreateStartMenuShortcut)
        {
            _engine.CreateStartMenuShortcut(installDir);
        }

        // Register file associations
        Progress = 0.85;
        StatusText = "Registering file types...";

        if (Options.RegisterFileAssociations)
        {
            _engine.RegisterFileAssociations(installDir, InstallEngine.VideoExtensions);
        }

        // Register in Add/Remove Programs
        Progress = 0.92;
        StatusText = "Completing installation...";

        _engine.RegisterUninstallEntry(installDir, AppVersion);
        _engine.RegisterInstallPath(installDir);

        Progress = 1.0;
        StatusText = "Installation complete!";

        CurrentPage = InstallerPage.Finish;
    }

    /// <summary>
    /// Finishes the installer. If <see cref="RunAfterInstall"/> is <c>true</c>,
    /// launches Vido.exe before closing.
    /// </summary>
    [RelayCommand]
    private void Finish()
    {
        if (RunAfterInstall)
        {
            var vidoExe = Path.Combine(InstallEngine.DefaultInstallDir, "Vido.exe");
            if (File.Exists(vidoExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = vidoExe,
                    UseShellExecute = true
                });
            }
        }

        _closeWindow();
    }

    /// <summary>
    /// Cancels the installer and closes the window.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _closeWindow();
    }
}
