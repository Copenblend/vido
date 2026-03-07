using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;

using Microsoft.Win32;

using Vido.Setup.Models;
using Vido.Setup.Services;
using Vido.Setup.ViewModels;

using Xunit;

namespace Vido.Tests.Setup;

/// <summary>
/// Tests for <see cref="InstallerViewModel"/> covering page navigation,
/// install execution flow, and finish/cancel behavior.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallerViewModelTests : IDisposable
{
    private readonly InstallEngine _engine = new();
    private readonly string _tempDir;
    private readonly List<string> _registryKeysToCleanup = [];
    private readonly List<string> _filesToCleanup = [];
    private readonly List<string> _dirsToCleanup = [];
    private bool _windowClosed;

    private static string UninstallRegistryPath =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{InstallEngine.UninstallGuid}}}";

    private static string InstallPathRegistryPath => @"Software\Vido\Install";

    private static string ProgIdRegistryPath => $@"Software\Classes\{InstallEngine.ProgId}";

    public InstallerViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VidoTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var keyPath in _registryKeysToCleanup)
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }

        foreach (var file in _filesToCleanup)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        foreach (var dir in _dirsToCleanup)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private InstallerViewModel CreateViewModel()
    {
        _windowClosed = false;
        return new InstallerViewModel(_engine, () => _windowClosed = true);
    }

    // ── Constructor ──

    /// <summary>
    /// Verifies that a new InstallerViewModel starts on the Welcome page.
    /// </summary>
    [Fact]
    public void Constructor_InitialPage_IsWelcome()
    {
        var vm = CreateViewModel();

        Assert.Equal(InstallerViewModel.InstallerPage.Welcome, vm.CurrentPage);
    }

    /// <summary>
    /// Verifies that AppVersion is a non-empty version string.
    /// </summary>
    [Fact]
    public void Constructor_AppVersion_IsNotEmpty()
    {
        var vm = CreateViewModel();

        Assert.False(string.IsNullOrEmpty(vm.AppVersion));
    }

    /// <summary>
    /// Verifies that Options is initialized with default values.
    /// </summary>
    [Fact]
    public void Constructor_Options_HasDefaults()
    {
        var vm = CreateViewModel();

        Assert.True(vm.Options.CreateDesktopShortcut);
        Assert.True(vm.Options.CreateStartMenuShortcut);
        Assert.True(vm.Options.RegisterFileAssociations);
    }

    /// <summary>
    /// Verifies that RunAfterInstall defaults to true.
    /// </summary>
    [Fact]
    public void Constructor_RunAfterInstall_DefaultsToTrue()
    {
        var vm = CreateViewModel();

        Assert.True(vm.RunAfterInstall);
    }

    /// <summary>
    /// Verifies that ExistingVersion is null when no installation exists.
    /// </summary>
    [Fact]
    public void Constructor_NoExistingInstall_ExistingVersionIsNull()
    {
        // Ensure no uninstall key exists
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);

        var vm = CreateViewModel();

        Assert.Null(vm.ExistingVersion);
        Assert.False(vm.IsUpgrade);
    }

    /// <summary>
    /// Verifies that ExistingVersion is detected when an installation exists.
    /// </summary>
    [Fact]
    public void Constructor_ExistingInstall_DetectsVersion()
    {
        // Create a fake uninstall registry entry
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath);
        key.SetValue("DisplayVersion", "1.2.3");
        _registryKeysToCleanup.Add(UninstallRegistryPath);

        var vm = CreateViewModel();

        Assert.Equal("1.2.3", vm.ExistingVersion);
        Assert.True(vm.IsUpgrade);
    }

    // ── Navigation ──

    /// <summary>
    /// Verifies that GoToOptionsCommand navigates from Welcome to Options.
    /// </summary>
    [Fact]
    public void GoToOptionsCommand_FromWelcome_NavigatesToOptions()
    {
        var vm = CreateViewModel();

        vm.GoToOptionsCommand.Execute(null);

        Assert.Equal(InstallerViewModel.InstallerPage.Options, vm.CurrentPage);
    }

    /// <summary>
    /// Verifies that GoBackCommand navigates from Options back to Welcome.
    /// </summary>
    [Fact]
    public void GoBackCommand_FromOptions_NavigatesToWelcome()
    {
        var vm = CreateViewModel();
        vm.GoToOptionsCommand.Execute(null);

        vm.GoBackCommand.Execute(null);

        Assert.Equal(InstallerViewModel.InstallerPage.Welcome, vm.CurrentPage);
    }

    // ── Install Command ──

    /// <summary>
    /// Verifies that InstallCommand transitions to the Progress page and then to Finish.
    /// Uses no embedded payload (null stream path) so extraction is skipped.
    /// </summary>
    [Fact]
    public async Task InstallCommand_ExecutesAndTransitionsToFinish()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);

        // Disable shortcuts and file associations to avoid side effects
        vm.Options.CreateDesktopShortcut = false;
        vm.Options.CreateStartMenuShortcut = false;
        vm.Options.RegisterFileAssociations = false;

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal(InstallerViewModel.InstallerPage.Finish, vm.CurrentPage);
        Assert.Equal(1.0, vm.Progress);
        Assert.Equal("Installation complete!", vm.StatusText);
    }

    /// <summary>
    /// Verifies that InstallCommand registers the uninstall entry and install path.
    /// </summary>
    [Fact]
    public async Task InstallCommand_RegistersUninstallEntryAndInstallPath()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);

        vm.Options.CreateDesktopShortcut = false;
        vm.Options.CreateStartMenuShortcut = false;
        vm.Options.RegisterFileAssociations = false;

        await vm.InstallCommand.ExecuteAsync(null);

        // Verify uninstall entry exists
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
        Assert.NotNull(key);
        Assert.Equal("Vido", key.GetValue("DisplayName"));

        // Verify install path exists
        using var installKey = Registry.CurrentUser.OpenSubKey(InstallPathRegistryPath);
        Assert.NotNull(installKey);
        Assert.NotNull(installKey.GetValue("Path"));
    }

    /// <summary>
    /// Verifies that InstallCommand completes successfully with shortcut options enabled.
    /// Shortcut file creation is tested in <see cref="InstallEngineTests"/>.
    /// </summary>
    [Fact]
    public async Task InstallCommand_WithShortcutOptions_CompletesSuccessfully()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);

        vm.Options.CreateDesktopShortcut = true;
        vm.Options.CreateStartMenuShortcut = true;
        vm.Options.RegisterFileAssociations = false;

        // Track shortcuts for cleanup
        var desktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Vido.lnk");
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Vido");

        _filesToCleanup.Add(desktopShortcut);
        _dirsToCleanup.Add(startMenuDir);

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal(InstallerViewModel.InstallerPage.Finish, vm.CurrentPage);
        Assert.Equal(1.0, vm.Progress);
    }

    /// <summary>
    /// Verifies that InstallCommand registers file associations when the option is enabled.
    /// </summary>
    [Fact]
    public async Task InstallCommand_WithFileAssociations_RegistersAssociations()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);
        _registryKeysToCleanup.Add(ProgIdRegistryPath);

        // Also clean up extension keys
        foreach (var ext in InstallEngine.VideoExtensions)
        {
            _registryKeysToCleanup.Add($@"Software\Classes\{ext}");
        }

        vm.Options.CreateDesktopShortcut = false;
        vm.Options.CreateStartMenuShortcut = false;
        vm.Options.RegisterFileAssociations = true;

        await vm.InstallCommand.ExecuteAsync(null);

        // Verify ProgID was created
        using var progIdKey = Registry.CurrentUser.OpenSubKey(ProgIdRegistryPath);
        Assert.NotNull(progIdKey);
    }

    /// <summary>
    /// Verifies that InstallCommand does not create shortcuts when options are disabled.
    /// </summary>
    [Fact]
    public async Task InstallCommand_WithOptionsDisabled_SkipsOptionalSteps()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);

        vm.Options.CreateDesktopShortcut = false;
        vm.Options.CreateStartMenuShortcut = false;
        vm.Options.RegisterFileAssociations = false;

        await vm.InstallCommand.ExecuteAsync(null);

        // Verify no ProgID was created
        using var progIdKey = Registry.CurrentUser.OpenSubKey(ProgIdRegistryPath);
        Assert.Null(progIdKey);

        Assert.Equal(InstallerViewModel.InstallerPage.Finish, vm.CurrentPage);
    }

    /// <summary>
    /// Verifies that progress is reported during install execution.
    /// </summary>
    [Fact]
    public async Task InstallCommand_ReportsProgress()
    {
        var vm = CreateViewModel();
        _registryKeysToCleanup.Add(UninstallRegistryPath);
        _registryKeysToCleanup.Add(InstallPathRegistryPath);

        vm.Options.CreateDesktopShortcut = false;
        vm.Options.CreateStartMenuShortcut = false;
        vm.Options.RegisterFileAssociations = false;

        var progressValues = new List<double>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.Progress))
                progressValues.Add(vm.Progress);
        };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.True(progressValues.Count > 0);
        Assert.Equal(1.0, progressValues[^1]);
    }

    // ── Finish Command ──

    /// <summary>
    /// Verifies that FinishCommand closes the window.
    /// </summary>
    [Fact]
    public void FinishCommand_ClosesWindow()
    {
        var vm = CreateViewModel();
        vm.RunAfterInstall = false;

        vm.FinishCommand.Execute(null);

        Assert.True(_windowClosed);
    }

    /// <summary>
    /// Verifies that FinishCommand does not launch Vido.exe when file doesn't exist.
    /// (RunAfterInstall is true but Vido.exe does not exist — no exception thrown.)
    /// </summary>
    [Fact]
    public void FinishCommand_RunAfterInstall_NoVidoExe_NoException()
    {
        var vm = CreateViewModel();
        vm.RunAfterInstall = true;

        // Should not throw — Vido.exe doesn't exist at DefaultInstallDir
        vm.FinishCommand.Execute(null);

        Assert.True(_windowClosed);
    }

    // ── Cancel Command ──

    /// <summary>
    /// Verifies that CancelCommand closes the window.
    /// </summary>
    [Fact]
    public void CancelCommand_ClosesWindow()
    {
        var vm = CreateViewModel();

        vm.CancelCommand.Execute(null);

        Assert.True(_windowClosed);
    }

    // ── Property Change Notifications ──

    /// <summary>
    /// Verifies that CurrentPage raises PropertyChanged.
    /// </summary>
    [Fact]
    public void CurrentPage_WhenChanged_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.CurrentPage))
                raised = true;
        };

        vm.GoToOptionsCommand.Execute(null);

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that StatusText raises PropertyChanged.
    /// </summary>
    [Fact]
    public void StatusText_WhenChanged_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.StatusText))
                raised = true;
        };

        vm.StatusText = "Testing...";

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that RunAfterInstall raises PropertyChanged.
    /// </summary>
    [Fact]
    public void RunAfterInstall_WhenChanged_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.RunAfterInstall))
                raised = true;
        };

        vm.RunAfterInstall = false;

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that Progress raises PropertyChanged.
    /// </summary>
    [Fact]
    public void Progress_WhenChanged_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.Progress))
                raised = true;
        };

        vm.Progress = 0.5;

        Assert.True(raised);
    }

    // ── InstallerPage Enum ──

    /// <summary>
    /// Verifies all enum values are defined.
    /// </summary>
    [Theory]
    [InlineData(InstallerViewModel.InstallerPage.Welcome)]
    [InlineData(InstallerViewModel.InstallerPage.Options)]
    [InlineData(InstallerViewModel.InstallerPage.Progress)]
    [InlineData(InstallerViewModel.InstallerPage.Finish)]
    public void InstallerPage_AllValues_AreDefined(InstallerViewModel.InstallerPage page)
    {
        Assert.True(Enum.IsDefined(page));
    }
}
