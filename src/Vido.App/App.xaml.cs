using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Core.Updates;
using Vido.Services.Events;
using Vido.Services.FileSystem;
using Vido.Services.Keyboard;
using Vido.Services.Logging;
using Vido.Services.Menus;
using Vido.Services.Settings;
using Vido.Services.State;
using Vido.Services.Updates;
using Vido.Services.SingleInstance;
using Vido.Services.Video;
using Vido.ViewModels;
using Vido.Views;
using Vido.Views.Updates;

namespace Vido.App;

/// <summary>
/// Application entry point. Configures the DI container and launches the main window.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private SingleInstanceService? _singleInstanceService;

    /// <summary>
    /// Performs asynchronous application startup, dependency initialization, and main window launch.
    /// </summary>
    /// <param name="e">Startup event arguments including command-line arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupTimer = Stopwatch.StartNew();
        base.OnStartup(e);

        // ── Uninstall mode ──
        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            var dialog = new UninstallDialog();
            dialog.ShowDialog();
            Shutdown();
            return;
        }

        // ── Single-instance check ──
        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.IsFirstInstance)
        {
            // Forward file path to existing instance and exit
            try
            {
                var filePath = e.Args.Length > 0 ? e.Args[0].Trim('"') : null;
                if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
                    _singleInstanceService.SendFileToExistingInstance(filePath);
            }
            catch
            {
                // If pipe fails, the existing instance is unreachable — exit silently.
                // The first instance continues running.
            }

            _singleInstanceService.Dispose();
            _singleInstanceService = null;
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load persisted settings and state before showing the window
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var stateService = _serviceProvider.GetRequiredService<IStateService>();
        await settingsService.LoadAsync();
        await stateService.LoadAsync();

        // Initialize FFmpeg (non-fatal if DLLs are not present)
        var logService = _serviceProvider.GetRequiredService<ILogService>();
        FFmpegInitializer.Initialize(logService);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.FFmpegVersion = FFmpegInitializer.VersionString;

        // Store command-line args BEFORE Show() so they're available when the
        // Loaded event fires (Show triggers Loaded synchronously).
        mainWindow.ProcessCommandLineArgs(e.Args);
        mainWindow.Show();

        // Start listening for file paths from secondary instances
        _singleInstanceService.FileReceived += mainWindow.HandleExternalFileOpen;
        _singleInstanceService.StartListening();

        // Log time-to-visible before kicking off deferred work
        logService.Info(
            $"Window visible in {startupTimer.ElapsedMilliseconds} ms",
            "Startup");

        startupTimer.Stop();
        logService.Info(
            $"Total startup completed in {startupTimer.ElapsedMilliseconds} ms",
            "Startup");
    }

    /// <summary>
    /// Performs asynchronous shutdown tasks and disposes the dependency injection container.
    /// </summary>
    /// <param name="e">Exit event arguments for application shutdown.</param>
    protected override async void OnExit(ExitEventArgs e)
    {
        _singleInstanceService?.Dispose();
        _singleInstanceService = null;

        if (_serviceProvider is not null)
        {
            // Persist state and flush any pending settings before shutdown
            var stateService = _serviceProvider.GetRequiredService<IStateService>();
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            await stateService.SaveAsync();
            await settingsService.SaveAsync();

            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core infrastructure
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IStateService, StateService>();

        // File system
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Video engine
        services.AddSingleton<Vido.Core.Playback.IVideoEngine, FFmpegVideoEngine>();

        // Menus
        services.AddSingleton<IContextMenuRegistry, ContextMenuRegistry>();

        // Keyboard shortcuts
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();

        // Update checking
        var vidoVersion = typeof(App).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0.0.0";
        // Strip any +commit suffix (e.g. "0.6.0+abc123" → "0.6.0")
        var plusIndex = vidoVersion.IndexOf('+');
        if (plusIndex >= 0) vidoVersion = vidoVersion[..plusIndex];
        services.AddSingleton<IUpdateService>(new GitHubUpdateService(vidoVersion));

        // ViewModels
        services.AddSingleton<FileExplorerViewModel>();
        services.AddSingleton<VideoPlayerViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<OutputLogViewModel>();
        services.AddSingleton<VideoDetailsViewModel>();
        services.AddSingleton<StatusBarViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }
}
