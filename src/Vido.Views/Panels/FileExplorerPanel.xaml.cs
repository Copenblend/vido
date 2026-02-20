using System.Windows;
using System.Windows.Controls;
using Vido.Core.FileSystem;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// File explorer panel displaying a folder tree view.
/// Handles lazy-loading of directory children on expansion
/// and switching between empty / folder-open visual states.
/// </summary>
public partial class FileExplorerPanel : UserControl
{
    /// <summary>
    /// Raised when the user clicks "Open Folder" (either the button or via a menu).
    /// The subscriber should show the folder dialog and call <see cref="FileExplorerViewModel.OpenFolder"/>.
    /// </summary>
    public event Action? OpenFolderRequested;

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

    private void OnOpenFolderButtonClick(object sender, RoutedEventArgs e)
    {
        OpenFolderRequested?.Invoke();
    }
}
