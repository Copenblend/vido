using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Custom title bar matching VS Code Dark Modern style.
/// Supports drag-to-move, double-click maximize/restore, and window control buttons.
/// </summary>
public partial class TitleBarView : UserControl
{
    /// <summary>Raised when the user selects File > Open Folder and picks a valid path.</summary>
    public event Action<string>? FolderOpened;

    /// <summary>Raised when the user selects File > Close Folder.</summary>
    public event Action? FolderClosed;

    /// <summary>Raised when the user selects File > Rescan Folder.</summary>
    public event Action? FolderRescanned;

    public TitleBarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the Close Folder menu item enabled state based on whether a folder is open.
    /// </summary>
    public void SetCloseFolderEnabled(bool enabled)
    {
        CloseFolderMenuItem.IsEnabled = enabled;
        RescanFolderMenuItem.IsEnabled = enabled;
    }

    /// <summary>
    /// Updates the maximize/restore icon and tooltip when the window state changes.
    /// </summary>
    public void UpdateWindowState(bool isMaximized)
    {
        MaximizeIcon.Children.Clear();

        if (isMaximized)
        {
            // Restore icon: two overlapping rectangles
            var backRect = new Rectangle
            {
                Width = 8, Height = 8,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 2)
            };
            backRect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            var frontRect = new Rectangle
            {
                Width = 8, Height = 8,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 2, 0, 0)
            };
            frontRect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            MaximizeIcon.Children.Add(backRect);
            MaximizeIcon.Children.Add(frontRect);

            MaximizeRestoreButton.ToolTip = "Restore Down";
        }
        else
        {
            // Maximize icon: single rectangle
            var rect = new Rectangle
            {
                Width = 9, Height = 9,
                StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            rect.SetResourceReference(Shape.StrokeProperty, "PrimaryForegroundBrush");

            MaximizeIcon.Children.Add(rect);

            MaximizeRestoreButton.ToolTip = "Maximize";
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ClickCount == 2)
        {
            if (DataContext is TitleBarViewModel vm)
            {
                vm.ToggleMaximizeCommand.Execute(null);
            }
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Open Folder"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FolderName))
        {
            FolderOpened?.Invoke(dialog.FolderName);
        }
    }

    private void OnCloseFolderClick(object sender, RoutedEventArgs e)
    {
        FolderClosed?.Invoke();
    }

    private void OnRescanFolderClick(object sender, RoutedEventArgs e)
    {
        FolderRescanned?.Invoke();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
