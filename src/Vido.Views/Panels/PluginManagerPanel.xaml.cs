using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// Code-behind for the Plugin Manager sidebar panel.
/// Handles click events and delegates to the PluginManagerViewModel.
/// </summary>
public partial class PluginManagerPanel : UserControl
{
    public PluginManagerPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Toggles the Installed section expanded/collapsed state.
    /// </summary>
    private void OnInstalledHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PluginManagerViewModel vm)
            vm.IsInstalledExpanded = !vm.IsInstalledExpanded;
    }

    /// <summary>
    /// Toggles the Available section expanded/collapsed state.
    /// </summary>
    private void OnAvailableHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PluginManagerViewModel vm)
            vm.IsAvailableExpanded = !vm.IsAvailableExpanded;
    }

    /// <summary>
    /// Opens the plugin detail panel when a plugin item is clicked.
    /// </summary>
    private void OnPluginItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PluginItemViewModel item
            && DataContext is PluginManagerViewModel vm)
        {
            vm.OpenDetailCommand.Execute(item);
        }
    }

    /// <summary>
    /// Opens the plugin settings when the cog icon is clicked.
    /// </summary>
    private void OnSettingsCogClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent bubbling to OnPluginItemClick
        if (sender is FrameworkElement fe && fe.DataContext is PluginItemViewModel item
            && DataContext is PluginManagerViewModel vm)
        {
            vm.OpenPluginSettingsCommand.Execute(item);
        }
    }

    /// <summary>
    /// Installs a plugin when the Install button is clicked.
    /// </summary>
    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent bubbling to OnPluginItemClick
        if (sender is FrameworkElement fe && fe.DataContext is PluginItemViewModel item
            && DataContext is PluginManagerViewModel vm)
        {
            await vm.InstallPluginAsync(item);
        }
    }

    /// <summary>
    /// Uninstalls a plugin when the Uninstall button is clicked.
    /// </summary>
    private async void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent bubbling to OnPluginItemClick
        if (sender is FrameworkElement fe && fe.DataContext is PluginItemViewModel item
            && DataContext is PluginManagerViewModel vm)
        {
            await vm.UninstallPluginAsync(item);
        }
    }

    /// <summary>
    /// Updates a plugin when the Update button is clicked.
    /// </summary>
    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent bubbling to OnPluginItemClick
        if (sender is FrameworkElement fe && fe.DataContext is PluginItemViewModel item
            && DataContext is PluginManagerViewModel vm)
        {
            await vm.UpdatePluginAsync(item);
        }
    }

    // ── Focus highlight helpers ──

    private static readonly SolidColorBrush FocusBrush = new(Color.FromRgb(0x00, 0x7a, 0xcc));

    private void OnSearchBoxGotFocus(object sender, RoutedEventArgs e)
        => SearchBorder.BorderBrush = FocusBrush;

    private void OnSearchBoxLostFocus(object sender, RoutedEventArgs e)
        => SearchBorder.BorderBrush = (Brush)FindResource("PrimaryBorderBrush");

    private void OnDropdownGotFocus(object sender, RoutedEventArgs e)
        => DropdownBorder.BorderBrush = FocusBrush;

    private void OnDropdownLostFocus(object sender, RoutedEventArgs e)
    {
        if (!RegistryDropdown.IsDropDownOpen)
            DropdownBorder.BorderBrush = (Brush)FindResource("PrimaryBorderBrush");
    }

    private void OnDropdownOpened(object sender, EventArgs e)
        => DropdownBorder.BorderBrush = FocusBrush;

    private void OnDropdownClosed(object sender, EventArgs e)
    {
        if (!RegistryDropdown.IsFocused)
            DropdownBorder.BorderBrush = (Brush)FindResource("PrimaryBorderBrush");
    }
}
