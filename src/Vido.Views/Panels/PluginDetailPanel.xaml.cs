using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vido.Core.Logging;
using Vido.Core.Plugin;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// Detail panel for a single plugin. Shown as a tab in the main editor area.
/// Displays header with action buttons, tabbed content (Details/Changelog/Settings),
/// and right-side metadata pane.
/// </summary>
public partial class PluginDetailPanel : UserControl
{
    private readonly PluginItemViewModel? _item;
    private readonly PluginManagerViewModel? _managerVm;
    private readonly IPluginHost? _pluginHost;
    private readonly ILogService? _logService;
    private string _activeTab = "Details";

    public PluginDetailPanel(
        PluginItemViewModel? item,
        PluginManagerViewModel? managerVm,
        IPluginHost? pluginHost,
        ILogService? logService)
    {
        _item = item;
        _managerVm = managerVm;
        _pluginHost = pluginHost;
        _logService = logService;

        InitializeComponent();

        if (item is not null)
        {
            PopulateHeader();
            PopulateMetadata();
            LoadContent();
            SubscribeToChanges();
        }
    }

    /// <summary>
    /// Populates the header section with plugin info.
    /// </summary>
    private void PopulateHeader()
    {
        if (_item is null) return;

        HeaderTitle.Text = _item.DisplayName;
        HeaderPublisher.Text = _item.Publisher;
        HeaderDescription.Text = _item.Description;
        HeaderVerifiedBadge.Visibility = _item.IsOfficial ? Visibility.Visible : Visibility.Collapsed;

        UpdateActionButtons();
    }

