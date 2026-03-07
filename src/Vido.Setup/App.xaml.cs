using System.Windows;

using Vido.Setup.Services;
using Vido.Setup.ViewModels;

namespace Vido.Setup;

/// <summary>
/// Application entry point for the Vido installer.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var engine = new InstallEngine();
        var window = new InstallerWindow();
        var viewModel = new InstallerViewModel(engine, () => window.Close());

        window.DataContext = viewModel;
        window.Show();
    }
}
