using System.Windows;
using System.Windows.Input;

namespace Vido.Setup;

/// <summary>
/// Code-behind for the installer window. Handles drag-to-move and close button.
/// </summary>
public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
