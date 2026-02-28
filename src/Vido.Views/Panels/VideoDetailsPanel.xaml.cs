using System.Windows.Controls;

namespace Vido.Views.Panels;

/// <summary>
/// Code-behind for the Video Details panel.
/// Displays formatted metadata about the currently loaded video.
/// </summary>
public partial class VideoDetailsPanel : UserControl
{
    /// <summary>
    /// Sets up the video details panel and its data-bound metadata display.
    /// </summary>
    public VideoDetailsPanel()
    {
        InitializeComponent();
    }
}
