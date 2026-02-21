# vi-015 Fullscreen Mode — Test Plan

## Automated Tests (3 new, 421 total)

### MainWindowViewModelTests — Fullscreen (3)
- [x] IsFullscreen_DefaultFalse
- [x] IsFullscreen_CanBeSet
- [x] IsFullscreen_RaisesPropertyChanged

## Manual Verification

### Enter Fullscreen
- [ ] F11 toggles fullscreen on/off
- [ ] F key toggles fullscreen on/off
- [ ] Double-click on video surface enters fullscreen
- [ ] Double-click on video surface in fullscreen exits fullscreen
- [ ] View > Fullscreen menu item enters fullscreen

### Fullscreen Appearance
- [ ] Video fills the entire screen (no borders, no gaps)
- [ ] Title bar is hidden
- [ ] Activity bar is hidden
- [ ] Sidebar is hidden
- [ ] Status bar is hidden
- [ ] Tab strip is hidden
- [ ] Bottom panel is hidden
- [ ] Right panel is hidden
- [ ] Transport controls overlay visible at bottom with gradient background

### Controls Auto-Hide
- [ ] After 3 seconds of no mouse movement, controls fade out (200ms animation)
- [ ] Mouse cursor hides when controls hide
- [ ] Moving mouse shows controls again with fade-in animation
- [ ] Moving mouse shows cursor again
- [ ] Auto-hide timer resets on each mouse movement

### Exit Fullscreen
- [ ] Escape exits fullscreen
- [ ] F11 exits fullscreen
- [ ] F exits fullscreen
- [ ] Double-click exits fullscreen
- [ ] Window returns to previous size and position
- [ ] Sidebar visibility restored to pre-fullscreen state
- [ ] Bottom panel visibility/collapse state restored
- [ ] Right panel visibility/collapse state restored
- [ ] Status bar visibility restored
- [ ] Tab strip visible again
- [ ] Title bar visible again
- [ ] Activity bar visible again

### Edge Cases
- [ ] Enter fullscreen from maximized window → exit returns to maximized
- [ ] Enter fullscreen from normal window → exit returns to same position/size
- [ ] Enter fullscreen → close app → reopen → window at pre-fullscreen geometry
- [ ] Multiple Enter/Exit cycles don't accumulate event handlers (no performance degradation)
- [ ] Fullscreen works on the current monitor (multi-monitor setup)
- [ ] Keyboard shortcuts don't fire when typing in text fields during fullscreen
- [ ] Controls overlay allows interaction (seek, volume, play/pause) without exiting fullscreen
