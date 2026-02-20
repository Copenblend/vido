# Vido - Requirements Clarification Questions

The following questions address ambiguities, gaps, and decision points in the Vido requirements. Each question includes recommendations to help guide your decision. Once answered, these will inform a complete, unambiguous implementation plan.

---

## Q1: Technology Stack

**What technology stack should Vido be built with?**

This is the most critical architectural decision. It affects performance, extensibility, UI fidelity to VS Code, and plugin architecture.

| Option | Pros | Cons |
|--------|------|------|
| **A) Electron + TypeScript** (Recommended) | Closest to VS Code's actual stack. Can reuse VS Code's open-source UI components, icons, and theming directly. Massive ecosystem for plugins. Familiar to most developers. | Higher memory footprint than native. Requires careful optimization for "ultra-performant" goal. |
| **B) Tauri + TypeScript/Rust** | Much lighter than Electron (~10x smaller binary). Rust backend is extremely fast. Native webview keeps UI web-based so VS Code styling is achievable. | Smaller ecosystem. Plugin system in Rust is more complex. Can't directly reuse VS Code components as easily. Webview rendering differences across platforms. |
| **C) WPF / WinUI 3 (C#/.NET)** | Excellent native Windows performance. Smooth resizing/moving. | Windows-only. Cannot reuse VS Code's web-based components. Plugin system via .NET assemblies is less accessible to web developers. UI recreation would be entirely manual. |
| **D) Qt / C++** | Maximum performance. Cross-platform. | Slowest development. Cannot reuse any VS Code components. Higher complexity for plugin system. |

**Recommendation:** Option **A (Electron + TypeScript)** — it's VS Code's own stack, so achieving UI parity is most natural. Performance concerns can be mitigated with hardware-accelerated video rendering (offloading to native layer), lazy loading, and careful architecture. If Windows-only performance is paramount, Option C is a strong second choice.

**CHOICE** Option C - This will be a windows only application and must be ultra performant on windows. I expected UI recreation to be one of the first steps we take in this process.  

---

## Q2: Target Platform(s)

**Which operating systems must Vido support at launch?**

| Option | Notes |
|--------|-------|
| **A) Windows only** (Recommended for v1) | Fastest path to market. Simplifies testing. Can expand later. |
| **B) Windows + macOS** | Moderate additional effort for Electron/Tauri. Significant for WPF. |
| **C) Windows + macOS + Linux** | Full cross-platform. Adds testing complexity. |

**Recommendation:** Option **A** for initial release, with cross-platform as a future goal. The architecture should be platform-agnostic where possible so expansion is easy.

**CHOICE** Windows only. 

---

## Q3: Video Rendering Engine

**Which video decoding/rendering backend should be used?**

This directly impacts performance, codec support, and the "most performant player" goal.

| Option | Pros | Cons |
|--------|------|------|
| **A) FFmpeg (via ffmpeg.wasm or native bindings)** (Recommended) | Industry standard. Supports virtually every codec. Can be offloaded to native process for performance. Most flexible for plugin-based codec extensions. | Requires careful integration. Licensing considerations (LGPL/GPL depending on codecs). |
| **B) libVLC** | Battle-tested. Handles nearly everything out of the box. Simple integration. | Heavier dependency. Less granular control. Harder to extend via plugins. |
| **C) Native HTML5 `<video>` element** | Zero dependencies. Hardware-accelerated by Chromium (in Electron). Extremely simple. | Limited codec support (no AVI natively). Less control over rendering pipeline. |
| **D) mpv (libmpv)** | Extremely performant. Excellent codec support. Used by many power-user players. | Additional native dependency. Plugin extension for codecs is mpv-dependent. |

**Recommendation:** Option **A (FFmpeg via native bindings)** with a fallback to HTML5 `<video>` for natively supported formats (mp4/webm). This gives maximum codec coverage while leveraging hardware acceleration where possible. Alternatively, Option **D (mpv/libmpv)** is excellent if you want proven playback quality with less custom work.

**CHOICE** Option A unless there is a better option specifically for .net on windows only platforms. 

---

