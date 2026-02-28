using System.Windows.Controls;

namespace Vido.Views.Controls;

/// <summary>
/// Sidebar panel â€” shows the header and content for the active panel kind.
/// The <see cref="PanelHost"/> ContentPresenter is set by MainWindow
/// based on the currently active sidebar panel.
/// </summary>
public partial class SidebarView : UserControl
{
    /// <summary>
    /// Sets up the sidebar view, including the panel host and header.
    /// </summary>
    public SidebarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the content displayed in the panel area below the header.
    /// </summary>
    /// <param name="content">The UI content to display, or null to clear the panel.</param>
    public void SetPanelContent(object? content)
    {
        PanelHost.Content = content;
    }
}
