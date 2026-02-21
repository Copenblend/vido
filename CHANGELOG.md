# Changelog

All notable changes to the Vido project will be documented in this file.

## [Unreleased]

### vi-009
- Created VideoPlayerViewModel — binds to IVideoEngine, exposes PlayPause/Stop/SkipPrevious/SkipNext/ToggleMute/ToggleLoop commands, seek support with BeginSeek/EndSeek for slider dragging, position/duration text formatting, sibling video file navigation for skip prev/next, auto-advance on media end, FrameReady event forwarding
- Created VideoPlayerControl.xaml — video player tab combining video display (WriteableBitmap rendered to Image) and transport controls bar; empty state with film reel icon and "Open a video file to begin" text; seek bar with elapsed/total time labels; skip prev/next, play/pause, stop buttons; volume slider with mute toggle; loop toggle
- Created PlayerStyles.xaml — VS Code Dark Modern styled transport controls: TransportButtonStyle/PlayPauseButtonStyle (rounded hover), TransportToggleButtonStyle (accent when active), SeekSliderStyle (flat bar with accent fill, thumb visible on hover), VolumeSliderStyle (compact), TimeLabelStyle (Consolas monospace); icon geometries for play, pause, stop, skip prev/next, volume high/muted, loop, film reel
- Created VideoPlayerControl.xaml.cs — WriteableBitmap frame rendering via BeginInvoke, auto-resize on resolution change, seek slider drag start/complete handlers, visual state switching between empty/media states
- Added Video tab ("Player") — always the leftmost tab with play icon, no close button, active bottom accent line (AccentBrush), styled tab strip matching VS Code tab appearance
- Wired double-click on video files in file explorer — TreeViewItem MouseDoubleClick EventSetter triggers playback; VideoFileDoubleClicked event added to FileExplorerPanel
- Wired context menu "Play" action to trigger video playback
- Registered VideoPlayerViewModel as singleton in DI container
- Added InternalsVisibleTo for Vido.Tests in Vido.ViewModels
- Added tests: VideoPlayerViewModelTests (38) — covering initial state (12), volume clamping (5), mute/loop toggle (4), no-op commands without media (2), engine event handling (5 — state, position, frame), FormatTime formatting (5 theory cases), GetAdjacentVideoFile (1), seek begin/end (2), dispose safety (2)
- Total test count: 210 (all passing)

### vi-008
- Created PlaybackState enum (None, Playing, Paused, Stopped) in Vido.Core.Playback
- Created VideoMetadata model with full media properties (resolution, codecs, frame rate, bitrate, duration, file info, container format, audio details) and computed Resolution property
- Created FrameData model for decoded BGRA32 video frames with pixel data, dimensions, stride, and PTS
- Created IVideoEngine interface — playback control (Load/Play/Pause/Stop/Seek), state properties (Position, Duration, Volume, Mute, Loop), events (PositionChanged at ~60Hz, StateChanged, FrameReady, MediaEnded), IDisposable
- Created FFmpegInitializer — locates FFmpeg DLLs in app base directory or runtimes/win-x64/native/ (NuGet convention), validates presence of avcodec DLL, thread-safe one-time initialization via DynamicallyLoadedBindings
- Created FrameConverter — swscale-based AVFrame to BGRA32 pixel data conversion, auto-configures on format/dimension changes
- Created AudioRenderer — NAudio WASAPI audio output with buffered wave provider, volume/mute control, play/pause/stop/flush operations, graceful degradation when no audio device available
- Created FFmpegVideoEngine implementing IVideoEngine — full playback engine with: demuxing via avformat, video/audio stream detection, codec setup with multi-threaded decoding, swresample audio conversion to float interleaved, background decode thread with pause support, PTS-based frame timing, seek with codec buffer flush, loop support, position updates at ~60Hz, proper cleanup of all FFmpeg contexts
- Added FFmpeg.AutoGen.Abstractions 8.0.0 and FFmpeg.AutoGen.Bindings.DynamicallyLoaded 8.0.0 NuGet packages to Vido.Services
- Added FFmpeg.LGPL 20260220.1.0 NuGet package to Vido.Services — provides native FFmpeg DLLs (avcodec-62, avformat-62, avutil-60, swscale-9, swresample-6) automatically via NuGet runtimes convention, no manual DLL downloads required
- Added NAudio 2.2.1 NuGet package to Vido.Services for WASAPI audio output
- Enabled AllowUnsafeBlocks in Vido.Services for FFmpeg P/Invoke interop
- Added InternalsVisibleTo for Vido.Tests in Vido.Services
- Registered IVideoEngine → FFmpegVideoEngine as singleton in DI container
- FFmpeg initialization called on startup (non-fatal — logs warning if DLLs not present)
- Added tests: PlaybackStateTests (3), VideoMetadataTests (4), FrameDataTests (3), FFmpegInitializerTests (9), FFmpegVideoEngineTests (13) — 32 new tests covering models, path resolution, engine state, preconditions, and error handling
- Total test count: 172 (all passing)