## Q4: Window Chrome & Title Bar

**Should Vido use a custom title bar like VS Code, or the native OS title bar?**

| Option | Notes |
|--------|-------|
| **A) Custom title bar** (Recommended) | Matches VS Code exactly. Menu bar integrated into title bar. More polished, professional look. |
| **B) Native OS title bar** | Simpler implementation. Feels more native. Menu bar is separate. |

**Recommendation:** Option **A** — VS Code uses a custom frameless window with a custom title bar. To achieve visual parity, Vido should do the same. This also allows the menu bar to be part of the title bar, matching VS Code's layout.

**CHOICE** Custom title bar, just like VS Codes, all styling should match VS code menus, highlights, colors (Dark Modern) everything. 

---

## Q5: Skip Forward/Backward Duration

**What should the default skip duration be, and should it be configurable?**

| Option | Notes |
|--------|-------|
| **A) 5 seconds** | Common in many players (YouTube, Netflix). |
| **B) 10 seconds** (Recommended) | Standard in most media players. Familiar to users. |
| **C) User-configurable with a default** | Most flexible. Adds a settings entry. |

**Recommendation:** Option **C** with a default of **10 seconds**. The setting should be exposed in the Settings tab. This is a trivial addition and gives users control.

**CHOICE** This skip is to skip to the next video (alphanumerically) in the open folder in the file explorer - this is NOT to skip through the current video. 

---

## Q6: Fullscreen Mode

**Should Vido support fullscreen video playback?**

| Option | Notes |
|--------|-------|
| **A) Yes, with F11 or double-click** (Recommended) | Expected standard feature for any video player. Hides all UI panels. |
| **B) No, not in v1** | Simplifies initial implementation but feels incomplete. |

**Recommendation:** Option **A** — fullscreen is a core expectation of any video player. It should hide all chrome (menus, panels, status bar) and show only the video with a fade-in overlay for controls on mouse movement.

**CHOICE** Option A

---

## Q7: Multiple Simultaneous Videos

**Can multiple videos be open in tabs simultaneously, or only one active video at a time?**

| Option | Notes |
|--------|-------|
| **A) Multiple tabs, only one plays at a time** (Recommended) | Tabs show video thumbnails/names. Switching tabs switches the active video. Reasonable memory usage. |
| **B) Multiple tabs, multiple can play simultaneously** | Power-user feature. Higher resource usage. Complex audio mixing. |
| **C) Single video only, no tabs for videos** | Simplest. Tabs used only for settings/other panels. |

**Recommendation:** Option **A** — allows users to have several videos "queued" in tabs (like browser tabs) but only one plays at a time. Switching tabs pauses the previous video. This balances usability with performance.

**CHOICE** One Video tab, there is THE video tab and that is it. It is always the left most tab, and cannot be closed (with an x) all other tabs can be closed. Tabs can be on top of the video player, but the video player is very restrictve in that regard. 

---

## Q8: Playlist / Queue Support

**Should Vido support playlists or sequential playback of multiple files?**

| Option | Notes |
|--------|-------|
| **A) Yes, as a built-in bottom or right panel** | Natural extension. Could be a plugin later, but useful baseline. |
| **B) No, playlists should be a plugin** (Recommended) | Keeps the base player minimal and clean. Demonstrates plugin system capability. |
| **C) Basic auto-play-next-file-in-folder** | Simple middle ground. No playlist UI, just sequential auto-play. |

**Recommendation:** Option **B** — this aligns with the "minimal UI by default" requirement and gives an excellent first plugin to build and test the plugin system with. Option **C** is a reasonable addition to the base player.

**CHOICE** B 

---

## Q9: Subtitle Support

**Should Vido support subtitles (SRT, ASS, VTT, embedded) in the base player?**

| Option | Notes |
|--------|-------|
| **A) Yes, basic SRT/VTT support in base** (Recommended) | Expected feature. SRT/VTT are simple to implement. |
| **B) Full subtitle support (SRT, ASS, embedded, styling)** | More complex. ASS requires a dedicated renderer. |
| **C) No, subtitles as a plugin** | Keeps base simple but feels incomplete for a video player. |

