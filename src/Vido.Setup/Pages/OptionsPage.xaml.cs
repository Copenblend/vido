using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Vido.Setup.Pages;

/// <summary>
/// Options page with checkboxes for shortcuts, file associations, and install location.
/// </summary>
public partial class OptionsPage : UserControl
{
    public OptionsPage()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose Install Location"
        };

        if (Directory.Exists(InstallPathTextBox.Text))
            dialog.InitialDirectory = InstallPathTextBox.Text;

        if (dialog.ShowDialog() == true)
            InstallPathTextBox.Text = dialog.FolderName;
    }
}
