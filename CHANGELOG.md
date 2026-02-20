# Changelog

All notable changes to the Vido project will be documented in this file.

## [Unreleased]

### vi-006
- Created FileNode model in Vido.Core with lazy-loading dummy-child pattern, video extension detection, and ObservableCollection children
- Created IFileSystemService interface and FileSystemService implementation — reads directory contents sorted (dirs first, then files), skips hidden items, handles access errors gracefully
- Created FileExplorerViewModel with OpenFolder/CloseFolder commands, lazy ExpandNode, folder state persistence, and startup restore
- Created TreeViewStyles.xaml — VS Code Dark Modern styled TreeView with expand/collapse chevron, hover/selection highlights, folder/video/generic file icon geometries
- Created FileExplorerPanel.xaml — TreeView with HierarchicalDataTemplate showing folder (open/closed), video, and generic file icons; empty-state "Open Folder" button
- Enabled "File > Open Folder" menu item with OpenFolderDialog and "File > Close Folder" with dynamic enable/disable
- Updated SidebarView with ContentPresenter panel host for sidebar panel switching
- Wired sidebar panel switching in MainWindow — Explorer panel shown when active, extensible for future panels
- Last opened folder persisted in AppState and restored on startup
- Registered IFileSystemService and FileExplorerViewModel in DI container
- Added 41 new unit tests: FileNodeTests (10), FileSystemServiceTests (8), FileExplorerViewModelTests (12) — with Theory-based video extension coverage and temp directory isolation
- Total test count: 99 (all passing)

### vi-005
- Created IEventBus interface and EventBus implementation — thread-safe publish/subscribe with IDisposable subscriptions
- Created ILogService interface and LogService implementation — thread-safe in-memory logging with Debug/Info/Warning/Error levels and EntryAdded event
- Created AppSettings model with sensible defaults for volume, playback, UI layout, file explorer, and general preferences
- Created ISettingsService interface and SettingsService implementation — JSON persistence to %APPDATA%/Vido/settings.json with 500ms debounced saves
- Created AppState model for window geometry, last session info, and active sidebar panel
- Created IStateService interface and StateService implementation — JSON persistence to %APPDATA%/Vido/state.json with SemaphoreSlim concurrency protection
- Registered all services as singletons in DI container (App.xaml.cs)
- Settings and state are loaded asynchronously before MainWindow shows; saved on exit
- MainWindow restores window position, size, and maximized state from persisted AppState on startup
- MainWindow saves window geometry (using RestoreBounds when maximized) on close
- Added AllowNamedFloatingPointLiterals to StateService JSON options for NaN default support
- Added 28 unit tests: EventBusTests (8), LogServiceTests (9), SettingsServiceTests (6), StateServiceTests (5) — all passing

### vi-005 Polish
- Fixed GridSplitter divider between sidebar and editor: swapped Z-order so 1px line renders on top of transparent hit area
- Fixed settings gear icon center circle vertical alignment (Canvas.Top 8.8 → 7.8) to match gear path center
- Rounded all icon corners: Explorer rectangles (RadiusX/Y=1.5), Extensions path (StrokeLineJoin=Round), Extensions rectangle (RadiusX/Y=1), explorer lines (round line caps), window control icons (minimize/close round caps, maximize RadiusX/Y=1.5), submenu arrows (round joins and caps)
- Fixed menu targeting: stretched Menu to fill full 30px title bar height for larger click targets
- Fixed menu dropdown immediately closing: reduced popup top margin (8→2) and added VerticalOffset=-2 to eliminate dead zone between button and dropdown
- Fixed maximized window extending off-screen: added MonitorFromWindow/GetMonitorInfo to WM_GETMINMAXINFO handler to constrain ptMaxPosition and ptMaxSize to the monitor's working area
- Window border (1px) now hidden when maximized (no visible frame at screen edges)

