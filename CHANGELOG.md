# Changelog

All notable changes to the Vido project will be documented in this file.

## [Unreleased]

### Changed
- **vido-258**: Replaced the read-only hardcoded install path display in `OptionsPage.xaml` with an editable `TextBox` bound to `Options.InstallPath` and a "Browse..." button using `OpenFolderDialog`. Added `BrowseButton_Click` handler in `OptionsPage.xaml.cs`. Browse button styled with `ButtonDefaultBackgroundBrush`/`ButtonBackgroundBrush` hover matching the Install button pattern.
- **vido-257**: Added `InstallPath` property to `InstallOptions` (defaults to `InstallEngine.DefaultInstallDir`). Updated `InstallerViewModel.InstallAsync()` and `Finish()` to use `Options.InstallPath` instead of the hardcoded default, wiring the custom path through extraction, shortcuts, file associations, registry entries, and post-install launch. Added 3 new tests.

- **vido-247**: Updated `SeekSliderThumb` style in `PlayerStyles.xaml` — replaced invisible 0px-width transparent grid with a 12px white `Ellipse` matching the `Osr2SliderThumbStyle`. Added `IsMouseOver` trigger that sets the stroke to `AccentBrush`. XAML-only change, no code-behind modifications.

- **vido-251**: Hardened script clear pipeline with comprehensive test coverage. Verified the existing `ScriptCleared` → `OnCardScriptCleared` → `ClearAxisScript` pipeline correctly stops TCode output, removes scripts from TCodeService, and allows auto-load recovery on the next video. Added 4 new tests: `ClearScript_RemovesFromTCodeScripts` (verifies `HasScriptsLoaded` becomes false after all axes cleared), `ClearScript_AutoLoadRecovery` (verifies cleared axes re-load on next video), `ClearScript_OtherAxesUnaffected` (verifies card state and TCode state for uncleared axes), `ClearScript_MultipleClearsWork` (verifies sequential clears of R0, R1, L0 with correct intermediate states).
- **vido-250**: Implemented validated script opening with TCode push. Replaced `AxisCardViewModel.ExecuteOpenScript` local parsing with `ScriptOpenRequested` event — the parent `AxisControlViewModel` now validates axis compatibility, parses the file (suffix-match or multi-axis fallback), and pushes to TCodeService. Added `FunscriptMatcher.GetAxisIdForFile()` static method for suffix-based axis detection. Added `IToastService` property on `AxisControlViewModel` for error feedback. Removed `ParseFileFunc` from `AxisCardViewModel`. Added `SetManualScript()` internal method. Updated 3 existing tests, added 8 new script-open tests and 8 `GetAxisIdForFile` theory cases (16 net new tests).
- **vido-246**: Added error handling for corrupt/unsupported video files. Wrapped `_engine.LoadAsync()` in `LoadMediaCoreAsync` with `try/catch` — on failure, logs error via `_logService.Error()`, shows error toast via `ToastService?.ShowError()`, sets `HasMedia = false`, and returns without rethrowing. Added `HasMedia` guards in `LoadAndPlayAsync` and `RestoreLastVideoAsync` to skip post-load logic on failure. Added `IToastService?` settable property on `VideoPlayerViewModel`, wired from `MainWindow.xaml.cs`. Fire-and-forget call sites (event bus subscription, playlist/sibling auto-play) are now safe from unobserved task exceptions. Updated 1 existing test, added 6 new error handling tests.
- **vido-245**: File explorer now filters out non-video files. Added `FilterVideoFiles()` method to `FileExplorerViewModel` that retains only directories, recognized video files (`FileNode.IsVideoFile`), and files matching `AdditionalAcceptedExtensions`. Applied in all 4 tree-building code paths: `OpenFolderAsync`, `ExpandNodeAsync`, `RescanFolderAsync`, and `RestoreExpandedStateAsync`. Added 5 new unit tests covering filtering in open/expand/rescan paths, `AdditionalAcceptedExtensions` retention, and empty directory preservation. Updated 15 existing tests from `.txt` to `.mp4` extensions.

### Changed
- **vido-230 (Core Optimization)**: Removed all dead code left behind by the Pulse removal epic and optimized performance-critical paths. Deleted `AudioSamplesAvailable` event and `EmitAudioSamples` from `FFmpegVideoEngine`/`IVideoEngine`. Removed 4 orphaned haptic event types (`HapticAxisConfigEvent`, `HapticScriptsChangedEvent`, `HapticTransportStateEvent`, `HapticAxisSnapshot`) and all zero-subscriber publish calls. Removed unused `SkiaSharp` package reference from `Vido.Core`. Optimized `TCodeService` by replacing 8 `Dictionary<string, ...>` fields with fixed-size arrays indexed by `AxisConfig.Ordinal`, eliminating ~32 hash lookups per `OutputTick` at 100+ Hz. Converted `InterpolationService` from `ConcurrentDictionary<string, int>` to `int[]`. Optimized `LogService` snapshot from eager per-call `ToList().AsReadOnly()` to lazy-rebuild with dirty flag. Net result: ~250 lines of dead code removed, zero dictionary operations on the hot path, zero per-log-call allocations.
- **vido-235**: Optimized `LogService` snapshot allocation by replacing eager per-call `ToList().AsReadOnly()` in `Log()` with a lazy-rebuild pattern. Added `_snapshotDirty` volatile flag set on write; `Entries` getter rebuilds snapshot via double-checked locking only when dirty. `Clear()` resets the dirty flag after writing the empty snapshot. Zero allocations per `Log()` call; snapshot created only when `Entries` is actually read. Added 2 tests.
- **vido-234**: Optimized `TCodeService` and `InterpolationService` by replacing 8 `Dictionary<string, ...>` fields with fixed-size arrays indexed by `AxisConfig.Ordinal`. Added `Ordinal` property to `AxisConfig` (assigned in `SetAxisConfigs`). Changed `InterpolationService.GetPosition` signature from `string axisId` to `int axisOrdinal` and replaced `ConcurrentDictionary<string, int>` index cache with `int[]`. Added `InterpolationService.SetAxisCount()`. Converted `IsDirty` from `string`-based to `int ordinal`-based lookup. Added `GetOrdinalForId()` helper for non-hot-path string-to-ordinal resolution. Sentinel values: `-1` (unsent TCode), `double.NaN` (inactive ramp/return), `null` (absent objects). Added `InternalsVisibleTo("Vido.Services")` and `InternalsVisibleTo("Vido.Tests")` to `Vido.Core.csproj`. Updated all tests to call `SetAxisConfigs` before `SetScripts`/`StartTestAxis` and use integer ordinals for `IsDirty`/`GetPosition`.

### Removed
- **vido-233**: Removed unused `SkiaSharp` NuGet package reference from `Vido.Core.csproj`. Zero SkiaSharp imports existed in Core — SkiaSharp is correctly consumed only in `Vido.Views` via `SkiaSharp.Views.WPF`. `SkiaSharpVersion` property retained in `Directory.Build.props`.
- **vido-232**: Removed orphaned haptic event types (`HapticAxisConfigEvent`, `HapticScriptsChangedEvent`, `HapticTransportStateEvent`, `HapticAxisSnapshot`) and all publish call sites. Deleted 4 type files and the empty `Haptics` directory. Removed `PublishTransportState()` and `BuildConnectionLabel()` from `Osr2PlusSidebarViewModel`, `PublishOsr2AxisConfig()` from `MainWindow`, and dead code in the ScriptsChanged handler (preserved auto-show visualizer logic). Cleaned up `using Vido.Core.Haptics` from 6 files. Removed 10 tests (8 from `HapticTypesTests`, 2 transport-state tests from `Osr2ViewModelTests`).
- **vido-231**: Removed dead `AudioSamplesAvailable` event from `IVideoEngine` and `FFmpegVideoEngine`, the `EmitAudioSamples()` private method and both call sites (time-stretch and direct audio paths), `AudioSampleEventArgs` type, and `AudioSampleEventArgsTests`. Zero subscribers existed after Pulse removal. Audio rendering pipeline unchanged.
- **vido-208**: Removed the Pulse beat detection feature entirely. Deleted all Pulse source code (30+ source files across Views, ViewModels, Services, Core), 7 Pulse-only haptic/event types, 10 Pulse AppSettings properties, 500+ Pulse-only tests, Pulse theme and assets, Pulse documentation and guide pages. Surgically removed Pulse integration code from MainWindow, TCodeService, AxisControlViewModel, BeatBarViewModel, BeatBarOverlay, SettingsViewModel, and ActivityBarView. Cleaned up all Pulse references from index.html, README.md, settings.md, user-interface.md, and RELEASENOTES.md. Removed dead external beat source infrastructure (`CreateExternal`, `IsExternal`) from `BeatBarMode`. Final verification confirmed zero Pulse references remain in source code (excluding `AxisFillMode.Pulse` fill pattern).

