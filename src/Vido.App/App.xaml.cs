using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Logging;
using Vido.Core.Settings;
using Vido.Core.State;
using Vido.Services.Events;
using Vido.Services.FileSystem;
using Vido.Services.Logging;
using Vido.Services.Settings;
using Vido.Services.State;
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
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load persisted settings and state before showing the window
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var stateService = _serviceProvider.GetRequiredService<IStateService>();
        await settingsService.LoadAsync();
        await stateService.LoadAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
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

        // ViewModels
        services.AddSingleton<FileExplorerViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }
}