**Recommendation:** Option **A** — basic SRT and VTT support is expected in any modern video player and relatively simple to implement. Advanced formats (ASS, embedded subtitle tracks) can be added via plugin.

**CHOICE** C - we may add native support eventually, but for now, it will be a plugin later. 
---

## Q10: Drag and Drop Support

**Should Vido support drag-and-drop of video files?**

| Option | Notes |
|--------|-------|
| **A) Yes, drag files onto the window to open them** (Recommended) | Standard UX pattern. Drop onto player area to play, drop onto file explorer to add. |
| **B) No** | Unusual for a media player. Would feel like a missing feature. |

**Recommendation:** Option **A** — drag-and-drop is a fundamental UX expectation for media players.

**CHOICE** A
---

## Q11: Audio-Only File Support

**Should Vido support audio-only files (MP3, FLAC, WAV, etc.) in the base player?**

| Option | Notes |
|--------|-------|
| **A) Yes, with a simple visualizer or album art display** | Broadens the player's utility. Simple waveform visualizer is achievable. |
| **B) Yes, but just a static "Now Playing" screen** (Recommended) | Minimal effort. Shows filename, duration, and playback controls. |
| **C) No, audio-only files are out of scope** | Keeps focus on video. Audio support could be a plugin. |

**Recommendation:** Option **B** — if the video engine already supports audio codecs (which FFmpeg/mpv do), there's no reason to block audio files. A simple static screen with metadata is minimal effort.

**CHOICE** C
---

## Q12: Theme Support

**Should Vido support only Dark Modern, or multiple themes?**

| Option | Notes |
|--------|-------|
| **A) Dark Modern only for v1** (Recommended) | Matches requirements exactly. Simplifies development. |
| **B) Dark Modern + Light Modern** | Small additional effort if theming is CSS-based. |
| **C) Full theme system (like VS Code's marketplace themes)** | Powerful but significant effort. Better as a future enhancement. |

**Recommendation:** Option **A** — the requirements specify "identical to VS Code (Dark Modern)." Build the theming system to be extensible (CSS variables / design tokens) so themes can be added later via plugins, but ship only Dark Modern.

**CHOICE** A
---

## Q13: State Persistence

**Should Vido remember its state between sessions?**

This includes: window position/size, last opened folder, last played video position, panel layout, volume level.

| Option | Notes |
|--------|-------|
| **A) Full state persistence** (Recommended) | Professional feel. Remembers everything. |
| **B) Minimal (window position/size and volume only)** | Simple. Quick to implement. |
| **C) None** | Every launch is fresh. Uncommon for modern apps. |

**Recommendation:** Option **A** — an "ultra-professional" player should remember the user's workspace. At minimum: window geometry, open folder, panel layout, volume, and playback position of the last video.

**CHOICE** A
---

## Q14: Keyboard Shortcuts

**What keyboard shortcuts should be supported in the base player?**

| Option | Notes |
|--------|-------|
| **A) Minimal set (Space=play/pause, arrows=skip, F11=fullscreen, Esc=exit fullscreen)** | Quick to implement. |
| **B) Comprehensive set matching common players** (Recommended) | Space, arrows, M=mute, F=fullscreen, +/-=volume, Ctrl+O=open file, etc. |
| **C) Fully customizable shortcut system (like VS Code's keybindings.json)** | Most flexible but significant effort for v1. |

**Recommendation:** Option **B** with the architecture to support **C** later (via a keybinding registry that plugins can extend). Ship with sensible defaults matching common media player conventions.

**CHOICE** B

---

## Q15: Plugin Distribution & Discovery

**The requirements mention "maybe something like a JSON list on GitHub." How exactly should plugin discovery work?**

| Option | Notes |
|--------|-------|
| **A) Single JSON registry file on a GitHub repo** (Recommended) | Simplest. A public GitHub repo contains a `registry.json` listing all approved plugins with name, description, version, and download URL. Plugins are hosted as GitHub releases on their own repos. |
| **B) Decentralized — users paste plugin repo URLs directly** | No central registry needed. Less discoverable. |
| **C) Full marketplace server (like VS Code Marketplace)** | Most powerful. Way overkill for v1. |

