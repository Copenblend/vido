using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vido.Core.Plugin;
using Vido.Core.Settings;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// Settings panel displayed as a tab. Shows categorized application settings
/// and plugin settings with search filtering.
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(ISettingsService settingsService, IPluginHost? pluginHost)
    {
        InitializeComponent();

        var appSettingsStore = new AppSettingsStore(settingsService);
        _viewModel = new SettingsViewModel(settingsService, appSettingsStore, pluginHost);

        CategoriesControl.ItemsSource = _viewModel.FilteredCategories;
        UpdateNoResultsVisibility();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchText = SearchBox.Text;
        CategoriesControl.ItemsSource = _viewModel.FilteredCategories;
        UpdateNoResultsVisibility();
    }

    private void UpdateNoResultsVisibility()
    {
        NoResultsText.Visibility = _viewModel.NoResults
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Restricts number TextBox input to digits and decimal points.
    /// </summary>
    private void OnNumericPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[\d.]$");
    }

    /// <summary>
    /// Handles the remove button click for a string list item.
    /// </summary>
    private void OnRemoveListItem(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string item)
        {
            // Walk up the tree to find the SettingDisplayItem
            var settingItem = FindParentSettingDisplayItem(btn);
            settingItem?.RemoveListItemCommand.Execute(item);
        }
    }

    /// <summary>
    /// Handles pressing Enter in the add-item TextBox.
    /// </summary>
    private void OnAddListItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            var settingItem = FindParentSettingDisplayItem(tb);
            settingItem?.AddListItemCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles the Add button click for a string list item.
    /// </summary>
    private void OnAddListItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var settingItem = FindParentSettingDisplayItem(btn);
            settingItem?.AddListItemCommand.Execute(null);
        }
    }

    /// <summary>
    /// Walks up the visual tree to find the <see cref="SettingDisplayItem"/> DataContext.
    /// Uses the visual tree (not logical tree) so it works inside DataTemplates.
    /// </summary>
    private static SettingDisplayItem? FindParentSettingDisplayItem(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is SettingDisplayItem item)
                return item;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
