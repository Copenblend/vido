using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    /// Assigns the correct context menu based on the node type when right-clicking.
    /// Hidden nodes always get the "Unhide" context menu.
    /// </summary>
    private void OnNodeRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not FileNode node)
            return;

        // Hidden nodes get a special context menu with "Unhide"
        if (node.IsHidden)
        {
            element.ContextMenu = (ContextMenu)FindResource("HiddenNodeContextMenu");
        }
        else if (node.IsDirectory)
        {
            element.ContextMenu = (ContextMenu)FindResource("FolderContextMenu");
        }
        else if (node.IsVideoFile)
        {
            element.ContextMenu = (ContextMenu)FindResource("VideoFileContextMenu");
        }
        else
        {
            element.ContextMenu = (ContextMenu)FindResource("NonVideoFileContextMenu");
        }

        // Tag the context menu with the node for handlers
        element.ContextMenu.Tag = node;
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
}
