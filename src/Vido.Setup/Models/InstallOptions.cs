using Vido.Setup.Services;

namespace Vido.Setup.Models;

/// <summary>
/// User-selectable options presented on the installer's Options page.
/// </summary>
public sealed class InstallOptions
{
    /// <summary>
    /// The directory where Vido will be installed.
    /// Defaults to <see cref="InstallEngine.DefaultInstallDir"/> (<c>%LOCALAPPDATA%\Vido</c>).
    /// </summary>
    public string InstallPath { get; set; } = InstallEngine.DefaultInstallDir;

    /// <summary>
    /// Whether to create a desktop shortcut for Vido.
    /// </summary>
    public bool CreateDesktopShortcut { get; set; } = true;

    /// <summary>
    /// Whether to create a Start Menu shortcut under Programs\Vido.
    /// </summary>
    public bool CreateStartMenuShortcut { get; set; } = true;

    /// <summary>
    /// Whether to register file associations for common video formats.
    /// </summary>
    public bool RegisterFileAssociations { get; set; } = true;
}
