using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Menus;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Services.Events;
using Vido.Services.FileSystem;
using Vido.Services.Keyboard;
using Vido.Services.Logging;
using Vido.Services.Menus;
using Vido.Services.Settings;
using Vido.Services.State;
using Vido.Services.Video;
using Vido.ViewModels;
using Vido.Views;

namespace Vido.App;

/// <summary>
/// Application entry point. Configures the DI container and launches the main window.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupTimer = Stopwatch.StartNew();
        base.OnStartup(e);

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

        // Log time-to-visible before kicking off deferred work
        logService.Info(
            $"Window visible in {startupTimer.ElapsedMilliseconds} ms",
            "Startup");

        // Defer plugin activation to run after the first render pass completes.
        // This lets the window paint immediately without being blocked by
        // synchronous plugin I/O (directory scan, assembly load, activation).
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                var pluginTimer = Stopwatch.StartNew();
                var pluginHost = _serviceProvider.GetRequiredService<IPluginHost>();
                pluginHost.ActivateAll();
                pluginTimer.Stop();
                logService.Info(
                    $"Plugin activation completed in {pluginTimer.ElapsedMilliseconds} ms",
                    "Startup");
            }
            catch (Exception ex)
            {
                logService.Error($"Plugin system initialization failed: {ex.Message}", "PluginHost");
            }

            startupTimer.Stop();
            logService.Info(
                $"Total startup completed in {startupTimer.ElapsedMilliseconds} ms",
                "Startup");
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            // Deactivate all plugins before shutdown
            try
            {
                var pluginHost = _serviceProvider.GetRequiredService<IPluginHost>();
                pluginHost.DeactivateAll();
            }
            catch
            {
                // Plugin errors should not prevent shutdown
            }

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

        // Plugin system
        services.AddSingleton<PluginHost.ContributionRegistry>();
        services.AddSingleton<IContributionRegistry>(sp => sp.GetRequiredService<PluginHost.ContributionRegistry>());
        services.AddSingleton<IPluginHost, PluginHost.PluginHost>();
        services.AddSingleton<IPluginInstaller, Vido.Services.Plugin.PluginInstaller>();

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
