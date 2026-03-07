# Release Notes

## [0.22.0]

### What's New
- Installer and update dialog buttons now highlight with accent blue on hover
- Update notifications are now clickable — click the toast to view update details
- The update dialog stays visible while applying updates with a clear progress indicator
- Release notes in the update dialog are now beautifully formatted with markdown rendering

### Bug Fixes
- Fixed auto-update notification being easy to miss (now stays for 10 seconds and is interactive)
- Fixed update dialog disappearing immediately when clicking "Restart Now" to apply updates

## [0.19.0]

### What's New
- Custom branded installer with per-user installation (no admin required)
- In-app self-update with download progress and one-click restart
- Auto-check for updates on startup with toast notification
- Branded update dialog with version comparison and release notes
- Uninstall support with optional app data cleanup

## [0.18.0]

### What's New
- Fill profiles for haptic device axis configuration (5 built-in profiles)
- Save, rename, and delete custom fill profiles
- Generate funscript files from Pulse beat data
- Beat rate selector for funscript generation
- Amplitude-aware funscript generation matching audio waveform
- "(modified)" indicator when axis settings diverge from selected profile

### Bug Fixes
- Fixed application freeze when using Serial transport with Pulse and no funscript
- Fixed bottom panel appearing over fullscreen video on auto-play
- Fixed SyncWithStroke fill modes getting stuck at fixed position

## [0.17.0]

### What's New
- Single-instance application with file forwarding
- "On Peak & Valley" and "Mid Stroke" beat bar modes
- Configurable toast duration, fullscreen auto-hide delay, and resume playback prompt
- Toast notifications for OSR2+ connection events and Pulse state changes
- Video filename display in fullscreen overlay
- Pulse toggle button in title bar toolbar
- Improved tooltips on all interactive controls

### Bug Fixes
- Fixed screenshot button not appearing immediately when toggling setting

## [0.14.0]

### Improvements
- OSR2+ device control is now built-in (no longer requires a plugin)
- Pulse audio-to-haptics is now built-in (no longer requires a plugin)
- Playlist management is now built-in (no longer requires a plugin)
- Faster application startup
- Activity bar now shows 5 fixed icons: Explorer, OSR2+, Pulse, Playlists, Settings

## [0.13.0]

### What's New
- Playlist sidebar with drag-and-drop, save/load, and recent playlists
- Pulse audio analysis with waveform visualization and beat detection
- OSR2+ integrated sidebar with connection management and axis control
- Funscript visualizer with graph and heatmap modes
- Beat bar with multiple detection modes