### vi-007
- Created ContextMenuStyles.xaml — VS Code Dark Modern themed context menus with rounded corners, drop shadow, hover highlights, keyboard shortcut hints, and separator styling
- Created IContextMenuRegistry interface and ContextMenuRegistry implementation — thread-safe, ordered menu entry registry supporting File, Folder, and Background context targets (extensible by plugins)
- Added context menus to File Explorer: video file menu (Play, Hide from View, Reveal in File Explorer), non-video file menu (Hide from View, Reveal in File Explorer), folder menu (Hide from View, Reveal in File Explorer), background menu (Open Folder, Close Folder, Rescan Folder, Show Hidden Files toggle)
- Added RescanFolder command — re-reads directory from disk preserving expanded state; hidden files persist across rescans
- Added HideFile command — hides file/folder from explorer view (persisted in AppState.HiddenFiles, not deleted from disk)
- Added UnhideFile command — restores a hidden file/folder to normal visibility
- Added ToggleShowHiddenFiles command — toggles visibility of hidden items; when shown, hidden items appear dimmed (40% opacity) and italic
- Added IsHidden property to FileNode with INotifyPropertyChanged support — drives dimmed/italic styling via DataTrigger
- Hidden files are non-playable — Play context menu respects IsHidden flag
- Right-clicking a hidden node shows dedicated HiddenNodeContextMenu with "Unhide" and "Reveal in File Explorer"
- Added checkmark column to ContextMenuStyles for IsCheckable/IsChecked menu items (✓ character)
- Added RevealInExplorer command — opens Windows Explorer with item selected
- Added SelectedNode tracking to FileExplorerViewModel
- Hidden-file filtering handled entirely in ViewModel (ApplyHiddenFilter) — IFileSystemService uses simple signatures with no hidden-paths parameter
- Added tooltips for non-video files: "filename — Not a supported video format"
- Registered IContextMenuRegistry in DI container
- Context menu highlights use AccentBrush (#007acc) matching top-level menu style
- Removed all separators from context menus for consistent inter-item spacing; reduced top menu separator margin to 8,0
- Added thin blue scrollbar to file explorer TreeView — 2px thumb width, AccentBrush color, transparent track, matching sidebar accent indicator style
- Added smooth pixel-based scrolling to TreeView (VirtualizingPanel.ScrollUnit="Pixel")
- Added "Rescan Folder" to File dropdown menu (enabled alongside Close Folder)
- Added tests: ContextMenuRegistryTests (10), FileExplorerViewModelTests (30 — covering OpenFolder, CloseFolder, ExpandNode with hidden filter, RestoreLastFolder, RescanFolder preserving hidden state, HideFile, UnhideFile, ToggleShowHiddenFiles with tree refresh, ShowHiddenFiles filtering, SelectedNode), FileSystemServiceTests (8), FileNodeTests (+3 for IsHidden)
- Total test count: 130 (all passing)

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
