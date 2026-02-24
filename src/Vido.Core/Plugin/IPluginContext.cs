using Vido.Core.Events;
using Vido.Core.FileSystem;
using Vido.Core.Keyboard;
using Vido.Core.Logging;
using Vido.Core.Playback;

namespace Vido.Core.Plugin;

/// <summary>
/// Provides plugins with access to all Vido extension points.
/// Passed to <see cref="IVidoPlugin.Activate"/>.
/// </summary>
public interface IPluginContext
{
    /// <summary>Plugin's own manifest data.</summary>
    PluginManifest Manifest { get; }

    /// <summary>Path to the plugin's installation directory on disk.</summary>
    string PluginDirectory { get; }

    /// <summary>Access to the Vido event bus for subscribing/publishing events.</summary>
    IEventBus Events { get; }

    /// <summary>Access to the video playback engine.</summary>
    IVideoEngine VideoEngine { get; }

    /// <summary>Access to application logging.</summary>
    ILogService Logger { get; }

    /// <summary>Access to the settings store (for reading/writing plugin settings).</summary>
    IPluginSettingsStore Settings { get; }

    // ── UI Contribution Registration ──

    /// <summary>
    /// Register a sidebar panel view factory.
    /// The <paramref name="contributionId"/> must match a sidebar contribution
    /// declared in the plugin manifest.
    /// The factory must return a WPF <c>FrameworkElement</c> (returned as <c>object</c>
    /// to keep Vido.Core platform-agnostic).
    /// </summary>
    void RegisterSidebarPanel(string contributionId, Func<object> viewFactory);

    /// <summary>
    /// Register a bottom panel tab view factory.
    /// The factory must return a WPF <c>FrameworkElement</c>.
    /// </summary>
    void RegisterBottomPanel(string contributionId, Func<object> viewFactory);

    /// <summary>
    /// Register a right panel tab view factory.
    /// The factory must return a WPF <c>FrameworkElement</c>.
    /// </summary>
    void RegisterRightPanel(string contributionId, Func<object> viewFactory);

    /// <summary>
    /// Register a status bar item view factory.
    /// The factory must return a WPF <c>FrameworkElement</c>.
    /// </summary>
    void RegisterStatusBarItem(string contributionId, Func<object> viewFactory);

    /// <summary>
    /// Updates the text of a previously registered status bar item.
    /// Uses the built-in text renderer so the item is shown with the
    /// same style as native status bar items.
    /// </summary>
    /// <param name="contributionId">The contribution ID used during registration.</param>
    /// <param name="text">Display text.</param>
    void UpdateStatusBarItem(string contributionId, string text);

    /// <summary>Register a toolbar button click handler.</summary>
    void RegisterToolbarButtonHandler(string contributionId, Action clickHandler);

    /// <summary>
    /// Sets the highlight state of a toolbar button. When highlighted the host
    /// applies the accent background colour; when not highlighted the button
    /// returns to its default transparent background.
    /// </summary>
    void SetToolbarButtonHighlight(string contributionId, bool highlighted);

    /// <summary>Register a context menu action handler.</summary>
    void RegisterContextMenuHandler(string contributionId, Action<FileNode> handler);

    /// <summary>Register a file double-click handler for specific extensions.</summary>
    void RegisterFileHandler(string[] extensions, Action<FileNode> handler);

    /// <summary>
    /// Register custom file icons for specific extensions.
    /// Keys are extensions (e.g. ".funscript"), values are paths relative
    /// to the plugin directory.
    /// </summary>
    void RegisterFileIcons(Dictionary<string, string> extensionToIconPath);

    /// <summary>Register a keyboard shortcut.</summary>
    void RegisterKeyBinding(KeyBinding binding, Action handler);

    /// <summary>
    /// Requests that the host show and expand the specified right panel.
    /// The <paramref name="contributionId"/> must match a right panel contribution
    /// that was previously registered via <see cref="RegisterRightPanel"/>.
    /// </summary>
    void RequestShowRightPanel(string contributionId);

    /// <summary>
    /// Requests that the host show and activate the specified bottom panel tab.
    /// The <paramref name="contributionId"/> must match a bottom panel contribution
    /// that was previously registered via <see cref="RegisterBottomPanel"/>.
    /// </summary>
    void RequestShowBottomPanel(string contributionId);

    /// <summary>
    /// Register a control bar item with an optional video overlay.
    /// The item appears left of the loop button in the transport controls.
    /// The <paramref name="contributionId"/> must match a controlBar contribution
    /// declared in the plugin manifest.
    /// </summary>
    /// <param name="contributionId">Contribution ID from manifest.</param>
    /// <param name="viewFactory">Factory returning a WPF element for the control bar button/widget.</param>
    /// <param name="overlayFactory">
    /// Optional factory returning a WPF element to overlay on the video surface.
    /// Use <see cref="ToggleControlBarOverlay"/> to show/hide it.
    /// </param>
    void RegisterControlBarItem(string contributionId, Func<object> viewFactory, Func<object>? overlayFactory = null);

    /// <summary>
    /// Toggles visibility of a control bar item's video overlay.
    /// Only applicable when the item was registered with a non-null overlayFactory.
    /// </summary>
    void ToggleControlBarOverlay(string contributionId, bool visible);
}
