using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vido.Core.FileSystem;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// File explorer panel displaying a folder tree view.
/// Handles lazy-loading of directory children on expansion,
/// context menu interactions, and switching between empty / folder-open visual states.
/// </summary>
public partial class FileExplorerPanel : UserControl
{
    /// <summary>
    /// Raised when the user clicks "Open Folder" (either the button or via a menu).
    /// The subscriber should show the folder dialog and call <see cref="FileExplorerViewModel.OpenFolder"/>.
    /// </summary>
    public event Action? OpenFolderRequested;

    /// <summary>
    /// Raised when the user chooses "Play" on a video file from the context menu.
    /// </summary>
    public event Action<FileNode>? PlayFileRequested;

    /// <summary>
    /// Raised when the user double-clicks a video file in the tree.
    /// </summary>
    public event Action<FileNode>? VideoFileDoubleClicked;

    public FileExplorerPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FileExplorerViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is FileExplorerViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateVisualState(newVm.HasFolderOpen);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileExplorerViewModel.HasFolderOpen) && sender is FileExplorerViewModel vm)
            UpdateVisualState(vm.HasFolderOpen);
    }

    private void UpdateVisualState(bool hasFolderOpen)
    {
        EmptyState.Visibility = hasFolderOpen ? Visibility.Collapsed : Visibility.Visible;
        TreePanel.Visibility = hasFolderOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── Tree item events ────────────────────────────────────────────

    /// <summary>
    /// Handles tree item expansion — triggers lazy-loading of directory children.
    /// </summary>
    private void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem treeViewItem
            && treeViewItem.DataContext is FileNode node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.ExpandNode(node);
        }
    }

    /// <summary>
    /// Tracks the selected node in the ViewModel when a TreeViewItem is selected.
    /// </summary>
    private void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem treeViewItem
            && treeViewItem.DataContext is FileNode node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.SelectedNode = node;
        }
    }

    // ─── Node right-click → context menu assignment ──────────────────

    /// <summary>
    /// Assigns the correct context menu based on the node type when right-clicking
    /// anywhere on a TreeViewItem row (not just the text/icon area).
    /// Uses PreviewMouseRightButtonUp (tunneling) so the ContextMenu is set on the
    /// TreeViewItem before WPF's context-menu service processes MouseRightButtonUp.
    /// </summary>
    private void OnTreeItemPreviewRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item || item.DataContext is not FileNode node)
            return;

        // Only handle for the TreeViewItem closest to the actual click source,
        // preventing parent TreeViewItems from also processing during tunneling.
        var nearestItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (nearestItem != item)
            return;

        // Select the item so it highlights
        item.IsSelected = true;

        // Assign the correct context menu to the TreeViewItem itself
        ContextMenu menu;
        if (node.IsHidden)
            menu = (ContextMenu)FindResource("HiddenNodeContextMenu");
        else if (node.IsDirectory)
            menu = (ContextMenu)FindResource("FolderContextMenu");
        else if (node.IsVideoFile)
            menu = (ContextMenu)FindResource("VideoFileContextMenu");
        else
            menu = (ContextMenu)FindResource("NonVideoFileContextMenu");

        menu.Tag = node;
        item.ContextMenu = menu;
    }

    /// <summary>
    /// Handles double-click on a TreeViewItem. If the item is a non-hidden video file,
    /// raises <see cref="VideoFileDoubleClicked"/> to trigger playback.
    /// </summary>
    private void OnTreeItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item || item.DataContext is not FileNode node)
            return;

        // Only handle for the directly-clicked item
        var nearestItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (nearestItem != item)
            return;

        if (node.IsVideoFile && !node.IsHidden)
        {
            VideoFileDoubleClicked?.Invoke(node);
            e.Handled = true;
        }
    }

    // ─── Context menu click handlers ────────────────────────────────

    private void OnPlayFileClick(object sender, RoutedEventArgs e)
    {
        // Hidden files are not playable
        if (GetNodeFromContextMenu(sender) is { IsVideoFile: true, IsHidden: false } node)
            PlayFileRequested?.Invoke(node);
    }

    private void OnHideFileClick(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromContextMenu(sender) is { } node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.HideFileCommand.Execute(node);
        }
    }

    private void OnUnhideFileClick(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromContextMenu(sender) is { } node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.UnhideFileCommand.Execute(node);
        }
    }

    private void OnRevealInExplorerClick(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromContextMenu(sender) is { } node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.RevealInExplorerCommand.Execute(node);
        }
    }

    // ─── Background context menu handlers ───────────────────────────

    private void OnTreeBackgroundContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm && sender is ContextMenu menu)
        {
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                if (item.Name == "ContextShowHiddenFiles")
                {
                    item.IsChecked = vm.ShowHiddenFiles;
                    break;
                }
            }
        }
    }

    private void OnContextOpenFolderClick(object sender, RoutedEventArgs e)
    {
        OpenFolderRequested?.Invoke();
    }

    private void OnContextCloseFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.CloseFolderCommand.Execute(null);
    }

    private void OnContextRescanFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.RescanFolderCommand.Execute(null);
    }

    private void OnToggleShowHiddenFilesClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
        {
            vm.ToggleShowHiddenFilesCommand.Execute(null);

            // Sync the checkmark state with the VM
            if (sender is MenuItem menuItem)
                menuItem.IsChecked = vm.ShowHiddenFiles;
        }
    }

    // ─── Buttons ────────────────────────────────────────────────────

    private void OnOpenFolderButtonClick(object sender, RoutedEventArgs e)
    {
        OpenFolderRequested?.Invoke();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from a <see cref="MenuItem"/> to its owning <see cref="ContextMenu"/>
    /// and reads the <see cref="FileNode"/> from its <see cref="FrameworkElement.Tag"/>.
    /// </summary>
    private static FileNode? GetNodeFromContextMenu(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            // The parent ContextMenu has the node stashed in its Tag
            if (menuItem.Parent is ContextMenu ctx && ctx.Tag is FileNode node)
                return node;
        }
        return null;
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="start"/> and returns the
    /// first ancestor of type <typeparamref name="T"/>, or null if none is found.
    /// </summary>
    private static T? FindVisualParent<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