**Recommendation:** Option **A** — create a `vido-plugin-registry` GitHub repo containing a `registry.json`. Each entry points to a plugin's GitHub repo. Plugins are distributed as `.zip` archives attached to GitHub releases. The plugin manager in Vido fetches this JSON, displays available plugins, and downloads/installs from the linked releases. Simple, free, and scalable enough for early adoption.

**CHOICE** A

---

## Q16: Plugin Sandboxing & Security

**Should plugins run in a sandboxed environment, or have full access?**

| Option | Notes |
|--------|-------|
| **A) Full access (like VS Code extensions)** (Recommended for v1) | Simpler architecture. Plugins can do anything the app can. Trust model similar to VS Code. |
| **B) Sandboxed with a permission system** | More secure. Significantly more complex. |
| **C) Sandboxed with user-granted permissions** | Best security. Most complex. |

**Recommendation:** Option **A** for v1 — VS Code extensions have essentially full access, and Vido's plugin ecosystem will be small initially. Architect the plugin API so that sandboxing *could* be added later, but don't implement it now.

**CHOICE** A
---

## Q17: File Explorer Scope

**What does the File Explorer browse? A single folder, or multiple root folders?**

| Option | Notes |
|--------|-------|
| **A) Single folder at a time (like VS Code's "Open Folder")** (Recommended) | Simpler. User opens a folder containing their media. Context menu supports "Open Folder" and "Close Folder." |
| **B) Multiple root folders (like VS Code's Workspaces)** | More powerful but more complex. |
| **C) Predefined media library locations (Videos folder, etc.)** | Less flexible. More like a media library than a file explorer. |

**Recommendation:** Option **A** — single folder, matching VS Code's primary "Open Folder" workflow. The context menu already specifies "open a folder" and "close the folder" (singular). Multi-root can be a future enhancement.

**CHOICE** A

---

## Q18: File Explorer — Non-Video Files

**Should non-video files be visible in the file explorer, or filtered out?**

| Option | Notes |
|--------|-------|
| **A) Show all files with generic icons for non-video** (Recommended) | Matches VS Code behavior. Requirements mention "every other file must be a generic icon." |
| **B) Show only video/audio files** | Cleaner for a media player but diverges from VS Code. |
| **C) Configurable filter** | Most flexible. User can toggle between all files and media-only. |

**Recommendation:** Option **A** — the requirements explicitly say "every other file must be a generic icon," implying all files are shown. Non-video files simply aren't playable — double-clicking them does nothing or shows a message.

**CHOICE** A - nothing should happen on clicking, and hovering or double clicking should show a tooltip telling the user its not supported - howerever - this should be extendable. For example in the case of a .funscript plugin, a developer might want to load the funscript on double click, or show something like "Load Funscript" in the context menu. Users should be able to have context menu actions on all files, with at minimum "Remove" which only removes it from the context of the application, it does not delete from disk. 

---

## Q19: Application Distribution / Installation

**How should Vido be distributed?**

| Option | Notes |
|--------|-------|
| **A) Portable executable (no installer)** | Simplest. Just unzip and run. Good for development/testing. |
| **B) Installer (e.g., NSIS, MSI, or electron-builder)** (Recommended) | Professional. Handles file associations, shortcuts, uninstall. |
| **C) Both portable and installer** | Best of both worlds. Slightly more build config. |

**Recommendation:** Option **C** — but for the implementation plan, we start with **A** (just build and run) and add installer packaging as a final step. The focus should be on functionality first.

**CHOICE** C - so long as it is not overly complex to do so. 

---

## Q20: Application Auto-Update

**Should Vido itself (not just plugins) support auto-updates?**

| Option | Notes |
|--------|-------|
| **A) Yes, using electron-updater or similar** | Professional. Standard for Electron apps. |
| **B) No, manual updates only for v1** (Recommended) | Simplifies v1. Auto-update can be added later. |
| **C) Notification only (tells user an update is available)** | Middle ground. No auto-download. |

