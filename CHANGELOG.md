# Changelog

All notable changes to the Vido project will be documented in this file.

## [Unreleased]

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
