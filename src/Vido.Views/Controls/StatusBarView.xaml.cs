using System.Windows.Controls;

namespace Vido.Views.Controls;

/// <summary>
/// Status bar â€” bottom bar showing file info, codec, resolution, etc.
/// </summary>
public partial class StatusBarView : UserControl
{
    /// <summary>
    /// Sets up the status bar UI and its data-bound display elements.
    /// </summary>
    public StatusBarView()
    {
        InitializeComponent();
    }
}
