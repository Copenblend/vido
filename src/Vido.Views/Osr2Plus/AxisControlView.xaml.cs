using System.Windows.Controls;

namespace Vido.Views.Osr2Plus;

/// <summary>
/// View for the axis control panel, displaying axis cards and a test button.
/// DataContext: <see cref="Vido.ViewModels.Osr2Plus.AxisControlViewModel"/>.
/// </summary>
public partial class AxisControlView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="AxisControlView"/> class.</summary>
    public AxisControlView()
    {
        InitializeComponent();
    }
}
