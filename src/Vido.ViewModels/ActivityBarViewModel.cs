using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Layout;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the activity bar. Tracks which sidebar panel is active
/// and whether the sidebar is visible.
/// </summary>
public partial class ActivityBarViewModel : ObservableObject
{
    private readonly ISettingsService? _settingsService;

    [ObservableProperty]
    private SidebarPanelKind _activePanel = SidebarPanelKind.Explorer;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    /// <summary>
    /// Ordered collection of plugin-contributed sidebar items.
    /// The view binds to this to render plugin buttons in the correct order.
    /// </summary>
    public ObservableCollection<PluginSidebarItem> PluginItems { get; } = new();

    public ActivityBarViewModel() : this(null) { }

    public ActivityBarViewModel(ISettingsService? settingsService)
    {
        _settingsService = settingsService;
        if (settingsService is not null)
            _isSidebarVisible = settingsService.Current.SidebarVisible;
    }

    /// <summary>
    /// Selects a panel. If the panel is already active, toggles the sidebar.
    /// Otherwise, switches to the requested panel and ensures the sidebar is visible.
    /// </summary>
    [RelayCommand]
    private void SelectPanel(SidebarPanelKind panel)
    {
        if (ActivePanel == panel && IsSidebarVisible)
        {
            IsSidebarVisible = false;
        }
        else
        {
            ActivePanel = panel;
            IsSidebarVisible = true;
        }
    }

    /// <summary>
    /// Sets the active panel without toggling visibility. Used for state restoration.
    /// </summary>
    public void SetActivePanel(SidebarPanelKind panel)
    {
        ActivePanel = panel;
    }

    partial void OnIsSidebarVisibleChanged(bool value)
    {
        if (_settingsService is null) return;
        _settingsService.Current.SidebarVisible = value;
        _settingsService.QueueSave();
    }

    /// <summary>
    /// Clears the active panel selection. Used when a plugin sidebar panel is activated
    /// to deselect all built-in panels without toggling visibility.
    /// </summary>
    public void ClearActivePanel()
    {
        ActivePanel = (SidebarPanelKind)(-1);
    }

    /// <summary>
    /// Returns true if the given panel is the currently active one.
    /// Helper used by the view to determine icon highlight state.
    /// </summary>
    public bool IsPanelActive(SidebarPanelKind panel) => ActivePanel == panel;

    // ── Plugin sidebar ordering (vb-007) ──

    /// <summary>
    /// Adds a plugin sidebar item, inserting it at the position indicated by
    /// the persisted order (if available) or by its default <see cref="PluginSidebarItem.Order"/>.
    /// </summary>
    public void AddPluginItem(PluginSidebarItem item)
    {
        var savedOrder = _settingsService?.Current.PluginSidebarOrder;
        if (savedOrder is { Count: > 0 })
        {
            var idx = savedOrder.IndexOf(item.Id);
            if (idx >= 0)
                item.Order = idx;
            else
                item.Order = savedOrder.Count + item.Order;
        }

        // Insert at the correct sorted position
        var insertIdx = 0;
        while (insertIdx < PluginItems.Count && PluginItems[insertIdx].Order <= item.Order)
            insertIdx++;

        PluginItems.Insert(insertIdx, item);
    }

    /// <summary>
    /// Removes a plugin sidebar item by its full ID.
    /// </summary>
    public void RemovePluginItem(string id)
    {
        var item = PluginItems.FirstOrDefault(p => p.Id == id);
        if (item is not null)
            PluginItems.Remove(item);
    }

    /// <summary>
    /// Moves a plugin sidebar item from one index to another (drag-and-drop).
    /// Persists the updated order to settings.
    /// </summary>
    public void MovePluginItem(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= PluginItems.Count) return;
        if (newIndex < 0 || newIndex >= PluginItems.Count) return;
        if (oldIndex == newIndex) return;

        PluginItems.Move(oldIndex, newIndex);

        // Update Order values to match new positions
        for (var i = 0; i < PluginItems.Count; i++)
            PluginItems[i].Order = i;

        PersistPluginOrder();
    }

    private void PersistPluginOrder()
    {
        if (_settingsService is null) return;
        _settingsService.Current.PluginSidebarOrder = PluginItems.Select(x => x.Id).ToList();
        _settingsService.QueueSave();
    }
}