**Recommendation:** Option **B** for v1 — app auto-update is complex and requires signing, hosting, etc. Focus on the plugin auto-update system first (which is explicitly required). App auto-update can follow.

**CHOICE** B
---

## Q21: Testing Strategy

**What level of automated testing should the implementation plan include?**

| Option | Notes |
|--------|-------|
| **A) Manual testing only for v1** | Fastest to ship. Higher risk of regressions. |
| **B) Unit tests for core logic (plugin system, state management)** (Recommended) | Good balance. Tests the critical extensibility infrastructure. |
| **C) Full test suite (unit + integration + E2E)** | Most robust. Significant time investment. |

**Recommendation:** Option **B** — unit test the plugin system, event bus, and state management. These are the foundational pieces that everything else depends on. E2E tests for the UI can come later.

**CHOICE** B - The AI developer should implement all tests during as part of every ticket, to include regression. They must take into account any changes that have been made as part of that ticket even if they drift from the original scope of the ticket. 

---

## Q22: The "TCode Plugin" — What Is It?

**You mention a "TCode plugin" will be added soon after. Can you describe what TCode does at a high level?**

This will help me design the plugin API to ensure it can accommodate TCode's needs. Even a brief description (e.g., "it adds a code editor panel," "it adds timestamped annotations," "it transcodes video") would help me architect the right extension points.

**Description** The TCode plugin is meant to add functionality related to sending TCode to automated stroker devices such as the OSR or SR6. I have implemented a video player that you can explore here c:\source\funsvp for more information. I will be creating it as a plugin for Vido once Vido is in V1. The plugin will need to be as full featured as my implementation in FunSVP. In the context of Vido, it would need to add a bottom tab for the funscript viewer, a sidemenu option for the device controls, and context menu options in addition to the TCode and COM port implementation. It will need to fit into the video player seemlessly. 

---

## Q23: Video File Associations

**Should Vido register itself as a handler for video file types in the OS?**

| Option | Notes |
|--------|-------|
| **A) Yes, optionally during install** (Recommended) | Standard for media players. User can choose which types. |
| **B) No** | Simpler. User must always open files from within Vido. |

**Recommendation:** Option **A** — but this only applies if we have an installer (Q19). For v1 development, this is low priority.

**CHOICE** A - Yes for the installer. 
---

## Q24: Status Bar Content

**What should the status bar display in the base player?**

The requirements say plugins should be able to "change what is displayed in the Status bar" but don't specify the default content.

| Option | Notes |
|--------|-------|
| **A) Minimal: current file name, resolution, duration, codec** (Recommended) | Useful info at a glance. Matches VS Code's informational status bar. |
| **B) Extended: + FPS, bitrate, file size, playback speed** | More technical. Useful for power users. |
| **C) Empty by default, populated by plugins** | Most minimal. Feels incomplete without plugins. |

**Recommendation:** Option **A** — show filename, video resolution, duration, and codec in the status bar. This gives useful at-a-glance info. The status bar API should allow plugins to add/remove/replace items.

**CHOICE** A - and the plugin support is very important

---

## Q25: Top Menu Structure

**What menu items should appear in the top menu bar?**

The requirements say plugins can add new top-level menu buttons (like VS Code's Chat button) but NOT add to the main menus (File/Edit/Help). What should the base menus contain?

**Recommended base structure:**

| Menu | Items |
|------|-------|
| **File** | Open File, Open Folder, Close Folder, Recent Files, Exit |
| **Edit** | (Reserved for future — Copy frame? Preferences/Settings shortcut?) |
| **View** | Toggle Sidebar, Toggle Status Bar, Toggle Fullscreen, Zoom In/Out |
| **Playback** | Play/Pause, Stop, Skip Forward, Skip Backward, Loop, Playback Speed |
| **Help** | About, Documentation link, Check for Updates |

Does this structure work, or would you like to modify it?

**CHOICE** I like the structure you have suggested. 

---

*Please answer each question with the option letter or your own preference. Any additional context you provide will be incorporated into the implementation plan.*