    /// <summary>
    /// Updates the install/uninstall and enable/disable button states.
    /// </summary>
    private void UpdateActionButtons()
    {
        if (_item is null) return;

        if (_item.IsInstalled)
        {
            InstallUninstallButton.Content = "Uninstall";
            InstallUninstallButton.Style = (Style)FindResource("ActionButtonRedStyle");

            EnableDisableButton.Content = _item.IsEnabled ? "Disable" : "Enable";
            EnableDisableButton.Style = _item.IsEnabled
                ? (Style)FindResource("ActionButtonRedStyle")
                : (Style)FindResource("ActionButtonBlueStyle");
            EnableDisableButton.Visibility = Visibility.Visible;
            SettingsGearButton.Visibility = Visibility.Visible;
        }
        else
        {
            InstallUninstallButton.Content = "Install";
            InstallUninstallButton.Style = (Style)FindResource("ActionButtonBlueStyle");
            EnableDisableButton.Visibility = Visibility.Collapsed;
            SettingsGearButton.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Populates the right-side metadata pane.
    /// </summary>
    private void PopulateMetadata()
    {
        if (_item is null) return;

        MetaVersion.Text = _item.Version;
        MetaTags.Text = _item.Tags.Count > 0 ? string.Join(", ", _item.Tags) : "—";
        MetaLastUpdated.Text = _item.LastUpdated ?? "—";
        MetaLicense.Text = !string.IsNullOrWhiteSpace(_item.License) ? _item.License : "—";
    }

    /// <summary>
    /// Loads README.md and CHANGELOG.md content from the plugin directory.
    /// </summary>
    private void LoadContent()
    {
        if (_item is null) return;

        // Load README.md
        var readme = TryReadPluginFile("README.md");
        DetailsText.Text = !string.IsNullOrWhiteSpace(readme)
            ? readme
            : "No details available.";

        // Load CHANGELOG.md
        var changelog = TryReadPluginFile("CHANGELOG.md");
        ChangelogText.Text = !string.IsNullOrWhiteSpace(changelog)
            ? changelog
            : "No changelog available.";

        // Load settings
        LoadSettings();
    }

    /// <summary>
    /// Reads a file from the plugin directory. Returns null if not found or on error.
    /// </summary>
    private string? TryReadPluginFile(string filename)
    {
        if (_item?.PluginInfo?.Directory is null) return null;

        var path = Path.Combine(_item.PluginInfo.Directory, filename);
        if (!File.Exists(path)) return null;

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads and displays plugin settings.
    /// </summary>
    private void LoadSettings()
    {
        if (_item?.PluginInfo is null)
        {
            NoSettingsText.Visibility = Visibility.Visible;
            NoSettingsText.Text = _item?.IsInstalled == true
                ? "This plugin has no configurable settings."
                : "Install this plugin to configure its settings.";
            return;
        }

        var manifest = _item.PluginInfo.Manifest;
        var settings = manifest.Contributes.Settings;

        if (settings.Count == 0)
        {
            NoSettingsText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            // Get or create a settings store for this plugin
            // We use the PluginHost to find the actual IPluginHost at runtime
            // For now, create a direct store based on the plugin ID
            IPluginSettingsStore? store = null;

            // Try to get the store from the PluginHost via the manager VM
            if (_managerVm is not null)
            {
                // The manager VM has access to IPluginHost
                store = GetSettingsStoreFromHost(_item.Id);
            }

            if (store is null)
            {
                NoSettingsText.Visibility = Visibility.Visible;
                NoSettingsText.Text = "Unable to load settings.";
                return;
            }

            // Build display items for each setting
            var displayItems = new List<SettingDisplayItem>();

            foreach (var setting in settings)
            {
                var displayItem = new SettingDisplayItem(setting, store);
                displayItems.Add(displayItem);
            }

            SettingsItemsControl.ItemsSource = displayItems;
            NoSettingsText.Visibility = Visibility.Collapsed;

            // Handle section headers via code-behind after items are loaded
            SettingsItemsControl.Loaded += (_, _) => ApplySectionHeaders(displayItems);
        }
        catch (Exception ex)
        {
            _logService?.Error($"Failed to load settings for plugin '{_item.Id}': {ex.Message}", "PluginDetail");
            NoSettingsText.Visibility = Visibility.Visible;
            NoSettingsText.Text = "Failed to load settings.";
        }
    }

    /// <summary>
    /// Gets the settings store from the IPluginHost.
    /// </summary>
    private IPluginSettingsStore? GetSettingsStoreFromHost(string pluginId)
    {
        return _pluginHost?.GetSettingsStore(pluginId);
    }

    /// <summary>
    /// Shows section headers for settings that belong to different sections.
    /// </summary>
    private void ApplySectionHeaders(List<SettingDisplayItem> items)
    {
        string? lastSection = null;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Section is not null && item.Section != lastSection)
            {
                lastSection = item.Section;
                // Find the container and show its section header
                var container = SettingsItemsControl.ItemContainerGenerator.ContainerFromIndex(i);
                if (container is ContentPresenter cp)
                {
                    var header = FindChild<Border>(cp, "SectionHeader");
                    if (header is not null)
                        header.Visibility = Visibility.Visible;
                }
            }
            else if (item.Section is null || item.Section == lastSection)
            {
                lastSection = item.Section;
            }
        }
    }

    /// <summary>
    /// Subscribes to PropertyChanged on the PluginItemViewModel to update the UI in real-time.
    /// </summary>
    private void SubscribeToChanges()
    {
        if (_item is null) return;

        _item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PluginItemViewModel.IsInstalled)
                or nameof(PluginItemViewModel.IsEnabled))
            {
                Dispatcher.Invoke(UpdateActionButtons);
            }
        };
    }

    /// <summary>
    /// Switches to the Settings tab.
    /// </summary>
    public void SwitchToSettings()
    {
        SetActiveTab("Settings");
    }

    /// <summary>
    /// Sets the active tab (Details, Changelog, or Settings).
    /// </summary>
    private void SetActiveTab(string tabName)
    {
        _activeTab = tabName;

        // Update tab header appearance
        var activeBrush = (Brush)FindResource("PrimaryForegroundBrush");
        var inactiveBrush = (Brush)FindResource("SecondaryForegroundBrush");

        DetailsTabText.Foreground = tabName == "Details" ? activeBrush : inactiveBrush;
        ChangelogTabText.Foreground = tabName == "Changelog" ? activeBrush : inactiveBrush;
        SettingsTabText.Foreground = tabName == "Settings" ? activeBrush : inactiveBrush;

        // Toggle content visibility
        DetailsContent.Visibility = tabName == "Details" ? Visibility.Visible : Visibility.Collapsed;
        ChangelogContent.Visibility = tabName == "Changelog" ? Visibility.Visible : Visibility.Collapsed;
        SettingsContent.Visibility = tabName == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Click handlers ──

    private void OnDetailsTabClick(object sender, MouseButtonEventArgs e) => SetActiveTab("Details");
    private void OnChangelogTabClick(object sender, MouseButtonEventArgs e) => SetActiveTab("Changelog");
    private void OnSettingsTabClick(object sender, MouseButtonEventArgs e) => SetActiveTab("Settings");

    private async void OnInstallUninstallClick(object sender, RoutedEventArgs e)
    {
        if (_item is null || _managerVm is null) return;

        if (_item.IsInstalled)
            await _managerVm.UninstallPluginAsync(_item);
        else
            await _managerVm.InstallPluginAsync(_item);
    }

    private void OnEnableDisableClick(object sender, RoutedEventArgs e)
    {
        if (_item is null || _managerVm is null) return;
        _managerVm.ToggleEnabled(_item);
    }

    private void OnHeaderSettingsClick(object sender, RoutedEventArgs e)
    {
        SwitchToSettings();
    }

    /// <summary>
    /// Restricts input to numeric characters only (digits, decimal point, minus sign).
    /// </summary>
    private void OnNumericPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsNumericInput(e.Text);
    }

    private static bool IsNumericInput(string text)
    {
        return NumericRegex().IsMatch(text);
    }

    [GeneratedRegex(@"^[\d.\-]$")]
    private static partial Regex NumericRegex();

    /// <summary>
    /// Finds a named child element in the visual tree.
    /// </summary>
    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;

            var result = FindChild<T>(child, name);
            if (result is not null)
                return result;
        }
        return null;
    }
}
