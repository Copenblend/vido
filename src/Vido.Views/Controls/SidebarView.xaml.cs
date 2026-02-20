using System.Windows.Controls;

namespace Vido.Views.Controls;

/// <summary>
/// Sidebar panel — shows the header and content for the active panel kind.
/// Content will be populated by later tickets (vi-006 for Explorer, etc.).
/// </summary>
public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }
}