### vi-001
- Created solution structure with 7 projects: Core, Services, ViewModels, Views, PluginHost, App, Tests
- Frameless WPF MainWindow with VS Code Dark Modern background (#1f1f1f)
- WindowChrome-based resize/move with DPI-aware 800x600 minimum size enforcement
- DI container via Microsoft.Extensions.DependencyInjection
- xUnit test infrastructure with smoke test
- VS Code launch/task configuration for one-click Run & Debug

### vi-b-001
- Eliminated resize flicker by extending DWM glass frame over client area with dark mode attributes
- Set DWM immersive dark mode and caption color to #1f1f1f so the composition surface matches the app background
- Set Win32 class background brush as fallback and suppressed WM_ERASEBKGND for defense-in-depth

### vi-002
- Created custom title bar matching VS Code Dark Modern style with app icon, title text, and window controls
- Implemented minimize, maximize/restore, and close buttons with correct hover effects (#3d3d3d standard, #c42b1c red for close)
- Added double-click title bar to toggle maximize/restore
- Created TitleBarViewModel with IWindowService abstraction for platform-agnostic window management
- Created WindowService (WPF implementation) forwarding to SystemCommands
- Window state sync for Aero Snap and external state changes (updates icon between maximize/restore)
- Started theme system: Colors.xaml (full VS Code Dark Modern palette) and Brushes.xaml (SolidColorBrush resources)
- MainWindow now uses theme resource brushes instead of hardcoded hex colors
- Added 12 unit tests for TitleBarViewModel covering commands, state sync, and property change notifications

### vi-003
- Added menu bar inline in title bar after app icon, matching VS Code integrated menu layout
- Created MenuStyles.xaml with full dark-themed templates for top-level items, dropdown items, submenu parents, and separators
- File menu: Open File, Open Folder, Close Folder, Recent Files submenu, Exit (functional)
- Edit menu: placeholder "No actions available" disabled item
- View menu: Toggle Sidebar, Toggle Status Bar, Toggle Bottom Panel, Toggle Right Panel, Fullscreen, Zoom In, Zoom Out (all with shortcut hints)
- Playback menu: Play/Pause, Stop, Skip Forward, Skip Backward, Loop, Playback Speed submenu (0.25x–2.0x)
- Help menu: About Vido, Check for Updates
- All dropdown menus styled with dark background, rounded corners, drop shadow, hover highlight, and right-aligned shortcut hints
- Submenus (Recent Files, Playback Speed) open on hover with arrow indicators
- Removed "Vido" title text from title bar (title bar shows icon + menu + drag area + window controls only)
- Normalized dropdown menu item spacing: consistent 24px left padding, 16px right padding, 24px gap before shortcut/arrow
- Added rounding design tokens: MenuPopupCornerRadius (6) for dropdown/context menu borders, MenuItemCornerRadius (4) for top-level and dropdown item highlights
- Dropdown and submenu item highlights now have 4px corner radius with 4px horizontal inset, matching VS Code style
- Menu items without handlers are visible but disabled
- Exit menu item is functional (shuts down the application)

### vi-004
- Implemented VS Code-style core layout: Title Bar → Activity Bar | Sidebar | Editor Area → Status Bar
- Created ActivityBarView (48px vertical icon strip) with Explorer, Extensions, and Settings icons
- Active activity bar icon shows white left border indicator (2px) and white icon; inactive icons are dimmed (#9d9d9d)
- Clicking active icon toggles sidebar visibility; clicking different icon switches panel and shows sidebar
- Created SidebarView (300px default, resizable 170–600px via GridSplitter) with panel header text
- Sidebar header updates to match selected panel: EXPLORER, EXTENSIONS, SETTINGS
- Created StatusBarView (22px, #181818 background) — empty placeholder for later tickets
- Editor area shows "Open a video file to begin" placeholder text centered in remaining space
- Created LayoutStyles.xaml with ActivityBarButtonStyle, SidebarHeaderStyle, and VerticalSplitterStyle
- Created SidebarPanelKind enum in Vido.Core.Layout for panel identification
- Created ActivityBarViewModel with SelectPanel command handling toggle-on-self and switch-on-different logic
- Created SidebarViewModel with SetPanel method that updates header text
- Added 17 unit tests: 14 for ActivityBarViewModel, 3 for SidebarViewModel (all passing)
- Changed status bar background to VS Code blue (#007acc)
- Changed activity bar active indicator from white to VS Code blue (#007acc) — new AccentColor/AccentBrush design token
- Activity bar hover highlight now only covers the icon area (rounded 4px inset) instead of the full button, so the left indicator is never obscured
- Added 1px divider between title bar and content area
- Changed title bar background to #181818 to match sidebar/activity bar chrome color
- Increased sidebar GridSplitter transparent hit area from 5px to 11px for easier resize grabbing
- Consolidated all accent colors into single AccentColor (#007acc) / AccentBrush — used for status bar, activity bar indicator, and menu dropdown selection highlights
- Menu dropdown and submenu item hover highlights now use AccentBrush (#007acc) instead of SelectionBackgroundBrush
- Added Accent color (#007acc) to implementation plan design system as the universal accent token
- Redesigned all activity bar icons as thin-line stroke-based paths matching VS Code Codicon style: Explorer (stacked document pages with content lines), Extensions (puzzle-piece L-shape with detached block), Settings (gear outline with center circle)
- Activity bar hover no longer shows background highlight; instead inactive icons brighten from grey (#9d9d9d) to white (#ffffff) on hover, matching VS Code behavior
