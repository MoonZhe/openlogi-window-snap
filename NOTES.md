# Context: OpenLogi custom "Snap Left/Middle/Right" action-ring shortcuts

**Status: WORKING (2026-09-11).** No cmd flash, labels + icons on the ring, multi-monitor aware.

## Goal
Three buttons on a Logitech MX Master 4's Action Ring (via **OpenLogi**, a local-first
Rust app for Logitech HID++ devices) that resize the active window on **Windows**:

| Slot          | Label       | Icon         | Window placement                          |
|---------------|-------------|--------------|-------------------------------------------|
| `BottomLeft`  | Snap Left   | `ArrowLeft`  | left 50% of the monitor's work area       |
| `Bottom`      | Snap Middle | `Layers`     | centered 50% (Windows' ¼ · ½ · ¼ layout)  |
| `BottomRight` | Snap Right  | `ArrowRight` | right 50%                                 |

Config: `C:\Users\MoonZhe\.config\openlogi\config.toml`
Device: `serial:2545zaz672c8` (MX Master 4)
Other slots: `Top = Cut`, `TopRight = Copy`, `Right = Paste`, `Left = Undo`, `TopLeft = Redo`.

## Final solution

A tiny **windowless native exe** built with `csc.exe` (ships with Windows .NET Framework 4.x —
no third-party software), launched by OpenLogi's `OpenApplication` action.

### Files in `C:\Users\MoonZhe\.config\openlogi\`
- `snap.cs` (in the repo; was `snap-third.cs`) — source. Uses `user32.dll`: walks the Z-order from the foreground window
  to the first real app window (skipping consoles, the OpenLogi overlay, tool windows,
  DWM-cloaked windows, desktop/taskbar), gets the work area of **the monitor that window is
  on** (`MonitorFromWindow` + `GetMonitorInfo`), `ShowWindow(SW_RESTORE)` then `SetWindowPos`.
  Compensates for Win10/11 invisible resize borders (`GetWindowRect` vs
  `DWMWA_EXTENDED_FRAME_BOUNDS`) so adjacent snapped windows have no gap between them.
  Declares per-monitor DPI awareness v2 at startup so it works on monitors with different
  scaling factors (e.g. laptop panel at 150% next to an external monitor at 100%).
- `snap-left.exe`, `snap-middle.exe`, `snap-right.exe` — three identical copies of the build.
  `OpenApplication` takes no arguments, so the exe infers its zone from its **own filename**
  (`snap-<zone>.exe`). An explicit first arg (`left|middle|right`) still overrides.

Rebuild (then re-copy to the three names):
```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:snap-third.exe snap-third.cs
```
`/target:winexe` = GUI subsystem → never shows a console window.

### `config.toml` slots
```toml
[devices."serial:2545zaz672c8".action_ring.default.slots.BottomLeft]
label = "Snap Left"
icon = "ArrowLeft"

[devices."serial:2545zaz672c8".action_ring.default.slots.BottomLeft.action.OpenApplication]
path = 'C:\Users\MoonZhe\.config\openlogi\snap-left.exe'
display_name = "Snap left"

[devices."serial:2545zaz672c8".action_ring.default.slots.Bottom]
label = "Snap Middle"
icon = "Layers"

[devices."serial:2545zaz672c8".action_ring.default.slots.Bottom.action.OpenApplication]
path = 'C:\Users\MoonZhe\.config\openlogi\snap-middle.exe'
display_name = "Snap middle"

[devices."serial:2545zaz672c8".action_ring.default.slots.BottomRight]
label = "Snap Right"
icon = "ArrowRight"

[devices."serial:2545zaz672c8".action_ring.default.slots.BottomRight.action.OpenApplication]
path = 'C:\Users\MoonZhe\.config\openlogi\snap-right.exe'
display_name = "Snap right"
```
OpenLogi must be relaunched after editing `config.toml`.

## Replicating on another PC
1. Copy `snap.cs` (in the repo; was `snap-third.cs`), `snap-left.exe`, `snap-middle.exe`, `snap-right.exe` to
   `%USERPROFILE%\.config\openlogi\` there (no rebuild needed; .NET Framework 4.x is on every
   Windows 10/11).
2. Paste the slot blocks above into that PC's `config.toml`, replacing the user path and the
   device serial (`selected_device` at the top of that file).
3. Relaunch OpenLogi. An unsigned exe may trigger a one-time SmartScreen prompt.

## OpenLogi facts learned (from its binaries / error messages, not docs)
- `RunShellCommand` on Windows runs through **`cmd.exe /c`** → always a brief console flash.
  Also, at exe start the *foreground window is that cmd console*, so a naive
  `GetForegroundWindow()` resizes the console, not the user's window (hence the Z-order walk).
- `OpenApplication` uses **`ShellExecuteW`** → no console. TOML shape is a struct
  (`ApplicationTargetWire`): `{ path = '...', display_name = "..." }`. A bare string errors.
  Easiest way to discover such formats: set the action in the GUI, then read `config.toml`.
- Ring slot fields: `action`, `icon`, `label`.
- Valid `icon` values: Pointer, Mouse, Copy, Paste, Cut, Search, Save, Keyboard, Applications,
  Grid, Layers, Monitor, Lock, Camera, Play, Volume, Gauge, Refresh, ArrowUp, ArrowDown,
  ArrowLeft, ArrowRight, Undo, Redo, SelectAll, MouseBack, MouseForward, NewTab, CloseTab,
  ReopenTab, NextTab, PreviousTab, Reload, PreviousDesktop, NextDesktop, PreviousTrack,
  NextTrack, VolumeDown, Mute, ScrollLeft, ScrollRight, Folder, File, Globe, Terminal,
  Settings, Star, Heart, Calendar, Bell, User, Palette, Book, Ban.
- `Workflow` action (`PressKey`/`Delay`/`TypeText`/`RunShellCommand` steps) exists but is
  not exposed in the GUI; `CustomShortcut` is a single chord only.
- OpenLogi rewrites `config.toml` on GUI changes and can leave stray empty table headers
  (e.g. a duplicate `[...slots.Right]`) → parse error "duplicate key". Check after GUI edits.

## Approaches ruled out (kept for history)
1. **Windows Snap Layouts via `Win+Z` → 9 → 2**: a `Win+E` test macro injected by OpenLogi
   did nothing, so OpenLogi's key injection can't drive Win-key shortcuts. (Note: this is an
   OpenLogi limitation, not an OS rule — AutoHotkey injects Win combos fine via `SendInput`.)
2. **PowerShell script + VBScript launcher (`wscript.exe`) to hide the console**: failed with
   WSH "Not enough memory resources" even though `vbscript.dll`/`WScript.Shell` were correctly
   registered and the file was clean ASCII — almost certainly corporate policy (AppLocker/WDAC
   or a Defender ASR rule) blocking `wscript.exe` on this Azure AD-joined machine. Abandoned
   rather than diagnosed; the native exe removed the need for WSH entirely.
3. Third-party tools (PowerToys FancyZones etc.) — user does not want them.
