# vi-014 Keyboard Shortcuts System — Test Plan

## Automated Tests (48 total)

### KeyBindingTests (20 tests)

#### Equality (7)
- [x] Equals_SameKeyAndModifiers_ReturnsTrue
- [x] Equals_DifferentKey_ReturnsFalse
- [x] Equals_CaseInsensitive
- [x] Equals_SameKeyDifferentModifiers_ReturnsFalse
- [x] Equals_WithAllModifiers_ReturnsTrue
- [x] Equals_Null_ReturnsFalse
- [x] Equals_ObjectOverload_Works

#### GetHashCode (3)
- [x] GetHashCode_EqualBindings_SameHashCode
- [x] GetHashCode_CaseInsensitive_SameHashCode
- [x] GetHashCode_DifferentModifiers_DifferentHashCode

#### DisplayString (5)
- [x] DisplayString_KeyOnly
- [x] DisplayString_CtrlKey
- [x] DisplayString_CtrlShiftKey
- [x] DisplayString_AltKey
- [x] DisplayString_AllModifiers

#### ToString (1)
- [x] ToString_MatchesDisplayString

#### Constructor (3)
- [x] Constructor_NullKey_ThrowsArgumentNullException
- [x] Constructor_SetsProperties
- [x] Constructor_DefaultModifiersFalse

#### Dictionary Key Behavior (2)
- [x] CanBeUsedAsDictionaryKey
- [x] DictionaryKey_CaseInsensitive

### KeyboardShortcutServiceTests (28 tests)

#### Registration (9)
- [x] Register_NewBinding_ReturnsTrue
- [x] Register_SameCommandId_UpdatesBinding
- [x] Register_ConflictingKey_ReturnsFalse
- [x] Register_ConflictingKey_LogsWarning
- [x] Register_ConflictingKey_NewCommandTakesPrecedence
- [x] Register_WithModifiers_Works
- [x] Register_NullBinding_Throws
- [x] Register_NullCommandId_Throws
- [x] Register_NullHandler_Throws

#### Execution (4)
- [x] TryExecute_RegisteredBinding_ExecutesAndReturnsTrue
- [x] TryExecute_UnregisteredBinding_ReturnsFalse
- [x] TryExecute_WithModifiers_MatchesCorrectBinding
- [x] TryExecute_WrongModifiers_ReturnsFalse

#### Unregistration (4)
- [x] Unregister_ExistingCommand_ReturnsTrue
- [x] Unregister_NonexistentCommand_ReturnsFalse
- [x] Unregister_RemovedBindingNoLongerExecutes
- [x] Unregister_FreesKeyForReuse

#### Lookup (6)
- [x] FindBinding_RegisteredCommand_ReturnsBinding
- [x] FindBinding_UnregisteredCommand_ReturnsNull
- [x] GetCommandId_RegisteredBinding_ReturnsId
- [x] GetCommandId_UnregisteredBinding_ReturnsNull
- [x] GetAllCommandIds_ReturnsAllRegistered
- [x] GetAllCommandIds_ExcludesUnregistered

#### Case Insensitivity (2)
- [x] KeyComparison_IsCaseInsensitive
- [x] CommandId_IsCaseInsensitive

#### Re-binding (1)
- [x] SameCommand_NewKey_FreesOldKey

## Manual Verification

### Keyboard Shortcuts
- [ ] Space toggles play/pause when a video is loaded
- [ ] S stops playback
- [ ] M toggles mute
- [ ] Up arrow increases volume by 5%
- [ ] Down arrow decreases volume by 5%
- [ ] PageUp skips to previous file in folder
- [ ] PageDown skips to next file in folder
- [ ] Ctrl+B toggles sidebar visibility
- [ ] Ctrl+J toggles bottom panel visibility
- [ ] Ctrl+H toggles right panel visibility
- [ ] Ctrl+Shift+O opens folder dialog

### Text Input Suppression
- [ ] Shortcuts do not fire when typing in a text box (e.g., search)

### Menu Items
- [ ] Toggle Sidebar menu item works and shows Ctrl+B gesture
- [ ] Playback menu items (Play/Pause, Stop, Skip Forward, Skip Backward, Loop) all work
- [ ] Right Panel Show/Hide shows Ctrl+H gesture text

### Edge Cases
- [ ] Multiple rapid key presses don't cause issues
- [ ] Shortcuts work after opening and closing dialogs