### Changed
- **vido-183**: Added dark-neutral default button background (`ButtonDefaultBackgroundColor` #313131, `ButtonDefaultBackgroundBrush`) to both installer and main app theme resources. All primary action buttons in the installer (Welcome, Options, Finish pages) and UpdateDialog (Update Now, Restart Now, Open Release Page, OK) now display a dark gray (#313131) background by default and transition to accent blue (#0078d4) on hover. Secondary buttons (Later, Cancel, Close) remain unchanged. No functional changes — visual styling only.

### Added
- **vido-186**: Added `Applying` state (6th `DialogState`) to `UpdateDialog` with a rotating Segoe MDL2 spinner icon and "Applying update, please wait..." / "Vido will restart automatically." messages. Clicking "Restart Now" now transitions to the Applying state, calls `ApplyUpdate()`, waits 2 seconds for visual feedback, then shuts down the app. The window close button (X) is hidden during the Applying state to prevent premature close. Spinner uses a 16ms `DispatcherTimer`-driven `RotateTransform` (≈60fps). Includes 3 new unit tests.
- **vido-185**: Added `ReleaseNotesProvider` in `Vido.Views.Updates` that reads the bundled `RELEASENOTES.md` and extracts version-specific sections using `## [X.Y.Z]` header parsing (with v-prefix normalization and case-insensitive matching). Updated `UpdateDialog` to prefer `RELEASENOTES.md` content over the GitHub API release body, falling back to the API content when no matching section is found. Replaced `TextBlock` with `ContentControl` in the release notes area and renders markdown via `MarkdownRenderer.Render()` for rich formatted display. Added `RELEASENOTES.md` as a Content item in `Vido.App.csproj` (CopyToOutputDirectory). Includes 14 new unit tests.
- **vido-184**: Added `ShowActionable(string message, string? boldSuffix, Action onClick, double durationSeconds)` to `IToastService` and `ToastService`. Actionable toasts are interactive: the message body is clickable (invokes callback), a close button (X) dismisses without invoking the callback, and auto-dismiss uses a custom duration (default 10 seconds). Updated `MainWindow.OnAutoUpdateTimerTick` to use `ShowActionable` instead of `Show` — the startup update toast now reads "Click to view update details" and opens the UpdateDialog when clicked. Added `OnUpdateToastClicked` and `ShowUpdateDialogWithResult` helper methods. Existing `Show` and `ShowError` behavior is unchanged. Includes 12 new unit tests.
- **vido-170**: Updated build pipeline (`build-release.ps1`) to produce a custom setup EXE instead of a WiX MSI installer. Step 5 now creates a `payload.zip` from the portable build, publishes `VidoSetup.csproj` as a self-contained single-file executable with the payload embedded via `-p:PayloadZip`, copies/renames the output to `VidoSetup-{version}.exe`, code-signs it, and cleans up intermediate files. Removed all WiX CLI references, MSI build steps, and WiX extension download hints. Added 18 structural tests validating the script content. Added retry-with-verify to all `InstallEngine` registry operations (write-marker pattern guards against zombie key handles from Windows kernel async `DeleteSubKeyTree`). Added retry to shortcut creation for transient COM/IO errors. Added xUnit `[Collection("Registry")]` to serialize `InstallEngineTests` and `InstallerViewModelTests` (prevent cross-class parallel registry races).
- **vido-169**: Auto-check updates on startup. Added one-shot `DispatcherTimer` (5-second delay) in `MainWindow` constructor that calls `CheckForUpdateAsync()` when `AppSettings.AutoCheckUpdates` is `true`. If an update is available, shows a toast notification via `ToastService.Show()`. Exceptions are silently swallowed for background checks. Added `updates.autocheck` key to `AppSettingsStore` with getter/setter. Added "Updates" category to `SettingsViewModel` with a single boolean toggle for auto-check. Includes 12 new unit tests covering setting defaults, store get/set/save/change-notification, SettingsViewModel category, service gating by setting, exception swallowing, and toast conditions. 92.66% coverage on test code.
- **vido-168**: Self-update mechanism wired end-to-end in `UpdateDialog`. Moved download logic from `MainWindow` into `UpdateDialog.DownloadUpdateAsync()` — clicking "Update Now" now triggers download with progress reporting within the dialog itself. Added `UpdateDialog(IUpdateService)` constructor for dependency injection. "Cancel" button cancels the in-progress download via `CancellationTokenSource` and returns to Info state. "Restart Now" button calls `IUpdateService.ApplyUpdate()` (launches PowerShell script that waits for process exit, extracts zip, relaunches Vido.exe) and sets `UserChoseRestart = true`. Simplified `MainWindow.ShowCheckForUpdatesMessage()` to create a single `UpdateDialog` instance with the update service — removed second-dialog download pattern. On cancellation, dialog returns to Info state instead of closing. Includes 11 new unit tests covering download success/cancellation/error transitions, correct file naming, URL passthrough, script path handling (spaces in paths), and zip cleanup ordering. 72.18% coverage on UpdateDialog.xaml.cs (uncovered: WPF runtime I/O boundary code).
- **vido-167**: Branded update dialog replacing all `MessageBox.Show` calls in the update flow. Created `UpdateDialog` (480×400 borderless dark-themed WPF window) with five visual states: Info (version comparison + release notes + "Update Now"/"Later" buttons), Downloading (progress bar + percentage + "Cancel" button), Downloaded (success icon + "Restart Now"/"Later" buttons), Error (error message + "Open Release Page"/"Close" buttons), and UpToDate ("You're running the latest version" + version display + "OK" button). Rewrote `MainWindow.ShowCheckForUpdatesMessage()` to use `UpdateDialog` with async download via `DownloadUpdateAsync` (progress reporting + cancellation support) and `ApplyUpdate` for restart. Removed `DownloadAndPromptRestartAsync` method and `_pendingInstallerPath` field. Includes 13 unit tests covering state transitions, property assignment, and dialog lifecycle. 68.63% coverage (uncovered: WPF runtime I/O boundary code — `Close()`, `DragMove()`, `Process.Start()`, `DialogResult` setters).
- **vido-166**: Enhanced update service for portable zip-based self-updates. Added `AutoCheckUpdates` setting to `AppSettings` (defaults to `true`). Extended `IUpdateService` with `DownloadUpdateAsync` (progress reporting via `IProgress<double>` + cancellation support) and `ApplyUpdate` (writes PowerShell script to `%TEMP%\Vido\Updates\apply-update.ps1` that waits for process exit, extracts zip over install dir, and relaunches Vido.exe). Changed GitHub asset search from `.msi`/`Setup` to portable `.zip` pattern matching. Separated `GenerateApplyUpdateScript` as internal static for testability. Includes 12 new unit tests covering portable zip asset detection, MSI rejection, script generation (PID, paths, PowerShell structure), download progress reporting, cancellation, and `AutoCheckUpdates` default/reset behavior. 27 total update service tests, 72% coverage (uncovered: process-launching I/O boundary code).
- **vido-165**: Added `--uninstall` CLI support and branded `UninstallDialog` in `Vido.Views`. Launching `Vido.exe --uninstall` bypasses normal startup and shows a 420×300 borderless dark-themed dialog with three states: confirm (with optional "delete settings and app data" checkbox), progress (progress bar + status), and complete. Uninstall flow removes Add/Remove Programs registry entry, selectively removes file associations (only if value is `Vido.VideoFile`), removes install path registry key, deletes Desktop and Start Menu shortcuts, optionally deletes `%APPDATA%\Vido\`, notifies Explorer via `SHChangeNotify`, and writes a `cleanup.cmd` script to `%TEMP%\Vido\` for self-deletion after exit. Constants duplicated from `InstallEngine` since `Vido.Views` does not reference `Vido.Setup`. Added `InternalsVisibleTo("Vido.Tests")` to `Vido.Views.csproj`. Includes 15 unit tests covering registry cleanup, file association removal, cleanup script generation, app data deletion, and shortcut removal.
- **vido-164**: Added installer UI with four-page wizard flow (Welcome → Options → Progress → Finish). `InstallerViewModel` orchestrates page navigation and install execution via CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). `InstallerWindow` is a borderless 500×380 window with custom title bar, drag-to-move, and `DataTrigger`-based page switching. Welcome page shows Vido branding with version info and upgrade detection. Options page offers checkboxes for desktop/Start Menu shortcuts and file associations with read-only install location display. Progress page shows styled progress bar and status text during installation. Finish page shows success with "Run Vido" checkbox. `App.xaml.cs` wired to create `InstallEngine`, `InstallerViewModel`, and `InstallerWindow` on startup. Includes 25 unit tests with 89% coverage (uncovered: runtime I/O boundary code — `Process.Start`, embedded resource loading, assembly version fallback).
- **vido-163**: Created `Vido.Setup` WPF installer project with `InstallEngine` class providing per-user install/uninstall operations: payload zip extraction with progress reporting, Add/Remove Programs registry entry management, file association registration for 7 video formats (.mp4/.avi/.mkv/.mov/.wmv/.flv/.webm), desktop and Start Menu shortcut creation via COM, and install path registry tracking. All operations target HKCU (no elevation required). Includes `InstallOptions` model, dark theme resources matching Vido's VS Code Dark Modern palette, and 25 unit tests with 100% coverage.

### Added
- **VI-0029**: Replaced CPU-burning `SpinWait` in `TCodeService.SleepPrecise` with `Thread.Sleep(1)` for the final <5 ms window. Eliminates ~20% single-core CPU usage from the TCode output thread with negligible timing impact (±1 ms jitter, well within servo motor mechanical response times).
- **VI-0028**: Throttled waveform `RepaintRequested` from ~60 Hz to ~30 Hz by skipping every other `UpdateTime()` call. `CurrentTimeSeconds` and `CurrentAmplitude` still update at full 60 Hz rate. Halves `OnPaintSurface` invocations without visible quality loss.
- **VI-0027**: Integrated `WaveformStripRenderer` into `WaveformPanelView` for GPU-accelerated bitmap-blit waveform scrolling. `OnPaintSurface` now attempts strip-based rendering first (pre-rendered bitmap + cursor overlay), falling back to the existing per-frame SKPath rendering when the strip is not yet available. Strip renderer data is updated on `FullWaveform`, `AllBeats`, and `WindowDurationSeconds` property changes. Strip renderer is disposed on Unloaded and re-created on Loaded.
- **VI-0026**: Added `WaveformStripRenderer` in `Vido.Services.Pulse` — a double-buffered off-screen waveform renderer that pre-renders a 3× canvas-width strip on a background thread and swaps it atomically for GPU-accelerated scrolling. Supports proactive re-render when the viewport approaches the strip edge, cancellation-safe rendering with periodic `CancellationToken` checks, and renders grid lines, waveform fill/outline, beat markers, and time labels.
- **VI-0019**: Added "(modified)" visual indicator in the profile toolbar that appears in italic amber (#CC9900) text when current axis settings diverge from the selected profile. Uses existing `IsProfileModified` property and `BoolToVisibility` converter.
- **VI-0018**: Added Save, Delete, and Rename profile actions to the Axis Control panel. Three Segoe MDL2 icon buttons (save, rename, delete) appear next to the profile dropdown. Save captures current axis settings via `CompleteSaveProfile` — creates new or updates existing user profiles. Delete removes user profiles and clears selection. Rename uses `InputDialog` with pre-filled current name. Rename/Delete buttons only visible for user-created profiles. `RequestProfileName` and `RequestProfileRename` events bridge ViewModel to view-layer `InputDialog`. Added `defaultValue` parameter to `InputDialog.ShowInputDialog`.
- **VI-0017**: Added Fill Profile dropdown to the Axis Control panel. New "Profile" ComboBox above the axis cards allows selecting from available fill profiles. Selecting a profile applies all axis settings (Enabled, Min, Max, FillMode, SyncWithStroke, FillSpeedHz) in a single batch update. `AxisControlViewModel` gains `SelectedProfile`, `IsProfileModified`, `AvailableProfiles`, `CanDeleteSelectedProfile`, `CanRenameSelectedProfile`, `CaptureCurrentAxes()`, and `SetProfileService()`. Manual edits after profile apply are detected via `MatchesAxes` comparison. `FillProfileService` wired in `MainWindow.xaml.cs`.
- **VI-0015 / VI-0016**: Added `FillProfileService` implementing `IFillProfileService` with JSON persistence to `%APPDATA%/Vido/fill-profiles.json`, full CRUD operations (create, update, rename, delete), case-insensitive name lookup, name validation (trim, max 50 chars, unique), thread-safe mutations, and serialized async saves. Includes 5 built-in profiles: "Default" (all axes None), "Gentle Wave" (R0/R1/R2 Sine 25-75 @ 0.5 Hz), "Full Random" (R0/R1/R2 Random 0-100), "Grinding" (R2 Square sync-with-stroke), and "Reverse Grinding" (R2 Square sync-with-stroke inverted Min/Max).
- **VI-0014**: Added Fill Profile data model classes: `FillAxisSettings` (per-axis fill configuration with Enabled, Min, Max, FillMode, SyncWithStroke, FillSpeedHz), `FillProfile` (named group of axis settings with deep-clone and equality comparison), and `IFillProfileService` interface (CRUD operations and JSON persistence contract).
- **VI-0024**: Added beat rate selector ComboBox to the Pulse sidebar, placed to the left of the "Generate Funscript" button. Offers the same four options as the beat bar rate selector ("Every Beat" through "Every 4th Beat") but is independent from live playback beat rate. New `FilterBeatsByDivisor` method in `FunscriptWriter` filters the raw beat map before generation. Setting persisted as `PulseFunscriptBeatRateIndex` in `AppSettings`.

### Fixed
- **VI-0023**: Fixed generated funscripts from Pulse using fixed 0/100 positions instead of matching the audio waveform amplitude. New `CreateActionsFromBeatMap(BeatMap)` method in `FunscriptWriter` replicates `PulseTCodeMapper`'s amplitude-aware position formula — sampling `BeatMap.WaveformSamples` at each beat timestamp and scaling stroke range by `amplitudeScale * (0.5 + 0.5 * beatStrength)`. `PulseSidebarViewModel.GenerateFunscriptAsync` now calls the new overload. Original `CreateActionsFromBeats` preserved for backward compatibility.
- **VI-0022**: Fixed SyncWithStroke fill modes (R0/R1/R2) getting stuck at a fixed position when switching from funscript to Pulse mode. The empty L0 script injected by VI-0012 was winning the stroke tracking priority check in `OutputTick`, causing `_cumulativeStrokeDistance` to never accumulate. Swapped priority so external positions (Pulse) are checked before scripts for stroke tracking.
- **VI-0021**: Fixed bottom panel appearing over fullscreen video when the next video auto-plays and scripts load. `ActivateBottomPanelTab`, `OpenBottomPanelTab`, `ToggleBottomPanel`, and `ToggleBottomPanelCollapse` now guard `IsBottomPanelVisible = true` with `if (!IsFullscreen)`. The active tab is still set correctly so the right tab is visible when exiting fullscreen.

### Added
- **VI-0020**: Added "Generate Funscript" button to the Pulse sidebar that converts beat data into a standard `.funscript` file. New `FunscriptWriter` static utility in `Vido.Services` creates alternating high/low actions from beat events and serializes to the standard funscript JSON format. Button appears when Pulse is Ready/Active with a beat map and a video loaded. Overwrite confirmation dialog shown if a `.funscript` file already exists. Publishes `FunscriptGeneratedEvent` to trigger script reload in the axis control system. Toast notification shown on success/failure.
- **VI-0013**: Fixed screenshot button not appearing immediately when toggling the "Enable Screenshot Capture" setting. `SettingDisplayItem` now routes value changes through `ISettingsStore.Set()` when a settings store is provided, ensuring `SettingChanged` fires and downstream handlers (e.g., toolbar visibility) update immediately.
- **VI-0012**: Fixed application freeze when using Serial transport with Pulse and no funscript by injecting an empty L0 funscript when Pulse suppresses funscript auto-loading. This ensures `TCodeService` always has a valid script entry during Pulse+Serial playback, routing through the safe interpolation path instead of the fill-mode code path that could trigger a race condition.
- **VI-0009**: Fixed critical application freeze when using Serial transport with Pulse (no funscript). Root cause was a race condition between `SerialPort.Close()` on the UI thread and `BaseStream.Write()` on the TCode output thread. `Disconnect()` now closes the port asynchronously on a ThreadPool thread. Added `_sendLock` to serialize concurrent serial writes. `HomeAxes()`, `SendMidpoint()`, and `SendPositionWithOffset()` now enqueue commands via the output thread instead of calling `_transport.Send()` directly from the UI thread. `StopTimer()` join timeout increased from 500ms to 1500ms with a non-blocking fallback.

### Added
- **VI-0010**: Enforced single-instance application behavior using a named Mutex and named pipe IPC. When a second instance is launched with a file argument, it forwards the file path to the running instance via named pipe and exits. The primary instance brings itself to the foreground and opens the file. Graceful fallback: if the pipe connection fails, the second instance launches normally.
- **VI-0002**: Added "On Peak & Valley" and "Mid Stroke" beat bar modes. "On Peak & Valley" detects both peaks and valleys in a single pass. "Mid Stroke" detects the midpoint of descending strokes where the funscript value crosses 50 descending, using linear interpolation for precise timing. Both modes appear in the beat bar dropdown and persist across sessions.
- **VI-0001**: Added 4 new configurable settings: Toast Notification Duration (General), Fullscreen Auto-Hide Delay, Show Video Name in Fullscreen, and Resume Playback Prompt (Playback). New "General" settings category added to the Settings page. All new properties include defaults matching previous hard-coded behavior and are persisted via JSON.
- **VI-0005**: Added toast notifications for OSR2+ connection events. Info toasts shown on connect (UDP/Serial with port details) and disconnect. Error toasts shown on connection failure and unexpected device disconnection. `IToastService` injected as optional parameter to `Osr2PlusSidebarViewModel`. `ToastService` creation moved earlier in MainWindow initialization to support OSR2+ and future Pulse toast integration.
- **VI-0006**: Added toast notifications for Pulse state changes. "Pulse enabled" info toast shown when toggling on, "Pulse disabled" when toggling off. `IToastService` injected as optional parameter to `PulseSidebarViewModel`. Toast fires regardless of trigger source (sidebar toggle or future title bar button).
- **VI-0007**: Added video filename display to fullscreen overlay. The video name (without extension) appears top-left with a semi-transparent background. Follows the same auto-hide timing as transport controls — fades in/out with mouse movement. Only visible in fullscreen mode and respects the `FullscreenShowVideoName` setting. Updates automatically when the video changes.
- **VI-0008**: Added Pulse toggle button to the title bar toolbar. Uses Segoe MDL2 heart glyphs — filled heart with accent color when Pulse is active, outline heart when inactive. Bidirectional sync with the Pulse sidebar toggle via `PropertyChanged`. Inserted between the OSR2+ Connect button and the Screenshot button, with "Toggle Pulse" tooltip.
- **VI-0001-B**: Wired new settings to their consumers. `ToastService` now accepts `ISettingsService` and reads `ToastDurationSeconds` for auto-dismiss timing. Fullscreen auto-hide timer reads `FullscreenAutoHideSeconds` from settings (re-reads on each fullscreen entry and mouse move). `VideoPlayerViewModel.RestoreLastVideoAsync()` gates the resume bar on `ResumePlaybackPrompt` setting. Removed hard-coded `FullscreenHideDelayMs` constant.
- **VI-0004**: Added and improved tooltips on all interactive controls across the application. Transport buttons now use imperative voice ("Skip to previous file", "Toggle play or pause", etc.). Added tooltips to OSR2+ sidebar controls, axis card controls, Pulse toggle, file explorer button, settings page buttons, beat bar combo box, and About dialog. Updated screenshot button tooltip to "Capture screenshot".

### Removed
- **VI-0011**: Removed defunct "Enter Repository Code..." menu item from Help menu, along with the `EnterRepositoryCodeRequested` event, `OnEnterRepositoryCodeClick` handler, and all related TODO comments.

## [0.14.0] - 2026-03-03

### Changed
- OSR2+ device control is now a built-in feature (previously a plugin).
- Pulse audio-to-haptics is now a built-in feature (previously a plugin).
- Playlist management is now a built-in feature (previously a plugin).
- All settings are in the main Settings view under OSR2+, Pulse, and Playlists categories.
- Activity bar now shows 5 fixed icons: Explorer, OSR2+, Pulse, Playlists, Settings.
- Sidebar panel switching uses `SidebarPanelKind` enum instead of string-based plugin panel IDs.

### Removed
- Plugin system removed: plugin discovery, loading, `IContributionRegistry`, `IPluginHost`, `IPluginInstaller`, plugin manager UI, and all `Wire*`/`Unwire*` wiring infrastructure.
- Extensions panel removed from activity bar.
- `SettingContribution` replaced by compile-time-safe `SettingDefinition` with getter/setter delegates.
- Plugin button drag-and-drop reordering removed from activity bar.
- `Vido.PluginHost` project removed from solution.
- Custom `BoolToVisibilityConverter` removed (replaced by WPF built-in `BooleanToVisibilityConverter`).
- Stale `Vido.Core` and `Vido.Haptics` NuGet packages removed from `local-packages/`.

### Improved
- Faster application startup (no plugin loading overhead).
- Compile-time type safety for all features — no runtime reflection or dynamic loading.
- All feature services directly instantiated with strong typing.
- Clean shutdown with proper disposal of all OSR2+, Pulse, and Playlist resources.

### Integration Details (PI-023 through PI-031)

#### PI-031: Final Changelog
- Consolidated all plugin integration changes into structured changelog entry.

#### PI-030: Integration Verification  
- Verified all DI registrations, feature setup methods, settings categories, activity bar buttons, and event bus subscriptions.
- Confirmed zero orphaned plugin references in source code.
- All 1617 unit tests pass.

#### PI-029: Solution & Build Cleanup
- Verified `Vido.sln` contains only: Core, Services, ViewModels, Views, App, Tests.
- No NuGet references to `Vido.Core` or `Vido.Haptics` packages.
- `InternalsVisibleTo("Vido.Tests")` already present in `Vido.Services`.
- Removed 6 stale `.nupkg` files from `local-packages/`.
- Build: 0 errors, 0 warnings. Tests: 1617/1617 passed.

#### PI-027: Deduplicate WPF Converters
- Audited all converters in `Vido.Views/Converters/` — no duplicates exist within the main solution.
- Replaced custom `BoolToVisibilityConverter` with WPF's built-in `BooleanToVisibilityConverter` in 4 XAML files: `Osr2Plus/SidebarView.xaml`, `Osr2Plus/AxisControlView.xaml`, `Osr2Plus/AxisCardView.xaml`, `Pulse/PulseSidebarView.xaml`.
- Removed `converters` xmlns from `Osr2Plus/SidebarView.xaml` and `Osr2Plus/AxisControlView.xaml` (no longer needed after BoolToVisibilityConverter removal).
- Deleted `src/Vido.Views/Converters/BoolToVisibilityConverter.cs` — replaced by WPF built-in `BooleanToVisibilityConverter`.
- All remaining converters (9 classes) are unique with no duplicates: `HexToBrushConverter`, `HexToLowOpacityBrushConverter`, `FillModeDisplayConverter`, `BeatBarModeDisplayConverter`, `StateColorToBrushConverter`, `FractionToPercentConverter`, `NotNullToBoolConverter`, `StringNotEmptyToVisibilityConverter`, `StringToBoolConverter`, `StringToGeometryConverter`.
- All XAML converter references use `Vido.Views.Converters` namespace (or WPF built-in types).

### Plugin Integration (PI-026)
- Confirmed all plugin wiring methods already removed from `MainWindow.xaml.cs` (completed in PI-020).
- No `SetupPluginContributions()`, `WirePluginContributions()`, `UnwireStaleContributions()`, or any `Wire*`/`Unwire*` methods remain.
- No tracking fields (`_wiredBottomPanelIds`, `_wiredStatusBarIds`, etc.) remain.
- No `IContributionRegistry`, `IPluginHost`, `IPluginInstaller` constructor parameters remain.
- No `_pluginManagerVm` field or `ContributionsChanged` subscription remain.
- `SidebarView` content switching uses `SidebarPanelKind` enum (Explorer, Osr2Plus, Pulse, Playlists).
- `SetupOsr2Plus()`, `SetupPulse()`, `SetupPlaylists()` are the only feature wiring methods.
- No code changes required — all work was completed as part of PI-020.

### Plugin Integration (PI-025)
- Confirmed `VideoPlayerViewModel` already uses direct `IPlaylistProvider?` injection (completed in PI-020).
- No `IContributionRegistry` dependency remains in `VideoPlayerViewModel`.
- Playlist-based skip next/prev navigation delegates to `IPlaylistProvider` when active.
- Falls back to sibling file list when provider is null or inactive.
- `PlaylistProviderDelegationTests.cs` (309 lines) already provides comprehensive coverage.
- No code changes required — all work was completed as part of PI-020.

### Plugin Integration (PI-024)
- Removed Extensions button from activity bar (replaced by dedicated feature panel buttons already present).
- Removed plugin sidebar button drag-and-drop reordering infrastructure from `ActivityBarView.xaml.cs` (`AddPluginButton`, `InsertPluginButton`, `RemovePluginButton`, `SetPluginButtonActive`, `PluginButtonReordered` event, and all drag-drop handlers).
- Removed `PluginButtonsPanel` StackPanel from `ActivityBarView.xaml`.
- Removed `PluginButtonReordered` event subscription from `MainWindow.xaml.cs`.
- Activity bar now shows 5 fixed icons: Explorer, OSR2+, Pulse, Playlists, Settings (bottom).
- No `PluginSidebarItem` references remain in the codebase.

### Plugin Integration (PI-023)
- Refactored `SettingDisplayItem` to use `SettingDefinition` with getter/setter delegates instead of `SettingContribution` + `ISettingsStore`.
- Refactored `SettingsViewModel` to build feature categories directly using `SettingDefinition` with compile-time-safe `AppSettings` property accessors.
- Removed `SettingContribution` class (`src/Vido.Core/Settings/SettingContribution.cs`) — fully replaced by `SettingDefinition`.
- Removed `ISettingsStore` dependency from `SettingDisplayItem` and `SettingsViewModel`; now use `ISettingsService` directly.
- Removed `IsPlugin` property from `SettingsCategoryViewModel` and plugin icon from `SettingsPage.xaml`.
- Removed `RefreshRegistryUrls()` from `SettingsPage.xaml.cs` and `SettingsViewModel` (no longer needed without plugin registry URLs).
- Removed `AppSettingsStore` parameter from `SettingsPage` constructor; simplified to `(ISettingsService)`.
- Added new settings categories: OSR2+ (6 settings), Pulse (3 settings), Playlists (1 setting).
- Screenshot directory visibility now uses `PropertyChanged` observation instead of `ISettingsStore.SettingChanged` event.
- Updated all tests to use `SettingDefinition` + `ISettingsService` pattern; test count increased from 1606 to 1614.

### Plugin Integration (PI-020)
- Removed the entire plugin system infrastructure from the Vido host application.
- Deleted `src/Vido.Core/Plugin/` directory (31 files: IPluginHost, IContributionRegistry, IPluginInstaller, PluginManifest, PluginInfo, etc.).
- Deleted `src/Vido.Services/Plugin/` directory (PluginInstaller.cs).
- Deleted `src/Vido.PluginHost/` project entirely and removed from solution file.
- Moved `IPlaylistProvider` from `Vido.Core.Plugin` to new `Vido.Core.Playlists` namespace.
- Created `ISettingsStore` interface in `Vido.Core.Settings` (replacement for `IPluginSettingsStore`).
- Created `SettingContribution` class in `Vido.Core.Settings` (relocated from deleted Plugin namespace).
- Updated `AppSettingsStore` to implement `ISettingsStore`.
- Removed all plugin DI registrations from `App.xaml.cs` (ContributionRegistry, IPluginHost, IPluginInstaller).
- Removed plugin activation/deactivation lifecycle from `App.xaml.cs` startup and shutdown.
- Removed Plugin Manager UI: `PluginManagerViewModel`, `PluginItemViewModel`, `PluginDetailPanel`, `PluginManagerPanel`.
- Removed all plugin contribution wiring infrastructure from `MainWindow.xaml.cs` (~700 lines of WirePlugin* methods).
- Updated `VideoPlayerViewModel` to accept `IPlaylistProvider?` directly instead of via `IContributionRegistry`.
- Updated `SettingsViewModel` to use `ISettingsStore` instead of `IPluginSettingsStore`; gutted plugin settings building.
- Updated `FileExplorerPanel` to use `Dictionary<string, string> FileIcons` instead of `IContributionRegistry`.
- Removed `SettingsPage` `IPluginHost` constructor parameter.
- Deleted 10 plugin-related test files; fixed 8 remaining test files with updated type references.

## [0.13.0] - 2026-02-28

### Plugin Integration (PI-019)
- Wired Playlists into MainWindow with `SetupPlaylists()` method following OSR2+/Pulse patterns.
- Created `PlaylistFileService`, `DialogService`, `ToastService`, and `PlaylistProvider` service instances.
- Created `PlaylistViewModel` with full DI wiring (video engine, event bus, settings, toast, playlist provider).
- Registered `PlaylistSidebarView` as sidebar content for `SidebarPanelKind.Playlists`.
- Registered status bar item (`playlists.status`, Left-aligned, order 100) with `PropertyChanged` binding.
- Registered context menu entry "Add to Playlist" for video files in the file explorer.
- Registered `PlaylistProvider` with `IContributionRegistry` for transport control integration.
- Added `.vidpl` to file explorer accepted extensions.
- Added Playlists case to `OnPanelChanged` sidebar panel switching (replaced TODO comment).

### Plugin Integration (PI-001)
- Added `SkiaSharp` 2.88.9 package reference to `Vido.Core` for `IExternalBeatSource` strong-typed canvas rendering.

### Plugin Integration (PI-018)
- Integrated Playlist ViewModel, Views, and ToastService into the host application.
- Created `PlaylistViewModel` in `Vido.ViewModels/Playlists/` — full playlist management with add/remove/move items, drag-and-drop, save/load, recent playlists, auto-save, and playback integration.
- Created `PlaylistItemViewModel` in `Vido.ViewModels/Playlists/` — wraps `PlaylistItem` model for UI display with file-exists checking and playing state.
- Created `IToastService` interface in `Vido.Services/Playlists/` — abstraction for toast notifications (deviation: extracted interface to break circular dependency between ViewModels and Views).
- Created `ToastService` in `Vido.Views/Services/` implementing `IToastService` — VS Code-style toast notifications with fade animations.
- Created `PlaylistSidebarView.xaml` and code-behind in `Vido.Views/Playlists/` — sidebar panel with drag-and-drop, context menu, recent playlists dropdown.
- Created `PlaylistStyles.xaml` in `Vido.Views/Themes/` — dark theme resource dictionary with prefixed keys to avoid conflicts.
- Copied 2 icon assets (`Playlist-plugin.png`, `sidebar-icon.png`) to `Vido.Views/Assets/Playlists/`.
- Refactored `PlaylistViewModel` constructor: replaced `IPluginSettingsStore` with `ISettingsService`, removed `Action<string>?` callback, uses `CommunityToolkit.Mvvm.Input.RelayCommand`.
- Settings access refactored to `ISettingsService.Current.PlaylistAutoSave/RecentPlaylists/LastPlaylistPath`.
- Added 86 unit tests (14 PlaylistItemViewModel + 72 PlaylistViewModel). Total: 1952.

### Plugin Integration (PI-017)
- Integrated 4 Playlist service types: `PlaylistFileService`, `PlaylistProvider`, `IDialogService`, `DialogService`.
- `PlaylistFileService`: JSON serialization/deserialization of `.vidpl` playlist files using `System.Text.Json` with internal DTOs.
- `PlaylistProvider`: `IPlaylistProvider` implementation with Fisher-Yates shuffle, wrap-around navigation, video-only file filtering via `FileNode.VideoExtensions`.
- `IDialogService`: Abstraction for file save/open and confirmation dialogs (in `Vido.Services/Playlists/`).
- `DialogService`: WPF implementation of `IDialogService` using `Microsoft.Win32` dialogs (in `Vido.Views/Playlists/` — deviation due to WPF dependency).

### Plugin Integration (PI-016)
- Integrated 3 Playlist model types into `Vido.Core/Models/Playlists/`: `Playlist`, `PlaylistItem`, `RangeObservableCollection<T>`.
- `Playlist`: Ordered item collection with dirty tracking, `INotifyPropertyChanged` support, auto-dirty on item collection changes.
- `PlaylistItem`: File reference with case-insensitive `IEquatable<PlaylistItem>` equality, `FileName` derived from path.
- `RangeObservableCollection<T>`: Bulk `AddRange`, `RemoveRange`, `ReplaceAll` with single-notification reset, suppresses per-item notifications during bulk ops.

### Plugin Integration (PI-015)
- Added `SetupPulse()` method to `MainWindow.xaml.cs` wiring all Pulse services, ViewModels, views, event subscriptions, and UI contributions.
- Created and wired Pulse services: `FfmpegAudioDecoder`, `AudioPreAnalysisService`, `LiveAmplitudeService`, `PulseTCodeMapper`, `PulseEngine`.
- Registered 4 UI contribution points: sidebar panel (`SidebarPanelKind.Pulse`), bottom panel tab ("PULSE WAVEFORM"), status bar item ("Pulse Status", priority 600), control bar (`BeatRateComboBox` with visibility toggle).
- Wired 3 `IVideoEngine` events to `PulseEngine`: `AudioSamplesAvailable`, `PositionChanged` (with waveform time update), `SeekCompleted`.
- Wired `SuppressFunscriptEvent` subscription to auto-show Pulse Waveform bottom panel when Pulse suppresses funscripts.
- Added `IVideoEngine` constructor parameter to `MainWindow` for direct engine event access.
- Added Pulse sidebar button to `ActivityBarView` with sidebar-icon.png, `OnPulseClick` handler, and active state tracking.
- Added `SidebarPanelKind.Pulse` case to `OnPanelChanged` for sidebar content switching.
- Added Pulse resource disposal in `OnClosing`: unsubscribes engine events, disposes subscriptions, ViewModels, engine, and pre-analysis service.
- Restored persisted `PulseUsePulse` toggle state on startup.

### Plugin Integration (PI-014)
- Integrated 2 Pulse ViewModel types into `Vido.ViewModels/Pulse/`: `PulseSidebarViewModel`, `WaveformViewModel`.
- Integrated 3 Pulse XAML view pairs into `Vido.Views/Pulse/`: `PulseSidebarView`, `WaveformPanelView`, `BeatRateComboBox`.
- Created `PulseStyles.xaml` dark theme with colors, brushes, toggle, progress bar, scrollviewer, combobox, combobox item styles.
- Merged `PulseStyles.xaml` into `App.xaml` resource dictionaries.
- Copied 2 icon assets (`Pulse-plugin.png`, `sidebar-icon.png`) to `Assets/Pulse/` as embedded resources.
- `PulseSidebarViewModel`: State display, BPM readout, analysis progress, toggle on/off via `PulseEngine.SetEnabled`, beat rate selection with 4 divisor options, settings persistence for `PulseUsePulse` and `PulseBeatRateIndex`.
- `WaveformViewModel`: Waveform data binding, beat markers, playback position tracking, window duration selection (10s/30s/60s/2m/5m), `RepaintRequested` event for SkiaSharp canvas invalidation, settings persistence for `PulseWaveformWindowDuration`.
- `WaveformPanelView` code-behind: Full SkiaSharp rendering with pre-allocated paints/paths, waveform path caching, beat marker rendering, playback cursor.
- Added `InternalsVisibleTo("Vido.ViewModels")` and `InternalsVisibleTo("Vido.Views")` to `Vido.Services.csproj` for `PulseEngine` access.
- Added 66 unit tests covering PulseSidebarViewModel (constructor, null guards, defaults, UsePulse toggle with persistence, state changes via reflection callbacks, BPM/progress updates, status messages, beat rate selection, dispose) and WaveformViewModel (constructor, null guards, defaults, window duration with persistence, UpdateTime, Clear, engine state callbacks, BeatMap ready, RepaintRequested event, dispose safety).

### Plugin Integration (PI-013)
- Integrated 5 Pulse real-time service files into `Vido.Services/Pulse/`: `AudioRingBuffer`, `LiveAmplitudeService`, `PulseTCodeMapper`, `PulseEngine`, `PulseBeatSource`.
- Changed namespace from `PulsePlugin.Services` to `Vido.Services.Pulse`.
- Updated model imports from `PulsePlugin.Models` to `Vido.Core.Models.Pulse`.
- Updated haptic imports from `Vido.Haptics` to `Vido.Core.Haptics`.
- Refactored `PulseBeatSource.RenderBeat` and `RenderIndicator` to accept strongly-typed `SKCanvas` parameter (removed `object` cast guards).
- Removed `string? currentMediaPath` constructor parameter from `PulseEngine` (media path tracked via `VideoLoadedEvent` subscription).
- Added 99 unit tests covering AudioRingBuffer (write/read/overflow/wrap-around/concurrent), LiveAmplitudeService (submit/process/start/stop/reset/events), PulseTCodeMapper (null/empty/position range/waveform shape/binary search/reset), PulseEngine (state machine/enable-disable/analysis/playback/position/seek/divisor/dispose), and PulseBeatSource (contract/rendering/visual output/strong typing).

### Plugin Integration (PI-012)
- Integrated 7 Pulse audio analysis service files into `Vido.Services/Pulse/`: `IAudioDecoder`, `AudioChunk`, `FfmpegAudioDecoder`, `OnsetDetector`, `BpmEstimator`, `AmplitudeTracker`, `AudioPreAnalysisService`.
- Changed namespace from `PulsePlugin.Services` to `Vido.Services.Pulse`.
- Updated model imports from `PulsePlugin.Models` to `Vido.Core.Models.Pulse`.
- Verified FFmpeg.AutoGen.Abstractions and AllowUnsafeBlocks compile correctly for `FfmpegAudioDecoder` unsafe interop.
- Added 56 unit tests covering OnsetDetector (FFT, onset detection, sensitivity, buffer reuse), BpmEstimator (interval estimation, quantization, circular buffer wrap), AmplitudeTracker (RMS, downmix, byte buffer conversion), AudioPreAnalysisService (integration, progress, cancellation), and AudioChunk properties.

### Plugin Integration (PI-011)
- Copied 5 Pulse model types into `Vido.Core/Models/Pulse/`: `BeatEvent`, `BeatMap`, `BpmEstimate`, `PulseAnalysisResult`, `PulseState`.
- Changed namespace from `PulsePlugin.Models` to `Vido.Core.Models.Pulse`.
- Added 16 unit tests for model defaults, init properties, enum values, and BeatMap sorted data.

### Plugin Integration (PI-010)
- Wired OSR2+ feature directly into `MainWindow` via `SetupOsr2Plus()` method, replacing plugin-based `Osr2PlusPlugin.Activate()` with integrated architecture.
- Created and wired all OSR2+ services (`TCodeService`, `InterpolationService`, `FunscriptParser`, `FunscriptMatcher`, `BeatDetectionService`) and ViewModels (`Osr2PlusSidebarViewModel`, `AxisControlViewModel`, `VisualizerViewModel`, `BeatBarViewModel`) with manual instantiation.
- Registered 6 UI contribution points: sidebar panel (`SidebarPanelKind.Osr2Plus`), bottom panel tab ("Funscript Visualizer"), right panel ("Axis Settings"), status bar item ("OSR2+ Status"), toolbar button (Quick Connect with highlight on connection), control bar (BeatBar ComboBox + SkiaSharp overlay).
- Wired 8 event bus subscriptions: `VideoLoadedEvent` (load scripts, sync speed), `VideoUnloadedEvent` (clear scripts, stop TCode, home axes), `PlaybackStateChangedEvent` (start/stop TCode), `PlaybackPositionChangedEvent` (update time, sync speed), `ExternalBeatSourceRegistration`, `ExternalBeatEvent`, `SuppressFunscriptEvent`, `ExternalAxisPositionsEvent`.
- Wired cross-feature coordination: script changes → visualizer + beat bar + `HapticScriptsChangedEvent` publish + auto-show visualizer, axis config changes → `HapticAxisConfigEvent` publish, device connection → toolbar highlight + status bar text, sidebar buttons → panel show requests, beat bar mode → overlay visibility.
- Added OSR2+ button to `ActivityBarView` with sidebar-icon.png and panel selection via `SidebarPanelKind.Osr2Plus`.
- Added `SidebarPanelKind.Osr2Plus` case to `OnPanelChanged` for built-in sidebar content switching.
- Registered `.funscript` extension in `FileExplorerViewModel.AdditionalAcceptedExtensions`.
- Added `IEventBus` constructor parameter to `MainWindow` for direct event bus access.
- Added `Vido.Services` project reference to `Vido.Views.csproj` for service access.
- Added `InternalsVisibleTo("Vido.Views")` to `Vido.ViewModels.csproj` for `FileDialogFactory` access.
- Added TCode and subscription disposal in `OnClosing` for clean shutdown.
- Wired file dialog factory for manual funscript loading and speed ratio synchronization.

### Plugin Integration (PI-009)
- Integrated 6 OSR2+ XAML view pairs (`SidebarView`, `AxisControlView`, `AxisCardView`, `VisualizerView`, `BeatBarComboBox`, `BeatBarOverlay`) into `Vido.Views/Osr2Plus/` namespace.
- Created `RangeSlider` custom control in `Vido.Views/Controls/` with dual-thumb range selection, keyboard support, and default `ControlTemplate` in `Themes/RangeSliderGeneric.xaml`.
- Created 7 value converters in `Vido.Views/Converters/`: `BeatBarModeDisplayConverter`, `BoolToVisibilityConverter`, `FillModeDisplayConverter`, `HexToBrushConverter`, `HexToLowOpacityBrushConverter`, `StateColorToBrushConverter`, `FractionToPercentConverter`.
- Created `Osr2PlusStyles.xaml` dark theme with colors, brushes, scrollbar, button, textbox, combobox, checkbox, toggle switch, chevron, slider, and custom control styles.
- Added `Themes/Generic.xaml` for WPF custom control default template resolution.
- Merged `Osr2PlusStyles.xaml` into `App.xaml` resource dictionaries.
- Copied 8 icon assets (`connect-dot-green/red`, `connect-icon`, `funscript-pitch/roll/stroke/twist`, `sidebar-icon`) to `Assets/Osr2Plus/` as embedded resources.
- Updated all XAML namespace declarations for integrated architecture (`Vido.Views.Osr2Plus`, `Vido.Views.Controls`, `Vido.Views.Converters`, cross-assembly references to `Vido.Core.Models.Osr2Plus` and `Vido.ViewModels.Osr2Plus`).

### Plugin Integration (PI-008)
- Integrated 5 OSR2+ ViewModel types from `Osr2PlusPlugin.ViewModels` into `Vido.ViewModels/Osr2Plus/` namespace.
- `Osr2PlusSidebarViewModel`: Connection management (UDP/Serial), output rate, global offset, transport lifecycle with `HomeAxes` startup, `HapticTransportStateEvent` publishing via `IEventBus`, injectable `TransportFactory`/`PortLister` for testing.
- `AxisControlViewModel`: 4-axis card orchestration (L0/R0/R1/R2), funscript auto-loading (multi-axis first, individual fallback), manual override persistence, `SuppressFunscriptEvent` handling, test mode lifecycle, injectable `FindMatchingScriptsFunc`/`TryParseMultiAxisFunc`/`ParseFileFunc`.
- `AxisCardViewModel`: Individual axis card wrapping `AxisConfig`, min/max/enabled/fill mode/sync/fill speed/position offset properties, script loading with `FileDialogFactory`/`ParseFileFunc` injection, manual vs auto-loaded script tracking.
- `VisualizerViewModel`: Funscript visualizer mode selection (Graph/Heatmap), configurable time window (30s–5min), loaded axis tracking, static axis color/name maps.
- `BeatBarViewModel`: Beat bar mode management (Off/OnPeak/OnValley + external sources), `BeatDetectionService` integration, dynamic `AvailableModes` rebuilding on external source registration/unregistration, deferred external mode resolution, pre-external mode save/restore, `HidesBuiltInModes` support.
- Refactored all ViewModels from `IPluginSettingsStore` (string-keyed `Get`/`Set`) to `ISettingsService` (strongly-typed `AppSettings` properties + `QueueSave()`).
- Replaced plugin-internal `RelayCommand` with `CommunityToolkit.Mvvm.Input.RelayCommand`.
- Removed `OnSettingChanged` handlers from all ViewModels (no longer needed — settings only change from VM itself in integrated architecture).
- Added `Vido.Services` project reference to `Vido.ViewModels.csproj` for TCode/Funscript/BeatDetection service access.
- Added 93 unit tests covering settings persistence, property clamping, command execution, connect/disconnect lifecycle, transport state publishing, script loading/clearing/suppression, beat detection, external beat source registration/unregistration, deferred mode resolution, axis card config changes, and PropertyChanged notifications.
- Added `System.IO.Ports` 8.0.0 package reference to `Vido.Services` for serial transport support.
- Added `SkiaSharp.Views.WPF` 2.88.9 package reference to `Vido.Views` for SkiaSharp WPF rendering controls.
- Added `SkiaSharpVersion`, `SkiaSharpViewsWpfVersion`, `SystemIOPortsVersion` centralized version properties to `Directory.Build.props`.
- Removed NuGet packaging configuration from `Vido.Core` (`GeneratePackageOnBuild`, `PackageId`, etc.) — Vido.Core is no longer published as an external package.

### Plugin Integration (PI-002)
- Integrated all 10 haptic contract types from `Vido.Haptics` into `Vido.Core/Haptics/` namespace.
- Strong-typed `IExternalBeatSource.RenderBeat` and `RenderIndicator` methods from `object` to `SKCanvas`.
- Updated XML documentation to reflect integrated architecture (replaced "plugin" references with "feature").

### Plugin Integration (PI-007)
- Integrated 3 OSR2+ TCode engine types from `Osr2PlusPlugin.Services` into `Vido.Services/Osr2Plus/` namespace.
- `TCodeService`: Full TCode output engine with dedicated background thread (`ThreadPriority.AboveNormal`), `Stopwatch`-based time extrapolation, zero-allocation hot-path byte buffer formatting, configurable output rate (30–200 Hz), dirty-value tracking (≥1 change threshold), fill mode orchestration (7 waveform types + random), return-to-center animation (exponential smoothing), ramp-up blending, test-mode oscillation with smooth speed/amplitude transitions, external axis position support, homing sequence, and per-axis position offset (L0/R1/R2 percentage, R0 modular degrees).
- `PatternGenerator`: Static waveform calculator for 7 fill modes (Triangle, Sine, Saw, SawtoothReverse, Square, Pulse, EaseInOut) returning 0.0–1.0 position values with smooth cosine transitions at direction changes.
- `RandomPatternGenerator`: Cosine-interpolated random movement generator with configurable min/max range, 20% minimum distance constraint, and optional seeded RNG for deterministic test output.
- Changed `TestAxisState` visibility from `internal` to `public` in `Vido.Core.Models.Osr2Plus` to enable cross-assembly access from `Vido.Services`.
- Added 101 unit tests covering `PositionToTCode` (scaling, min/max, clamping), `IsDirty` (threshold tracking), `AxisOrdinal` (known/unknown axes), `FormatTCodeCommand` (linear/rotation prefix, formatting), `ApplyPositionOffset` (L0/R0/R1/R2 modes, clamping, modular wrapping), `ClampPitchFillPosition` (pitch vs non-pitch), `SetOutputRate` (clamping 30–200), `SetScripts`/`HasScriptsLoaded`, time extrapolation, test mode lifecycle (start/stop/update/stopAll/events), `PatternGenerator` (all 7 modes: range, periodicity, waveform shape), `RandomPatternGenerator` (range, seeded determinism, smooth transitions, reset), `HomeAxes`/`SendPositionWithOffset` (transport integration), and `SleepPrecise` timing.

### Plugin Integration (PI-006)
- Integrated 5 OSR2+ funscript services from `Osr2PlusPlugin.Services` into `Vido.Services/Osr2Plus/` namespace.
- `FunscriptParser`: Streaming `Utf8JsonReader`-based parser with single-axis `Parse()`/`ParseFile()` and multi-axis `TryParseMultiAxis()` support, UTF-8/UTF-16 BOM handling, pre-allocated action lists, and conditional sorting.
- `FunscriptMatcher`: Convention-based matching (`video.funscript → L0`, `.twist. → R0`, `.roll. → R1`, `.pitch. → R2`) with case-insensitive file search.
- `FunscriptLoadingService`: Orchestrates multi-axis-first loading, individual file fallback, and manual override persistence with `ScriptsChanged` event.
- `InterpolationService`: Linear interpolation with per-axis cached-index O(1) sequential advancement and O(log n) binary search fallback for seeks.
- `BeatDetectionService`: Peak/valley detection from funscript action data for beat bar visualization.
- Added 55 unit tests covering parsing (valid/invalid/malformed JSON, BOM handling, multi-axis, unsorted actions, clamping), matching (all axes, case-insensitive, empty), interpolation (boundary, midpoint, sequential, seek-back, reset, zero-range), beat detection (peaks, valleys, plateau, flat), and loading service (auto-match, multi-axis, overrides, events, clear).

### Plugin Integration (PI-005)
- Integrated 3 OSR2+ transport service types from `Osr2PlusPlugin.Services` into `Vido.Services/Osr2Plus/` namespace.
- `ITransportService`: Transport abstraction with `IsConnected`, `ConnectionLabel`, `ConnectionChanged`/`ErrorOccurred` events, `Send(string)`/`Send(ReadOnlySpan<byte>)`, and `Disconnect()`.
- `SerialTransportService`: COM port transport with thread-safe lock-based state management, `Connect(portName, baudRate)`, `ListPorts()`, stackalloc-based string-to-bytes encoding.
- `UdpTransportService`: UDP localhost transport with `Connect(port)`, datagram send, and clean reconnection lifecycle.
- Added 22 unit tests covering default state, connect/send/disconnect lifecycle, error handling, reconnection, data verification via UDP listener, and event firing.

### Plugin Integration (PI-004)
- Integrated 8 OSR2+ model types from `Osr2PlusPlugin.Models` into `Vido.Core/Models/Osr2Plus/` namespace.
- `AxisConfig`: Observable per-axis configuration with `INotifyPropertyChanged`, value clamping, derived properties (`RangeLabel`, `IsStroke`, `IsPitch`, `AvailableFillModes`, `HasScript`), and `CreateDefaults()` factory producing L0/R0/R1/R2 axes.
- `AxisFillMode`: 9-member enum (None, Random, Triangle, Sine, Saw, SawtoothReverse, Square, Pulse, EaseInOut).
- `BeatBarMode`: Sealed value-object class with static `Off`/`OnPeak`/`OnValley` instances, `CreateExternal()` factory, `IEquatable<BeatBarMode>` equality by `Id`.
- `BeatDetectionMode`, `ConnectionMode`, `VisualizationMode`: Simple enums for device configuration.
- `FunscriptData` and `FunscriptAction`: Funscript file data model with record-based actions.
- `TestAxisState`: Internal ephemeral state for axis test-pattern generation.
- Added 60 unit tests covering all model types, property clamping, change notification, equality, and factory methods.

### Plugin Integration (PI-003)
- Added `AxisSettingsData` class for per-axis OSR2+ configuration (`Min`, `Max`, `Enabled`, `FillMode`, `SyncWithStroke`, `FillSpeedHz`, `PositionOffset`) with `CreateDefaults()` factory for L0/R0/R1/R2 axes.
- Added `SettingDefinition` and `SettingValidation` records for compile-time-safe Settings UI descriptions with typed getter/setter delegates.
- Added 21 feature settings to `AppSettings`: OSR2+ Connection (4), OSR2+ Output (2), OSR2+ Visualizer (2), OSR2+ Runtime (2), OSR2+ Per-Axis (1 Dictionary), Pulse Detection (2), Pulse Visualizer (1), Pulse Runtime (2), Playlist (3).
- Updated `AppSettings.ResetToDefaults()` to reset all new feature settings.
- Replaced `SidebarPanelKind.Extensions` with `Playlists`, `Osr2Plus`, `Pulse` enum members.
- Removed all plugin settings properties from `AppSettings`: `PluginInstalledSectionExpanded`, `PluginAvailableSectionExpanded`, `PluginDirectories`, `DisabledPluginIds`, `PluginSidebarOrder`, `PluginRegistryUrls`, `OfficialRegistryUrl`, `NsfwRegistryUrl`, `OfficialRegistryUrls`, `ResolveRepositoryCode()`.
- Deleted `PluginSidebarItem` class (replaced by feature-specific enum members).
- Updated all downstream consumers (`SettingsService`, `AppSettingsStore`, `SidebarViewModel`, `ActivityBarViewModel`, `ActivityBarView`, `MainWindow`, `PluginManagerViewModel`, `PluginHost`) with TODO stubs for PI-021/PI-022.
- Added `additionalScanDirectories` constructor parameter to `PluginHost` for test-injectable scan directory support.
- Added `LoadAsync(IEnumerable<string>?)` overload to `PluginManagerViewModel` for test-injectable registry URLs.

### vido-132
- Cached `VideoPlayerControl` render dispatch callback (`_renderAction`) and switched frame render enqueues to reuse the cached delegate, removing per-frame method-group delegate allocations.
- Added XML `<summary>` documentation for spinner lifecycle methods in `VideoPlayerControl` and `FileExplorerPanel`.
- Added `QueueSave_AfterDispose_NoOp` tests for both `SettingsService` and `StateService` to verify post-dispose debounce-queue guards.

### vido-113
- Replaced `VideoLoadedEvent` local empty metadata sentinel with shared `VideoMetadata.Empty` fallback in `Vido.Core`.
- Added test assertions verifying default/null-init metadata returns the shared singleton instance.

### vido-114
- Refactored `EventBus` to copy-on-write immutable delegate arrays (`ConcurrentDictionary<Type, Delegate[]>`).
- Removed publish-path `ToArray()` snapshot allocation and publish lock; publish now iterates immutable snapshots.
- Added EventBus tests for duplicate-handler unsubscribe semantics and publish-path allocation checks.

### vido-115
- Optimized `VideoPlayerViewModel.OnEnginePositionChanged` to update `PositionText` only when whole-second display changes.
- Added second-cache reset/sync points in load, seek, restore, and stop flows.
- Added tests validating same-second text update suppression and cache reset behavior on stop.

### vido-116
- Reworked `VideoPlayerControl` frame rendering to atomic latest-frame swap (`Interlocked.Exchange`) with stale-frame disposal.
- Added single queued render-pass flow to prevent dispatcher frame backlog under UI pressure.
- Added pending-frame disposal on media unload to avoid pooled-buffer leaks.

### vido-117
- `FrameConverter.Convert` now reuses cached `byte*[]`/`int[]` source and destination arrays instead of allocating per frame.
- `AudioRenderer.SubmitSamples(float[])` now uses a persistent reusable byte buffer, removing per-call pool rent/return overhead.
- Added tests verifying persistent float-submit buffer reuse and growth behavior.

### vido-118
- Updated `FFmpegVideoEngine.WaitForPresentationTime` to hybrid wait mode: coarse `Thread.Sleep(1)` then sub-2ms `SpinWait` finish.
- Preserved seek-generation and cancellation abort checks in both wait phases.

### vido-119
- Added seek drag throttling in `VideoPlayerControl.OnSeekSliderMouseMove` using a `Stopwatch` timestamp guard (~30Hz max apply-seek rate).
- Ensured final seek precision on drag end by forcing `ApplySeek()` in `OnSeekSliderMouseUp` before `EndSeek()`.

### vido-120
- Refactored `LogService.Entries` to return a cached copy-on-write snapshot (`Volatile.Read`) instead of allocating `ToList().AsReadOnly()` per read.
- Updated `LogService` write paths (`Log`, `Clear`) to rebuild and publish snapshots.
- Added `LogService` tests for snapshot reference stability and mutation refresh behavior.

### vido-121
- Refactored `ContextMenuRegistry` to maintain per-target cached snapshots rebuilt on `Register` / `Unregister`.
- Replaced per-read LINQ filtering/sorting allocations in `GetEntries` with allocation-free snapshot lookup.
- Added `ContextMenuRegistry` tests for snapshot reference reuse and rebuild-on-mutation behavior.

### vido-122
- Refactored `ContributionRegistry` to maintain copy-on-write cached snapshots for all `Get*` contribution query methods and file icons.
- Rebuilt contribution snapshots on registration/unregister mutations, removing per-query `ToList()` and dictionary-copy allocations.
- Added snapshot-reference tests across sidebar, panel, status bar, toolbar, context menu, file handler, control bar, and file icon queries.

### vido-123
- Replaced loading spinner `DispatcherTimer` loops with `Storyboard` + `DoubleAnimation` in both `VideoPlayerControl` and `FileExplorerPanel`.
- Moved spinner rotation animation work to WPF composition pipeline while preserving start/stop behavior and rotation reset semantics.

### vido-124
- Added cached deterministic shutter WAV payload (`Lazy<byte[]>`) in `MainWindow` and extracted synthesis into `GenerateShutterWav()`.
- Updated screenshot sound playback to reuse cached bytes instead of regenerating PCM/WAV data on every screenshot.

### vido-125
- Reworked `PluginSettingsStore` to debounce writes with `System.Threading.Timer`, coalescing rapid `Set`/`Reset`/`ResetAll` updates.
- Added `Flush()` and `Dispose()` to persist pending changes reliably at shutdown/deactivation boundaries.
- Updated `PluginHost` deactivation/removal flows to flush/dispose plugin settings stores and added tests for debounce coalescing and flush/dispose persistence.

### vido-126
- Updated `OutputLogViewModel`, `FileExplorerViewModel`, and `PluginManagerViewModel` to use collection reassignment for batched UI updates.
- Replaced clear-and-add loops in filter/sort paths with single collection replacement assignments.
- Added tests verifying collection reference replacement behavior for output log filtering, explorer root sorting, and plugin manager filtering.

### vido-127
- Added cached command-id snapshot storage in `KeyboardShortcutService` and rebuilt it only on register/unregister mutations.
- Updated `GetAllCommandIds()` to return cached snapshots instead of allocating `ToList().AsReadOnly()` per call.
- Added tests verifying cached snapshot reference reuse.

### vido-128
- Replaced `Task.Run` + `Task.Delay` debounce pattern in `SettingsService` and `StateService` with reusable one-shot `System.Threading.Timer` instances.
- Updated queue-save flows to reuse existing timers, coalescing rapid calls without allocating per-call tasks.
- Added tests verifying debounce persistence of latest values and timer instance reuse for both services.

### vido-129
- Added static icon bitmap cache in `FileExplorerPanel` keyed by icon path for plugin file icons.
- Reused frozen `BitmapImage` instances across tree items to avoid repeated bitmap construction and decode work.

### vido-130
- Added `ILogService.IsEnabled(LogLevel)` contract support (default-enabled) and implemented it in `LogService`.
- Updated `FFmpegVideoEngine.ReportMetrics` to return early when debug logging is disabled, avoiding metrics string interpolation work.
- Added tests validating log-level enablement behavior and metrics guard logging suppression.

### vido-131
- Completed XML documentation updates for all newly added/modified public APIs introduced in tickets 127–130.
- Added/updated XML docs on `ILogService.IsEnabled`, `LogService.IsEnabled`, and new caching/debounce members touched in this ticket batch.

### Process / Agent Governance
- Added mandatory zero-warning rule for touched repositories (build + test warning-free) to all agent definitions.
- Added mandatory ticket strike-through updates in solution documents after completion.
- Added mandatory `CHANGELOG.md` update requirement for each touched repository per ticket.

### Breaking Changes
- Converted event contracts in `Vido.Core.Events` from `sealed class` to `readonly record struct`:
  - `PlaybackPositionChangedEvent`
  - `PlaybackStateChangedEvent`
  - `PlayFileRequestedEvent`
  - `VideoLoadedEvent`
  - `VideoUnloadedEvent`
- Changed `DropClassifier.ClassifyAll` return type from `List<(DropClassification, string)>` to `(DropClassification, string)[]`.
- Plugins/extensions consuming these contracts must be rebuilt against `Vido.Core` 0.13.0.

### Performance Improvements
- Replaced per-access allocations in `PluginPaths.DefaultPluginDirectory` with cached static path.
- Replaced mutable `HashSet<string>` statics with `FrozenSet<string>` in:
  - `FileNode.VideoExtensions`
  - `SettingContribution.ValidTypes`
  - `AppSettings.OfficialRegistryUrls`
- Cached `PropertyChangedEventArgs` instances for high-frequency INPC models:
  - `StatusBarItem`
  - `TabItemModel`
  - `BottomPanelTabItem`
  - `FileNode`
- Optimized `TimeFormatter` (`Format`, `FormatPadded`) to use `TimeSpan.TryFormat` with stack buffers.
- Optimized `KeyBinding` display string construction with `string.Create` (removed `List<string>` + `string.Join`).

### Model/Contract Improvements
- Converted `VideoMetadata` to `sealed record` and added safe defaults for struct event usage.
- Converted `UpdateCheckResult` to `sealed record` for value semantics and `with` support.

### Testing
- Added comprehensive test coverage for all five event types.
- Added/expanded tests for:
  - `PluginPaths`
  - `StatusBarItem`
  - `TabItemModel`
  - `BottomPanelTabItem`
  - `FileNode`
  - `TimeFormatter`
  - `UpdateCheckResult`
  - `DropClassifier`

### Migration
- Added migration guidance for plugin and consumer updates in `MIGRATION.md`.

## [0.10.0] - 2026-02-26

### vi-024
- **Portable distribution**: Added `build-release.ps1` PowerShell script that publishes a self-contained win-x64 application and packages it as a portable zip (~142 MB compressed, ~355 MB uncompressed)
- **MSI installer**: Created WiX 5 installer project (`installer/`) producing a per-user MSI (~109 MB) with Start Menu shortcut, optional Desktop shortcut, and optional video file associations (.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
- **Build script**: `build-release.ps1` automates the full release pipeline — clean, publish, zip, MSI build — with `-SkipInstaller`, `-Configuration`, and `-OutputDir` parameters
- **WiX installer features**: 3-feature installer with WixUI_FeatureTree dialog — core application (required), Desktop shortcut (optional), and file associations (optional)
- **Per-user install**: MSI installs to `%LocalAppData%\Vido` requiring no administrator privileges, with MajorUpgrade support for seamless version updates
- **BUILD.md**: Created comprehensive build documentation covering prerequisites, development/release builds, distribution details, project structure, versioning, and troubleshooting

### vi-025
- **Code quality audit**: Verified no dead code, no TODO/HACK/FIXME markers, no unused usings across entire solution
- **Swallowed catch blocks**: Added logging to all 7 empty catch blocks — PluginHost (3×), PluginInstaller (1×), FileExplorerPanel (1×), MainWindow (1×), App.xaml.cs (1×)
- **XML documentation**: Added XML doc comments to 49 public API members across IWindowService, AppWindowState, LogLevel, ILogService, IContributionRegistry, AppSettings, and AppState
- **Scrollbar theming**: Replaced AccentBrush (blue) scrollbar thumbs with proper grey ScrollbarThumbBrush matching VS Code Dark Modern; added hover opacity increase (0.5 → 0.8) on mouse-over
- **Theme resources**: Added DangerBrush, DangerHoverBrush, BadgeBackgroundBrush, SubtleBackgroundBrush, ScrollbarThumbBrush to Brushes.xaml with corresponding Colors.xaml entries
- **Hardcoded color cleanup**: Replaced 19+ hardcoded hex colors with theme resource references in PluginManagerPanel, PluginDetailPanel, StatusBarView, and AboutDialog
- **Window sizing**: Changed default window width from 2560 to 1280 to avoid overflowing 1080p displays on first launch
- **README.md**: Complete rewrite with features, requirements, build/run/test/publish instructions, keyboard shortcuts, architecture overview, plugin quick start, and configuration reference
- **PLUGIN_DEVELOPMENT.md**: Created comprehensive plugin development guide covering project setup, manifest schema, full API reference (IVidoPlugin, IPluginContext, IEventBus, IVideoEngine, IPluginSettingsStore), code examples for all extension points, plugin isolation/safety, registry format, and distribution workflow
- **CHANGELOG.md**: Added vi-025 entry summarizing all review and polish changes

### vi-023
- **Frame buffer pooling**: FrameData now implements IDisposable and uses `ArrayPool<byte>.Shared` instead of `new byte[]` per frame, eliminating ~8 MB/frame LOH allocations at 30+ fps
- **Hardware-accelerated decoding**: FFmpegVideoEngine tries D3D11VA, then DXVA2, with automatic software fallback. GPU decodes the video and frames are transferred to system memory for rendering
- **TreeView virtualization fix**: Fixed `CanContentScroll="False"` in FileExplorerTreeViewStyle that was defeating `VirtualizingStackPanel.IsVirtualizing` — file explorer with 1000+ files now only realizes visible items
- **Deferred plugin activation**: Plugin discovery/loading now runs via `Dispatcher.BeginInvoke(Background)` after the first render pass, improving time-to-visible window
- **ReadyToRun compilation**: Added `PublishReadyToRun` and explicit `TieredCompilation` to Vido.App.csproj for faster startup in published builds
- **Startup timing**: App.xaml.cs logs window-visible time, plugin activation time, and total startup time to the Output Log
- **Video load timing**: FFmpegVideoEngine logs media load duration and whether HW acceleration is active
- **Playback performance metrics**: Logs FPS, rendered/dropped frame counts, GC memory usage, and decode mode every 30 seconds during playback
- **Video surface scaling**: Changed BitmapScalingMode from HighQuality to LowQuality on the video Image control — imperceptible at 30+ fps but reduces GPU overhead during playback
- **OutputLogViewModel IDisposable**: Added Dispose() to unsubscribe from `ILogService.EntryAdded`, fixing potential memory leak pattern
- **FrameData Dispose on seek discard**: Frames dropped due to seek generation mismatch are now properly disposed (returning pooled buffers)

### vi-022
- Created AboutDialog showing app name, logo, version (0.1.0), .NET runtime version, and FFmpeg version
- Dialog styled to match VS Code Dark Modern theme (dark background, rounded border, accent OK button)
- Dialog is modal, centered on owner window, closes with OK button or Escape key
- Added FFmpegInitializer.VersionString property — captures FFmpeg version via `av_version_info()` after initialization
- Enabled Help > About Vido menu item — opens the About dialog
- Enabled Help > Check for Updates menu item — shows placeholder "You are running the latest version" message
- Added TitleBar.AboutRequested and TitleBar.CheckForUpdatesRequested events
- Set explicit app version (0.1.0) in Vido.App.csproj
- Added 1 new test for VersionString property (809 total passing)

### vi-021
- Implemented File > Open File menu item with video file filter dialog
- Added Ctrl+O keyboard shortcut for Open File
- Implemented command-line argument handling (file path → open and play, folder path → open in explorer)
- Command-line processing deferred to Loaded event for proper video engine initialization
- Created FileAssociationHelper (Windows registry-based file association for installer use)
- FileAssociationHelper supports Register, Unregister, and IsAssociated methods
- Added 9 new tests for FileAssociationHelper (808 total passing)

### vi-020
- Implemented the Settings panel as a tab-based page (opens from activity bar gear icon)
- Created SettingsViewModel with categorized app settings and plugin settings integration
- Created AppSettingsStore adapter: maps typed AppSettings properties to IPluginSettingsStore interface for reuse of SettingDisplayItem
- App settings organized into three categories:
  - **Playback**: Default Volume (number), Default Playback Speed (enum dropdown), Loop Playback (checkbox)
  - **File Explorer**: Show Hidden Files (checkbox)
  - **Plugins**: Custom Plugin Registry URL (text input)
- Plugin settings from active plugins appear as additional categories with puzzle piece icon
- Search filtering matches against setting title, description, and category name (case-insensitive)
- Settings save immediately on change via debounced persistence
- Settings tab is cached to preserve state across tab switches
- Extracted shared settings control styles (ComboBox, TextBox, CheckBox) from PluginDetailPanel into Themes/SettingsControlStyles.xaml
- Search bar has integrated magnifying glass icon and placeholder text
- Number inputs reject non-numeric character input
- 45 new unit tests: 25 for AppSettingsStore, 20 for SettingsViewModel
- Total tests: 779 passed, 0 failures

### vi-b-002
- Refactored plugin system infrastructure to align with PLUGIN_REQUIREMENTS.md
- Enhanced SettingContribution model: added `EnumValues` (List<string>), `Section` (optional string), `ForceOverride` (bool), and `ValidTypes` static set (boolean, string, number, enum)
- Added `Reset(key)` and `ResetAll()` to IPluginSettingsStore and PluginSettingsStore with SettingChanged notifications and persistence
- Implemented forceOverride logic in PluginHost.ActivatePlugin — settings with `forceOverride: true` have their developer default written on every activation
- Added input validation to all PluginContext registration methods (ArgumentNullException / ArgumentException for null/empty IDs, factories, handlers, extensions)
- Hardened PluginManifestLoader validation: settings type validation (must be boolean/string/number/enum), enum requires non-empty enumValues, settings id uniqueness, settings id vs contribution id collision, settings title required
- Created PluginIconConstants with documented size constants: SidebarIconSize (24), FileIconSize (16), ToolbarIconSize (16)
- Created PluginSafeInvoke utility: SafeCreateView wraps view factories with try-catch returning error placeholders, SafeInvoke wraps plugin actions with error logging
- Added PluginRegistryUrls list to AppSettings with OfficialRegistryUrl constant, file:// support for local dev, included in ResetToDefaults
- Added 36 new unit tests across PluginSettingsStoreTests (Reset, ResetAll), PluginContextTests (input validation), PluginManifestLoaderTests (settings validation), PluginSafeInvokeTests, PluginInfrastructureTests (AppSettings registry URLs, icon constants, SettingContribution model), ResetToDefaultsTests (PluginRegistryUrls)
- Total tests: 620 passed (4 pre-existing env-dependent failures), 0 warnings, 0 errors

### vi-017
- Implemented drag-and-drop support for video files, folders, and unsupported file types from Windows Explorer
- Video files dropped on the player area load and play immediately, opening the parent folder in the file explorer
- Video files dropped on the file explorer open the parent folder and play the video
- Folders dropped anywhere open in the file explorer sidebar
- Non-video files show a "File type not supported" warning notification that auto-hides after 3 seconds
- Visual feedback during drag-over: blue border (#007fd4) with semi-transparent background and hint text
- Player area shows "Drop video file to play"; file explorer shows "Drop to open folder"
- Main window acts as fallback handler for drops on title bar, status bar, or other chrome areas
- Created DropClassifier utility (Vido.Core) to centralize drag-drop file classification logic
- DropClassification enum: Folder, VideoFile, UnsupportedFile, Invalid
- DropClassifier.Classify() and ClassifyFirst() methods replace duplicated logic in all three drop handlers
- Added 28 unit tests: DropClassifierTests covering null/empty/whitespace, non-existent paths, directories, all 7 video extensions, case-insensitive matching, 6 non-video extensions, array handling
- Total tests: 463 (435 → 463), 0 warnings, 0 errors

### vi-016
- Implemented full state persistence — Vido now remembers all settings and state between sessions
- Added QueueSave() method to IStateService interface with 500ms debounce, matching SettingsService pattern
- StateService implements IDisposable, has thread-safe debounce with lock guard on CancellationTokenSource
- SettingsService debounce also hardened with lock guard for thread safety
- Added RecentFiles list to AppState (capped at 10) with AddRecentFile() method (deduplicates case-insensitive, inserts at front)
- VideoPlayerViewModel now injects ISettingsService + IStateService: restores volume/mute/loop from settings; saves volume/mute/loop on change; tracks last video path/position in state; saves position every 5 seconds of playback; adds to recent files on video load
- Added RestoreLastVideoAsync() — reloads last played video paused at saved position on startup
- MainWindowViewModel now injects ISettingsService: restores bottom/right panel visibility and status bar from settings; saves on toggle
- ActivityBarViewModel now injects ISettingsService: restores sidebar visibility from settings; saves on change; added SetActivePanel() for non-toggling state restoration
- FileExplorerViewModel now injects ISettingsService: restores ShowHiddenFiles from settings; saves on toggle
- FileExplorerViewModel now calls QueueSave() after folder open/close, hide/unhide file mutations
- MainWindow.xaml.cs RestoreLayoutState() restores panel dimensions, sidebar width, active sidebar panel, and last video on startup
- MainWindow.xaml.cs SaveWindowState() now also persists sidebar width, panel heights, and sidebar visibility to settings
- Sidebar width uses persisted value from settings instead of hardcoded 300px
- Constructor property initialization uses backing fields to avoid wasteful save triggers during startup
- Tests: 13 new (AppState.AddRecentFile, panel/sidebar/status bar persistence, settings/state round-trips) — 434 total, all passing

#### vi-016 Bug Fixes
- Fixed gray background on video restore: RestoreLastVideoAsync now uses Play→Seek→await SeekCompleted→Pause to ensure a frame renders before pausing (FFmpegVideoEngine's decode thread must be running for Seek to take effect)
- Fixed bottom/right panel collapsed state not persisting: added BottomPanelCollapsed and RightPanelCollapsed to AppSettings, saved on change, restored on startup
- Fixed sidebar panel reverting to Explorer on restart: RestoreLayoutState now calls OnPanelChanged after SetActivePanel to refresh sidebar content
- Fixed recent files not shown in UI: File > Recent Files submenu now dynamically populates from AppState on open, clicking a recent file loads and plays it
- Added "Show Hidden Files" toggle to View menu with checkmark state, synced with existing context menu checkmark in file explorer

#### vi-016 Enhancements
- View menu "Toggle Sidebar" and "Toggle Status Bar" now dynamically show "Show/Hide" text like Right/Bottom Panel menus
- Added Playback Speed button to transport controls (visible in both normal and fullscreen modes) with 0.25x–4x presets, persisted in settings
- Playback Speed submenu in Playback menu now functional with speed presets and active checkmark
- IVideoEngine.SpeedRatio property added; FFmpegVideoEngine multiplies playback clock by speed ratio with smooth clock offset transitions on speed change
- Added keyboard shortcuts: Ctrl+K (Close Folder), Ctrl+Shift+R (Rescan Folder)
- Removed Zoom In/Out placeholder menu items (Ctrl+=, Ctrl+-)
- Added "Clear Watch History" button at the bottom of File > Recent Files submenu with separator

#### vi-016 Code Audit
- Fixed fullscreen transitions incorrectly saving transient panel state: added SuppressSettingsSave guard on MainWindowViewModel, used during Enter/ExitFullscreen to prevent reactive save handlers from persisting hidden-panel state
- Fixed _lastSavedPositionSeconds not reset when loading new video: position save debounce now starts fresh per video
- Removed dead ConfirmOnExit property from AppSettings (never read or exposed in UI)
- Removed redundant SidebarVisible save in SaveWindowState() (already handled reactively by ActivityBarViewModel)
- Made SettingsService testable with custom directory constructor (test isolation)

### vi-015
- Implemented fullscreen mode toggle via F11, F key, or double-click on video area
- EnterFullscreen saves all pre-fullscreen state (window geometry, sidebar, bottom/right panel, status bar visibility) and hides all UI chrome (title bar, activity bar, sidebar, tab strip, status bar, bottom panel, right panel)
- ExitFullscreen (Escape, F11, F, double-click) restores all UI chrome, panel states, and window geometry exactly to pre-fullscreen state
- Restructured VideoPlayerControl from 2-row Grid to overlay layout — transport controls (seek bar + buttons) now overlay the video at bottom, enabling both normal-mode docked appearance and fullscreen gradient-overlay mode
- Added EnterFullscreenOverlay/ExitFullscreenOverlay methods to VideoPlayerControl — switches between solid editor background (normal) and semi-transparent gradient (fullscreen: transparent→black fade)
- Added double-click handler on VideoSurface (FullscreenToggleRequested event) — fires on double-click only, no interference with single-click
- Added fullscreen auto-hide: DispatcherTimer (3s inactivity) fades controls out (200ms), mouse movement fades them back in; mouse cursor hidden via Mouse.OverrideCursor when controls are hidden
- WindowChrome CaptionHeight set to 0 and ResizeBorderThickness to 0 during fullscreen (no drag/resize), restored to 30/6 on exit
- Registered 3 new keyboard shortcuts: F11 (toggle fullscreen), F (toggle fullscreen), Escape (exit fullscreen only)
- Enabled Fullscreen menu item in TitleBarView View menu — added FullscreenRequested event and OnFullscreenClick handler
- SaveWindowState correctly persists pre-fullscreen geometry when app closed in fullscreen mode
- OnWindowStateChanged skips normal state sync during fullscreen to avoid interference
- Fixed tick handler accumulation bug — DispatcherTimer created once via null check, Tick handler attached only on first creation
- Fixed status bar restoration — hidden via IsStatusBarVisible VM property (not direct Visibility) so PropertyChanged fires correctly on restore
- Added xmldoc to OutputTabId constant, updated MainWindowViewModelTests class summary
- Tests: 3 new (IsFullscreen default, set, PropertyChanged) — 421 total, all passing

### vi-014
- Created KeyBinding model in Vido.Core/Keyboard — sealed value-equality class (Key, Ctrl, Shift, Alt); case-insensitive key comparison; DisplayString property (e.g. "Ctrl+Shift+O"); IEquatable\<KeyBinding\>, GetHashCode, ToString; zero external dependencies
- Created IKeyboardShortcutService interface in Vido.Core/Keyboard — Register (conflict detection), Unregister, TryExecute, FindBinding (by commandId), GetAllCommandIds, GetCommandId (by KeyBinding)
- Created KeyboardShortcutService in Vido.Services/Keyboard — dictionary-based registry with bidirectional lookup (key→command, command→key); conflict detection with logging; re-binding a command frees the old key; overriding a key with a new command removes old command's binding
- Wired PreviewKeyDown on MainWindow — routes keyboard input through IKeyboardShortcutService; suppresses shortcuts when TextBox/TextBoxBase is focused; handles Key.System for Alt combos; MapWpfKey translates WPF Key enum to string
- Registered 11 default keyboard shortcuts: Space (Play/Pause), S (Stop), M (Toggle Mute), Up/Down (Volume ±5%), PageUp/PageDown (Skip Previous/Next), Ctrl+B (Toggle Sidebar), Ctrl+J (Toggle Bottom Panel), Ctrl+H (Toggle Right Panel), Ctrl+Shift+O (Open Folder)
- Enabled TitleBar menu items: Toggle Sidebar (Ctrl+B), all Playback items (Play/Pause, Stop, Skip Forward/Backward, Loop) with click handlers and events; added Ctrl+H InputGestureText to Right Panel Show/Hide
- Added SafeFireAndForget helper for async shortcut handlers — prevents unobserved async void exceptions
- Registered IKeyboardShortcutService → KeyboardShortcutService as singleton in DI container
- Added tests: KeyBindingTests (20) — equality (7), GetHashCode (3), DisplayString (5), ToString (1), constructor (3), dictionary key behavior (2); KeyboardShortcutServiceTests (28) — registration (9), execution (4), unregistration (4), lookup (6), case insensitivity (2), re-binding (1)
- Total test count: 418 (all passing)

### vi-013
- Created StatusBarAlignment enum (Left/Right) in Vido.Core — no external dependencies
- Created StatusBarItem model in Vido.Core — manual INotifyPropertyChanged implementation (Id, Alignment, Priority readonly; Text, Tooltip, IsVisible observable); no CommunityToolkit dependency per Vido.Core zero-NuGet-deps rule
- Created StatusBarViewModel — manages item registry with LeftItems/RightItems ObservableCollections; 4 built-in items: FileName (left, priority 0), Duration (right, priority 100), Resolution (right, priority 200), Codec (right, priority 300); subscribes to VideoPlayerViewModel.CurrentMetadata PropertyChanged for auto-update; FormatDuration (hh:mm:ss when ≥1hr, mm:ss otherwise); RegisterItem/UnregisterItem/FindItem with priority-ordered insertion; IDisposable with event unsubscription
- Updated StatusBarView.xaml — full layout with DockPanel, left/right ItemsControls, VS Code-styled blue background (#007acc), white text (#ffffff), 12px Segoe UI, 22px height, top 1px border separator, separator dots (3px ellipses, #99ffffff) between right-side items, DataTrigger to hide separator for first right item (Duration priority 100), BooleanToVisibilityConverter for item visibility
- Added IsStatusBarVisible property (default true) and ToggleStatusBar command to MainWindowViewModel
- Enabled "Toggle Status Bar" menu item in TitleBarView — connected Click handler to ToggleStatusBarRequested event
- Wired StatusBarViewModel in MainWindow constructor — SetupStatusBar sets DataContext, UpdateStatusBarVisibility toggles Visibility based on IsStatusBarVisible, PropertyChanged handler in SetupTabSystem
- Registered StatusBarViewModel as singleton in DI container
- Added tests: StatusBarViewModelTests (31) — initial state (4), metadata updates (7), metadata sync (1), duration formatting (4 theory cases), item registry (9), StatusBarItem INPC (3), dispose (2), short duration edge case (1); MainWindowViewModelTests (+3) — status bar visibility default, toggle, PropertyChanged
- Total test count: 371 (all passing)

### vi-012
- Created VideoDetailsViewModel — subscribes to VideoPlayerViewModel.CurrentMetadata PropertyChanged; exposes 12 bindable properties (HasMetadata, FileName, FilePath, FileSize, FormattedDuration, Resolution, VideoCodec, AudioCodec, FrameRate, Bitrate, ContainerFormat, AudioInfo); static formatting helpers for file size (B/KB/MB/GB), duration, bitrate (bps/Kbps/Mbps), audio info (codec + channels + sample rate); IDisposable
- Created VideoDetailsPanel.xaml — right panel content with three sections (VIDEO INFORMATION, VIDEO, AUDIO) separated by dividers; empty state "No video loaded" centered text; scrollable with themed scrollbar; label/value layout in grid columns
- Wired right panel — VideoDetailsPanel set as RightPanelContent in MainWindow; collapse/expand chevron, remembered width on toggle; View > Right Panel > Show/Hide and Video Info menu items in TitleBar
- Consolidated RightPanelCollapseButtonStyle — reduced from 54-line duplicate to 5-line BasedOn override in TabStyles.xaml
- Changed RightPanelContent from ContentControl to ContentPresenter for consistency
- Removed 3 unused x:Name attributes (TabContentArea, BottomPanelCollapseButton, RightPanelCollapseButton)
- Added tests: VideoDetailsViewModelTests (17) — initial state (1), metadata updates (5), file size formatting (1 theory ×6), duration formatting (1 theory ×4), bitrate formatting (1 theory ×6), audio info formatting (1 theory ×5 + 1), dispose (2), player integration (1), duration display (2)
- Total test count: 337 (all passing)

### vi-011
- Created OutputLogViewModel — observes ILogService, provides filtered ObservableCollection of LogEntryViewModel entries for UI display; supports level cycling (All → Info+ → Warn+ → Errors → All), auto-scroll toggle, and clear; uses SynchronizationContext for UI thread marshalling; loads existing entries on construction
- Created LogEntryViewModel — presentation wrapper for LogEntry with pre-formatted timestamp (HH:mm:ss.fff local time), level tag (DBG/INF/WRN/ERR), FormattedLine property including optional source
- Created OutputLogPanel.xaml — VS Code-style scrollable log view with monospace font (Cascadia Code/Consolas), color-coded entries (grey=Debug, white=Info, yellow=Warning, red=Error), toolbar with filter cycle button, auto-scroll toggle, and clear button; empty state text when no entries; virtualized ListBox for performance; hover/selection highlighting
- Created OutputLogPanel.xaml.cs — auto-scroll behavior via CollectionChanged subscription, deferred scroll at Loaded priority, managed Loaded/Unloaded lifecycle
- Replaced bottom panel stub in MainWindow.xaml — swapped placeholder text with ContentPresenter hosting OutputLogPanel
- Updated MainWindow.xaml.cs — added ILogService and OutputLogViewModel dependencies, SetupOutputLog wiring, startup/shutdown log messages, error logging for playback failures
- Added logging throughout the application:
  - FileExplorerViewModel: folder opened (with path), folder closed, folder rescanned
  - VideoPlayerViewModel: video loading, playing (with resolution/duration), paused, resumed, stopped
  - MainWindow: app started, app shutting down, playback error details
- Updated FileExplorerViewModel — added ILogService constructor parameter
- Updated VideoPlayerViewModel — added ILogService constructor parameter
- Registered OutputLogViewModel as singleton in DI container
- Updated existing tests — FileExplorerViewModelTests and VideoPlayerViewModelTests now pass ILogService mock
- Added tests: OutputLogViewModelTests (20) — covering initial state (3), existing entry loading (1), new entry via callback (2), clear (1), auto-scroll toggle (1), filter cycling (2), filter exclusion (1), SetFilter (2), LogEntryViewModel formatting (4), level tags theory (4)
- Total test count: 279 (all passing)

### vi-010
- Created TabItemModel — tab data model with Id, Title, IconGeometry, IsClosable, IsPinned properties
- Created MainWindowViewModel — manages tab system (OpenTab, CloseTab, ActivateTab, ReorderTab), bottom/right panel visibility toggles, OpenSettings command; Player tab is pinned leftmost and not closable
- Created TabWell.xaml — horizontal tab strip control with ItemsControl, click-to-activate, close button on hover (closable tabs only), drag-to-reorder support with pinned tab constraints, scroll arrows for overflow
- Created TabStyles.xaml — VS Code Dark Modern tab styles: TabItemBorderStyle (35px height, right border), TabCloseButtonStyle (×, appears on hover), TabScrollViewerStyle (hidden scrollbar with ◀▶ arrows), TabScrollButtonStyle, PanelTabStripStyle/PanelTabItemStyle (panel header tabs), HorizontalSplitterStyle (drag handle for bottom panel)
- Created SettingsPage.xaml — placeholder settings tab content with gear icon and "Settings will be available in a future update" text
- Reorganized MainWindow.xaml layout — editor area now contains TabWell + tab content (VideoPlayer visible when Player active, DynamicTabContent for other tabs); added bottom panel area with splitter and OUTPUT tab stub; added right panel area with splitter and VIDEO INFO tab stub
- Updated MainWindow.xaml.cs — added MainWindowViewModel integration, SetupTabSystem, UpdateTabContent (switches between Player/Settings/future tabs), UpdateBottomPanelVisibility/UpdateRightPanelVisibility (panel toggle with remembered dimensions)
- Updated ActivityBarView.xaml.cs — Settings gear icon now raises SettingsRequested event (opens as tab) instead of toggling sidebar panel
- Updated TitleBarView — enabled View > Toggle Bottom Panel and View > Toggle Right Panel menu items with click handlers and ToggleBottomPanelRequested/ToggleRightPanelRequested events
- Registered MainWindowViewModel as singleton in DI container
- Added tests: MainWindowViewModelTests (32) — covering constructor defaults (4), OpenTab (4), CloseTab (6), ActivateTab (2), ReorderTab (5), OpenSettings (3), panel toggles (2), FindTab (2), TabItemModel defaults (1), PropertyChanged notifications (3)
- Total test count: 259 (all passing)

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
