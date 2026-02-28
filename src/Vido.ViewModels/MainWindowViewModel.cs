using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vido.Core.Layout;
using Vido.Core.Settings;

namespace Vido.ViewModels;

/// <summary>
/// ViewModel for the main window. Manages the tab system and panel visibility.
/// Coordinates between the tab well, bottom panel, and right panel.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// When true, property change handlers skip persisting to settings.
    /// Used during fullscreen transitions to avoid saving transient UI state.
    /// </summary>
    public bool SuppressSettingsSave { get; set; }

    /// <summary>
    /// Well-known tab ID for the video player.
    /// </summary>
    public const string PlayerTabId = "Player";

    /// <summary>
    /// Well-known tab ID for the settings page.
    /// </summary>
    public const string SettingsTabId = "Settings";

    // ── Bottom Panel Tab IDs ──

    /// <summary>
    /// Well-known tab ID for the output log panel.
    /// </summary>
    public const string OutputTabId = "LogOutput";

    /// <summary>
    /// Material Design gear icon geometry for the Settings tab.
    /// </summary>
    private const string SettingsIconGeometry =
        "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5"
        + "M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37"
        + "C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05"
        + "C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10"
        + "C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05"
        + "C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11"
        + "C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63"
        + "C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.04 4.95,18.95L7.44,17.94"
        + "C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14"
        + "C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95"
        + "C19.27,19.04 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";

    // ── Tab Management ──

    /// <summary>
    /// All open tabs, in display order.
    /// </summary>
    public ObservableCollection<TabItemModel> Tabs { get; } = [];

    /// <summary>
    /// The currently active tab.
    /// </summary>
    [ObservableProperty]
    private TabItemModel? _activeTab;

    partial void OnActiveTabChanged(TabItemModel? oldValue, TabItemModel? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is not null) newValue.IsActive = true;
    }

    // ── Panel Visibility ──

    /// <summary>
    /// Whether the bottom panel is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isBottomPanelVisible;

    /// <summary>
    /// Whether the bottom panel is collapsed (showing only tab strip).
    /// </summary>
    [ObservableProperty]
    private bool _isBottomPanelCollapsed;

    /// <summary>
    /// Whether the right panel is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isRightPanelVisible;

    /// <summary>
    /// Whether the right panel is collapsed (showing only tab strip).
    /// </summary>
    [ObservableProperty]
    private bool _isRightPanelCollapsed;

    /// <summary>
    /// Whether the status bar is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusBarVisible = true;

    /// <summary>
    /// Whether the application is in fullscreen mode.
    /// </summary>
    [ObservableProperty]
    private bool _isFullscreen;

    // ── Bottom Panel Tabs ──

    /// <summary>
    /// All open bottom panel tabs.
    /// </summary>
    public ObservableCollection<BottomPanelTabItem> BottomPanelTabs { get; } = [];

    /// <summary>
    /// The currently active bottom panel tab.
    /// </summary>
    [ObservableProperty]
    private BottomPanelTabItem? _activeBottomPanelTab;

    partial void OnActiveBottomPanelTabChanged(BottomPanelTabItem? oldValue, BottomPanelTabItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is not null) newValue.IsActive = true;
    }
    
    /// <summary>
    /// Creates the main window view model, initializing tabs and restoring panel state from persisted settings.
    /// </summary>
    /// <param name="settingsService">Service for reading and persisting panel layout preferences.</param>
    public MainWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // The Player tab is always present, pinned leftmost, not closable
        var playerTab = new TabItemModel(PlayerTabId, "Player")
        {
            IconGeometry = "M 4,2 L 4,18 L 18,10 Z",
            IsClosable = false,
            IsPinned = true
        };

        Tabs.Add(playerTab);
        ActiveTab = playerTab;

        // Initialize panel state from persisted settings (use backing fields to avoid triggering saves)
        var s = settingsService.Current;

        // Initialize bottom panel tabs — only add Log Output if persisted as visible
        if (s.LogOutputVisible)
        {
            var outputTab = new BottomPanelTabItem(OutputTabId, "LOG OUTPUT") { IsClosable = false };
            BottomPanelTabs.Add(outputTab);
            ActiveBottomPanelTab = outputTab;
        }

        _isBottomPanelVisible = s.BottomPanelVisible;
        _isBottomPanelCollapsed = s.BottomPanelCollapsed;
        _isRightPanelVisible = s.RightPanelVisible;
        _isRightPanelCollapsed = s.RightPanelCollapsed;
        _isStatusBarVisible = s.StatusBarVisible;
    }

    // ── Tab Commands ──

    /// <summary>
    /// Opens a tab. If a tab with the same ID already exists, activates it.
    /// Otherwise creates a new tab and activates it.
    /// </summary>
    /// <param name="tabId">Unique identifier for the tab.</param>
    /// <param name="title">Display title for the tab header.</param>
    /// <param name="iconGeometry">Optional path geometry for the tab icon.</param>
    /// <param name="isClosable">Whether the user can close this tab (default true).</param>
    public void OpenTab(string tabId, string title, string? iconGeometry = null, bool isClosable = true)
    {
        var existing = FindTab(tabId);
        if (existing is not null)
        {
            ActiveTab = existing;
            return;
        }

        var tab = new TabItemModel(tabId, title)
        {
            IconGeometry = iconGeometry,
            IsClosable = isClosable
        };

        Tabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>
    /// Closes a tab by ID. Cannot close non-closable tabs (e.g., Player).
    /// If the closed tab was active, activates the nearest remaining tab.
    /// </summary>
    /// <param name="tabId">ID of the tab to close.</param>
    [RelayCommand]
    public void CloseTab(string tabId)
    {
        var tab = FindTab(tabId);
        if (tab is null || !tab.IsClosable) return;

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        // If the closed tab was active, activate the nearest neighbor
        if (ActiveTab == tab || ActiveTab is null)
        {
            if (Tabs.Count > 0)
            {
                var newIndex = Math.Min(index, Tabs.Count - 1);
                ActiveTab = Tabs[newIndex];
            }
            else
            {
                ActiveTab = null;
            }
        }
    }

    /// <summary>
    /// Activates a tab by ID.
    /// </summary>
    /// <param name="tabId">ID of the tab to activate.</param>
    [RelayCommand]
    public void ActivateTab(string tabId)
    {
        var tab = FindTab(tabId);
        if (tab is not null)
            ActiveTab = tab;
    }

    /// <summary>
    /// Reorders a tab from one index to another.
    /// Pinned tabs cannot be moved. Tabs cannot be moved before pinned tabs.
    /// </summary>
    /// <param name="fromIndex">Current index of the tab to move.</param>
    /// <param name="toIndex">Target index to move the tab to.</param>
    public void ReorderTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Tabs.Count) return;
        if (toIndex < 0 || toIndex >= Tabs.Count) return;
        if (fromIndex == toIndex) return;

        var tab = Tabs[fromIndex];
        if (tab.IsPinned) return;

        // Cannot move before pinned tabs
        var firstUnpinnedIndex = 0;
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (!Tabs[i].IsPinned) { firstUnpinnedIndex = i; break; }
        }

        if (toIndex < firstUnpinnedIndex) return;

        Tabs.Move(fromIndex, toIndex);
    }

    // ── Panel Commands ──

    /// <summary>
    /// Toggles the bottom panel visibility.
    /// </summary>
    [RelayCommand]
    public void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
        if (IsBottomPanelVisible)
            IsBottomPanelCollapsed = false;
    }

    /// <summary>
    /// Toggles the bottom panel between collapsed (tab strip only) and expanded.
    /// </summary>
    [RelayCommand]
    public void ToggleBottomPanelCollapse()
    {
        if (!IsBottomPanelVisible)
        {
            // If panel is hidden entirely, show it expanded
            IsBottomPanelVisible = true;
            IsBottomPanelCollapsed = false;
            return;
        }

        IsBottomPanelCollapsed = !IsBottomPanelCollapsed;
    }

    /// <summary>
    /// Toggles the right panel visibility.
    /// </summary>
    [RelayCommand]
    public void ToggleRightPanel()
    {
        IsRightPanelVisible = !IsRightPanelVisible;
        if (IsRightPanelVisible)
            IsRightPanelCollapsed = false;
    }

    /// <summary>
    /// Toggles the right panel between collapsed (tab strip only) and expanded.
    /// </summary>
    [RelayCommand]
    public void ToggleRightPanelCollapse()
    {
        if (!IsRightPanelVisible)
        {
            // If panel is hidden entirely, show it expanded
            IsRightPanelVisible = true;
            IsRightPanelCollapsed = false;
            return;
        }

        IsRightPanelCollapsed = !IsRightPanelCollapsed;
    }

    /// <summary>
    /// Toggles the status bar visibility.
    /// </summary>
    [RelayCommand]
    public void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    partial void OnIsBottomPanelVisibleChanged(bool value)
    {
        if (SuppressSettingsSave) return;
        _settingsService.Current.BottomPanelVisible = value;
        _settingsService.QueueSave();
    }

    partial void OnIsRightPanelVisibleChanged(bool value)
    {
        if (SuppressSettingsSave) return;
        _settingsService.Current.RightPanelVisible = value;
        _settingsService.QueueSave();
    }

    partial void OnIsStatusBarVisibleChanged(bool value)
    {
        if (SuppressSettingsSave) return;
        _settingsService.Current.StatusBarVisible = value;
        _settingsService.QueueSave();
    }

    partial void OnIsBottomPanelCollapsedChanged(bool value)
    {
        if (SuppressSettingsSave) return;
        _settingsService.Current.BottomPanelCollapsed = value;
        _settingsService.QueueSave();
    }

    partial void OnIsRightPanelCollapsedChanged(bool value)
    {
        if (SuppressSettingsSave) return;
        _settingsService.Current.RightPanelCollapsed = value;
        _settingsService.QueueSave();
    }

    // ── Log Output Toggle ──

    /// <summary>
    /// Whether the Log Output tab is currently visible.
    /// </summary>
    public bool IsLogOutputVisible => FindBottomPanelTab(OutputTabId) is not null;

    /// <summary>
    /// Toggles the Log Output tab visibility. When shown, activates it.
    /// When hidden, removes it from the tab strip.
    /// </summary>
    public void ToggleLogOutput()
    {
        var existing = FindBottomPanelTab(OutputTabId);
        if (existing is not null)
        {
            // Hide: remove the tab
            BottomPanelTabs.Remove(existing);
            if (ActiveBottomPanelTab == existing)
                ActiveBottomPanelTab = BottomPanelTabs.FirstOrDefault();

            _settingsService.Current.LogOutputVisible = false;
        }
        else
        {
            // Show: add and activate the tab
            var outputTab = new BottomPanelTabItem(OutputTabId, "LOG OUTPUT") { IsClosable = false };
            BottomPanelTabs.Insert(0, outputTab);
            ActiveBottomPanelTab = outputTab;
            IsBottomPanelVisible = true;

            _settingsService.Current.LogOutputVisible = true;
        }
        _settingsService.QueueSave();
    }

    // ── Bottom Panel Tab Commands ──

    /// <summary>
    /// Activates a bottom panel tab by ID. If the panel is hidden, opens it.
    /// If the tab was closed, re-adds it.
    /// </summary>
    /// <param name="tabId">ID of the bottom panel tab to activate.</param>
    [RelayCommand]
    public void ActivateBottomPanelTab(string tabId)
    {
        var tab = FindBottomPanelTab(tabId);
        if (tab is null)
        {
            // Re-add the tab if it was closed
            OpenBottomPanelTab(tabId);
            return;
        }

        ActiveBottomPanelTab = tab;
        IsBottomPanelVisible = true;
    }

    /// <summary>
    /// Opens (or re-opens) a bottom panel tab. If it doesn't exist, creates it
    /// and inserts it at its canonical position.
    /// </summary>
    /// <param name="tabId">ID of the bottom panel tab to open.</param>
    public void OpenBottomPanelTab(string tabId) => OpenBottomPanelTab(tabId, null);

    /// <summary>
    /// Opens (or re-opens) a bottom panel tab with an optional custom title.
    /// If it doesn't exist, creates it and inserts it at its canonical position.
    /// </summary>
    /// <param name="tabId">ID of the bottom panel tab to open.</param>
    /// <param name="customTitle">Optional display title override; defaults to the tab ID uppercased.</param>
    public void OpenBottomPanelTab(string tabId, string? customTitle)
    {
        var existing = FindBottomPanelTab(tabId);
        if (existing is not null)
        {
            ActiveBottomPanelTab = existing;
            IsBottomPanelVisible = true;
            return;
        }

        var (title, canonicalIndex) = tabId switch
        {
            OutputTabId => ("LOG OUTPUT", 0),
            _ => (customTitle ?? tabId.ToUpperInvariant(), BottomPanelTabs.Count)
        };

        var tab = new BottomPanelTabItem(tabId, title) { IsClosable = tabId != OutputTabId };

        // Insert at the canonical position (or at end if past current count)
        var insertIndex = Math.Min(canonicalIndex, BottomPanelTabs.Count);
        BottomPanelTabs.Insert(insertIndex, tab);
        ActiveBottomPanelTab = tab;
        IsBottomPanelVisible = true;

        // Persist Log Output visibility when it's re-added via Show Output
        if (tabId == OutputTabId)
        {
            _settingsService.Current.LogOutputVisible = true;
            _settingsService.QueueSave();
        }
    }

    /// <summary>
    /// Closes a bottom panel tab. If it was the active tab, activates
    /// the nearest neighbor. If no tabs remain, hides the panel.
    /// </summary>
    /// <param name="tabId">ID of the bottom panel tab to close.</param>
    [RelayCommand]
    public void CloseBottomPanelTab(string tabId)
    {
        var tab = FindBottomPanelTab(tabId);
        if (tab is null || !tab.IsClosable) return;

        var index = BottomPanelTabs.IndexOf(tab);
        BottomPanelTabs.Remove(tab);

        if (BottomPanelTabs.Count == 0)
        {
            ActiveBottomPanelTab = null;
            IsBottomPanelVisible = false;
            return;
        }

        if (ActiveBottomPanelTab == tab || ActiveBottomPanelTab is null)
        {
            var newIndex = Math.Min(index, BottomPanelTabs.Count - 1);
            ActiveBottomPanelTab = BottomPanelTabs[newIndex];
        }
    }

    /// <summary>
    /// Finds a bottom panel tab by ID, or null if not found.
    /// </summary>
    /// <param name="tabId">ID of the bottom panel tab to locate.</param>
    public BottomPanelTabItem? FindBottomPanelTab(string tabId)
    {
        for (int i = 0; i < BottomPanelTabs.Count; i++)
        {
            if (BottomPanelTabs[i].Id == tabId) return BottomPanelTabs[i];
        }
        return null;
    }

    /// <summary>
    /// Opens Settings as a tab (like VS Code).
    /// </summary>
    [RelayCommand]
    public void OpenSettings()
    {
        OpenTab(SettingsTabId, "Settings", iconGeometry: SettingsIconGeometry, isClosable: true);
    }

    // ── Helpers ──

    /// <summary>
    /// Finds a tab by its ID, or null if not found.
    /// </summary>
    /// <param name="tabId">ID of the tab to locate.</param>
    internal TabItemModel? FindTab(string tabId)
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].Id == tabId) return Tabs[i];
        }
        return null;
    }
}
