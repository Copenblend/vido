# Changelog

All notable changes to the Vido project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Solution structure with 7 projects: Core, Services, ViewModels, Views, PluginHost, App, Tests (vi-001)
- Frameless WPF MainWindow with VS Code Dark Modern background (#1f1f1f) (vi-001)
- WindowChrome-based resize/move with DPI-aware 800x600 minimum size enforcement (vi-001)
- DI container via Microsoft.Extensions.DependencyInjection (vi-001)
- xUnit test infrastructure with smoke test (vi-001)
- VS Code launch/task configuration for one-click Run & Debug (vi-001)

### Fixed
- Eliminated resize flicker and dark trailing edges by extending DWM glass frame over client area with dark mode attributes (vi-b-001)
- Set DWM immersive dark mode and caption color to #1f1f1f so the composition surface matches the app background (vi-b-001)
- Set Win32 class background brush as fallback and suppressed WM_ERASEBKGND for defense-in-depth (vi-b-001)
