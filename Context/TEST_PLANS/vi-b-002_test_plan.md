# vi-b-002 Test Plan — Plugin System Refactor

## Overview
Validates that the vi-018 plugin infrastructure has been refactored to fully align with `PLUGIN_REQUIREMENTS.md`, covering setting model enhancements, settings store operations, input validation, manifest validation hardening, icon constants, plugin isolation utilities, and multi-registry infrastructure.

---

## 1. SettingContribution Model (`PluginInfrastructureTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `ValidTypes_ContainsAllFourTypes` | boolean, string, number, enum are in ValidTypes |
| 2 | `ValidTypes_CaseInsensitive` | "Boolean", "STRING" match (OrdinalIgnoreCase) |
| 3 | `SettingContribution_DefaultPropertyValues` | New instance has correct defaults: empty Id, "string" Type, null Default, empty Title/Description, empty EnumValues, null Section, false ForceOverride |
| 4 | `SettingContribution_Section_CanBeSet` | Section property is settable |
| 5 | `SettingContribution_ForceOverride_CanBeSet` | ForceOverride property is settable |
| 6 | `SettingContribution_EnumValues_CanBePopulated` | EnumValues list can hold multiple strings |

## 2. PluginSettingsStore – Reset / ResetAll (`PluginSettingsStoreTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `Reset_ExistingKey_RemovesAndReturnsTrue` | Removing an existing key returns true, Get returns default |
| 2 | `Reset_MissingKey_ReturnsFalse` | Removing a non-existent key returns false |
| 3 | `Reset_FiresSettingChangedForRemovedKey` | SettingChanged event fires with the removed key |
| 4 | `Reset_DoesNotFireSettingChangedForMissingKey` | SettingChanged does NOT fire for non-existent key |
| 5 | `Reset_PersistsRemoval` | Key removal survives store re-instantiation |
| 6 | `ResetAll_ClearsAllSettings` | All stored values are gone after ResetAll |
| 7 | `ResetAll_FiresSettingChangedForEachKey` | One SettingChanged per key |
| 8 | `ResetAll_PersistsEmptyStore` | Empty state survives re-instantiation |

## 3. PluginContext — Input Validation (`PluginContextTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `RegisterSidebarPanel_NullId_Throws` | ArgumentNullException on null contributionId |
| 2 | `RegisterSidebarPanel_EmptyId_Throws` | ArgumentException on empty contributionId |
| 3 | `RegisterSidebarPanel_NullFactory_Throws` | ArgumentNullException on null viewFactory |
| 4 | `RegisterBottomPanel_NullFactory_Throws` | ArgumentNullException |
| 5 | `RegisterRightPanel_EmptyId_Throws` | ArgumentException on whitespace |
| 6 | `RegisterStatusBarItem_NullFactory_Throws` | ArgumentNullException |
| 7 | `RegisterToolbarButtonHandler_NullHandler_Throws` | ArgumentNullException |
| 8 | `RegisterContextMenuHandler_NullHandler_Throws` | ArgumentNullException |
| 9 | `RegisterFileHandler_EmptyExtensions_Throws` | ArgumentException on empty array |
| 10 | `RegisterFileHandler_NullHandler_Throws` | ArgumentNullException |
| 11 | `RegisterFileIcons_EmptyDict_Throws` | ArgumentException on empty dictionary |
| 12 | `RegisterKeyBinding_NullBinding_Throws` | ArgumentNullException |
| 13 | `RegisterKeyBinding_NullHandler_Throws` | ArgumentNullException |

## 4. Manifest Validation — Settings (`PluginManifestLoaderTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `Validate_SettingWithValidType_NoErrors` | All four types pass validation |
| 2 | `Validate_SettingInvalidType_ReturnsError` | Invalid type produces error |
| 3 | `Validate_EnumWithoutEnumValues_ReturnsError` | Enum type + empty enumValues produces error |
| 4 | `Validate_SettingMissingId_ReturnsError` | Empty setting id produces error |
| 5 | `Validate_SettingMissingTitle_ReturnsError` | Empty setting title produces error |
| 6 | `Validate_DuplicateSettingIds_ReturnsError` | Duplicate setting ids produce error |
| 7 | `Validate_SettingIdConflictsWithContribution_ReturnsError` | Setting id colliding with contribution id produces error |
| 8 | `Load_SettingsInManifest_ParsesCorrectly` | Full manifest JSON with settings deserializes correctly, including section, forceOverride, enumValues |

## 5. Plugin Isolation — PluginSafeInvoke (`PluginSafeInvokeTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `SafeCreateView_SuccessfulFactory_ReturnsView` | Normal factory returns the view |
| 2 | `SafeCreateView_ThrowingFactory_ReturnsFallback` | Throwing factory returns "[Plugin Error: …]" |
| 3 | `SafeCreateView_ThrowingFactory_LogsError` | Error is logged with pluginId and contributionId |
| 4 | `SafeInvoke_SuccessfulAction_Executes` | Normal action runs |
| 5 | `SafeInvoke_ThrowingAction_SwallowsAndLogs` | Exception is swallowed and logged |

## 6. Multi-Registry Infrastructure (`PluginInfrastructureTests`, `ResetToDefaultsTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `PluginRegistryUrls_DefaultContainsOfficialUrl` | Fresh AppSettings has the official URL |
| 2 | `PluginRegistryUrls_CanAddCustomUrl` | Custom URL can be appended |
| 3 | `PluginRegistryUrls_SupportsFileProtocol` | file:// URLs work |
| 4 | `OfficialRegistryUrl_IsNotEmpty` | Constant is non-empty HTTPS |
| 5 | `ResetToDefaults_RestoresPluginRegistryUrls` | ResetToDefaults restores to [OfficialRegistryUrl] |

## 7. Icon Constants (`PluginInfrastructureTests`)

| # | Test | Validates |
|---|------|-----------|
| 1 | `SidebarIconSize_Is24` | 24×24 |
| 2 | `FileIconSize_Is16` | 16×16 |
| 3 | `ToolbarIconSize_Is16` | 16×16 |

---

## Summary

| Suite | New Tests | Status |
|-------|-----------|--------|
| PluginSettingsStoreTests | 8 | ✅ Pass |
| PluginContextTests | 13 | ✅ Pass |
| PluginManifestLoaderTests | 8 | ✅ Pass |
| PluginSafeInvokeTests | 5 | ✅ Pass |
| PluginInfrastructureTests | 12 | ✅ Pass |
| ResetToDefaultsTests | ~2 updated | ✅ Pass |
| **Total new tests** | **36** | **✅ All pass** |
| **Full suite** | **620 passed** | **4 pre-existing env failures** |
