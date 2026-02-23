using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vido.Core.FileSystem;
using Vido.Core.Menus;
using Vido.Core.Plugin;
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

    /// <summary>
    /// Raised when a non-video file is double-clicked and matched by a plugin file handler.
    /// </summary>
    public event Action<FileNode>? FileHandlerRequested;

    /// <summary>
    /// Raised when files or folders are dropped onto the explorer panel.
    /// </summary>
    public event Action<string[]>? FilesDroppedOnExplorer;

    /// <summary>
    /// Optional context menu registry for injecting plugin-contributed menu items.
    /// Set by MainWindow after creating the panel.
    /// </summary>
    public IContextMenuRegistry? ContextMenuRegistry { get; set; }

    /// <summary>
    /// Optional contribution registry for querying plugin file icons.
    /// Set by MainWindow after creating the panel.
    /// </summary>
    public IContributionRegistry? ContributionRegistry { get; set; }

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
    /// When a TreeViewItem is loaded, checks if the file has a plugin-provided
    /// custom icon override and swaps the default icon if one exists.
    /// </summary>
    private void OnTreeItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item || item.DataContext is not FileNode node)
            return;

        if (node.IsDirectory || node.IsVideoFile) return;
        if (ContributionRegistry is null) return;

        var ext = Path.GetExtension(node.Name);
        if (string.IsNullOrEmpty(ext)) return;

        var icons = ContributionRegistry.GetFileIcons();

        // Try compound extension first (e.g. ".twist.funscript")
        string? iconPath = null;
        var dotIndex = node.Name.IndexOf('.');
        if (dotIndex >= 0)
        {
            var compoundExt = node.Name[dotIndex..];
            if (!string.Equals(compoundExt, ext, StringComparison.OrdinalIgnoreCase)
                && icons.TryGetValue(compoundExt, out var compoundPath))
            {
                iconPath = compoundPath;
            }
        }

        // Fall back to simple extension
        if (iconPath is null && !icons.TryGetValue(ext, out iconPath))
            return;

        if (!File.Exists(iconPath)) return;

        // Find the named elements inside the data template
        var contentPresenter = FindVisualChild<ContentPresenter>(item);
        if (contentPresenter is null) return;

        var template = contentPresenter.ContentTemplate;
        if (template is null) return;

        var genericIcon = template.FindName("GenericFileIcon", contentPresenter) as UIElement;
        var customIcon = template.FindName("CustomFileIcon", contentPresenter) as System.Windows.Controls.Image;

        if (genericIcon is not null && customIcon is not null)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                customIcon.Source = bitmap;
                customIcon.Visibility = Visibility.Visible;
                genericIcon.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // Icon load failed — keep default icon
            }
        }
    }

    /// <summary>
    /// Finds the first visual child of the specified type.
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }

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

        // Inject plugin-contributed context menu items
        InjectPluginContextMenuItems(menu, node);
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

        if (node.IsHidden) return;

        if (node.IsVideoFile)
        {
            VideoFileDoubleClicked?.Invoke(node);
            e.Handled = true;
        }
        else if (!node.IsDirectory)
        {
            // Check if a plugin file handler is registered for this extension
            FileHandlerRequested?.Invoke(node);
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

    private void OnRemoveFileClick(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromContextMenu(sender) is { } node
            && DataContext is FileExplorerViewModel vm)
        {
            vm.RemoveFileCommand.Execute(node);
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

    // ─── Plugin context menu injection ──────────────────────────────

    /// <summary>
    /// Dynamically adds plugin-contributed context menu items from the
    /// <see cref="IContextMenuRegistry"/> to the given context menu.
    /// Items tagged with "plugin-injected" are removed on each call to
    /// avoid duplicates when the same static ContextMenu resource is reused.
    /// </summary>
    private void InjectPluginContextMenuItems(ContextMenu menu, FileNode node)
    {
        if (ContextMenuRegistry is null) return;

        // Remove previously injected plugin items (static menus are reused)
        for (int i = menu.Items.Count - 1; i >= 0; i--)
        {
            if (menu.Items[i] is FrameworkElement fe && fe.Tag is string tag && tag == "plugin-injected")
                menu.Items.RemoveAt(i);
        }

        // Determine the target type
        var target = node.IsDirectory ? ContextMenuTarget.Folder : ContextMenuTarget.File;
        var entries = ContextMenuRegistry.GetEntries(target);

        if (entries.Count == 0) return;

        foreach (var entry in entries)
        {
            if (!entry.IsEnabled(node)) continue;

            var menuItem = new MenuItem
            {
                Header = entry.Label,
                Tag = "plugin-injected",
                InputGestureText = entry.InputGestureText
            };
            menuItem.SetResourceReference(StyleProperty, "ContextMenuItemStyle");
            var capturedNode = node;
            menuItem.Click += (_, _) =>
            {
                try { entry.Handler(capturedNode); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Plugin context menu handler error: {ex.Message}"); }
            };
            menu.Items.Add(menuItem);
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

    // ─── Drag and drop ──────────────────────────────────────────────

    private void OnExplorerDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DragOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnExplorerDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnExplorerDragLeave(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    /// <summary>
    /// Processes drops on the file explorer panel.
    /// Passes all paths to the parent for additive insert into the tree.
    /// </summary>
    private void OnExplorerDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            FilesDroppedOnExplorer?.Invoke(paths);

        e.Handled = true;
    }
}
