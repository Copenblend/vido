using System.Windows;

namespace Vido.Setup;

/// <summary>
/// Application entry point for the Vido installer.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // InstallerWindow will be wired up in vido-164 (Installer UI pages)
    }
}
