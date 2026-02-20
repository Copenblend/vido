using System.Windows.Controls;

namespace Vido.Views.Controls;

/// <summary>
/// Sidebar panel — shows the header and content for the active panel kind.
/// The <see cref="PanelHost"/> ContentPresenter is set by MainWindow
/// based on the currently active sidebar panel.
/// </summary>
public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the content displayed in the panel area below the header.
    /// </summary>
    public void SetPanelContent(object? content)
    {
        PanelHost.Content = content;
    }
}
