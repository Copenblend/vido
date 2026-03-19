# Release Notes

## [0.25.0]

### Bug Fixes
- Fixed Next and Previous player controls navigating through file explorer items instead of the active playlist
- Fixed shuffle/random mode choosing videos from the file explorer instead of the active playlist
- Improved audio and funscript synchronization — audio and haptic scripts now start from position zero in sync with the video, and stay in sync through playback
- Fixed audio glitch and sync offset after seeking

### Improvements
- Playlists are no longer auto-saved — all saves are now explicit. Use Save or Save As to keep your changes.

## [0.24.0]

### What's New
- You can now choose where Vido is installed — pick any folder using the new Browse button or type a custom path directly

## [0.23.0]

### Improvements
- The file explorer now shows only video files and folders, hiding non-video files for a cleaner browsing experience
- The video player seek bar now displays a visible thumb indicator showing your current playback position

### Bug Fixes
- Fixed a crash that occurred when trying to play a corrupt or incomplete video file — the app now shows an error message and continues working normally

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
- "(modified)" indicator when axis settings diverge from selected profile

### Bug Fixes
- Fixed bottom panel appearing over fullscreen video on auto-play
- Fixed SyncWithStroke fill modes getting stuck at fixed position

## [0.17.0]

### What's New
- Single-instance application with file forwarding
- "On Peak & Valley" and "Mid Stroke" beat bar modes
- Configurable toast duration, fullscreen auto-hide delay, and resume playback prompt
- Toast notifications for OSR2+ connection events
- Video filename display in fullscreen overlay
- Improved tooltips on all interactive controls

### Bug Fixes
- Fixed screenshot button not appearing immediately when toggling setting

## [0.14.0]

### Improvements
- OSR2+ device control is now built-in (no longer requires a plugin)
- Playlist management is now built-in (no longer requires a plugin)
- Faster application startup
- Activity bar now shows 4 fixed icons: Explorer, OSR2+, Playlists, Settings

## [0.13.0]

### What's New
- Playlist sidebar with drag-and-drop, save/load, and recent playlists
- OSR2+ integrated sidebar with connection management and axis control
- Funscript visualizer with graph and heatmap modes
- Beat bar with multiple detection modes
