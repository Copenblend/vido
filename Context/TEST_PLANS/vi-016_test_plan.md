# vi-016 State Persistence — Test Plan

## Automated Tests (13 new, 434 total)

### StatePersistenceTests — AppState.AddRecentFile (4)
- [x] AddRecentFile_AddsToFront
- [x] AddRecentFile_MovesDuplicateToFront
- [x] AddRecentFile_TrimsToMax
- [x] AddRecentFile_CaseInsensitiveDedupe

### StatePersistenceTests — MainWindowViewModel Persistence (4)
- [x] MainWindowVM_ToggleBottomPanel_SavesSettings
- [x] MainWindowVM_ToggleRightPanel_SavesSettings
- [x] MainWindowVM_ToggleStatusBar_SavesSettings
- [x] MainWindowVM_RestoresPanelState_FromSettings

### StatePersistenceTests — ActivityBarViewModel Persistence (3)
- [x] ActivityBarVM_RestoresSidebarVisibility
- [x] ActivityBarVM_SidebarToggle_SavesSettings
- [x] ActivityBarVM_SetActivePanel_DoesNotToggleVisibility

### StatePersistenceTests — Round-Trip Serialization (2)
- [x] StateService_SaveAndLoad_RoundTripsRecentFiles
- [x] SettingsService_SaveAndLoad_RoundTrips

## Manual Verification

### Window Geometry Persistence
- [ ] Close and reopen — window position (X, Y) restored
- [ ] Close and reopen — window size (W, H) restored
- [ ] Close maximized — reopens maximized (restore bounds preserved)
- [ ] Close in fullscreen — reopens at pre-fullscreen geometry

### Video Playback Settings
- [ ] Change volume → close → reopen → volume is restored
- [ ] Mute → close → reopen → muted state is restored
- [ ] Enable loop → close → reopen → loop is enabled
- [ ] Volume slider at 42% → settings.json shows `"volume": 0.42`

### Video State
- [ ] Play video → close → reopen → video is loaded (paused) at last position
- [ ] Play video to 2:30 → close → reopen → shows 2:30 in position text
- [ ] Delete last played video → reopen → no error (graceful skip)
- [ ] Recent files list shows last 10 opened videos in state.json

### Sidebar Persistence
- [ ] Hide sidebar → close → reopen → sidebar is hidden
- [ ] Show sidebar → close → reopen → sidebar is visible
- [ ] Resize sidebar to 400px → close → reopen → sidebar is 400px

### Panel Persistence
- [ ] Hide bottom panel → close → reopen → bottom panel hidden
- [ ] Show right panel → close → reopen → right panel visible
- [ ] Hide status bar → close → reopen → status bar hidden
- [ ] Resize bottom panel to 150px → close → reopen → 150px height

### File Explorer State
- [ ] Open folder → close → reopen → folder is restored
- [ ] Close folder → close app → reopen → no folder open
- [ ] Hide file → close → reopen → file still hidden
- [ ] Toggle ShowHiddenFiles → close → reopen → setting restored

### Debounce Behavior
- [ ] Rapidly change volume 10 times → only 1 disk write occurs (check file timestamps)
- [ ] No save occurs during 500ms after last change
- [ ] Save completes within ~600ms of last change

### Edge Cases
- [ ] First launch with no state.json/settings.json — uses defaults
- [ ] Corrupted state.json — reverts to defaults without crash
- [ ] Corrupted settings.json — reverts to defaults without crash
- [ ] Closing app during debounce timer — OnClosing saves synchronously
