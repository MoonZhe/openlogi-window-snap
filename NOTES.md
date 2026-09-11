# Context: OpenLogi window-snap action-ring shortcuts

**Status: WORKING (2026-09-11).** No cmd flash, labels + icons on the ring, multi-monitor
and mixed-DPI aware, gap-free tiling.

**Repo (source of truth): https://github.com/MoonZhe/openlogi-window-snap** (public)
Local clone: `C:\Users\MoonZhe\repos\openlogi-window-snap`

## Current ring layout on this PC

| Slot          | Label       | Icon         | Zone     | Placement                        |
|---------------|-------------|--------------|----------|----------------------------------|
| `BottomLeft`  | Snap Left   | `ArrowLeft`  | `left`   | left 50% of the work area        |
| `Bottom`      | Snap Middle | `Layers`     | `middle` | centered 50% (¼ · ½ · ¼)         |
| `BottomRight` | Snap Right  | `ArrowRight` | `right`  | right 50%                        |

Other slots: `Top = Cut`, `TopRight = Copy`, `Right = Paste`, `Left = Undo`, `TopLeft = Redo`.
Config: `C:\Users\MoonZhe\.config\openlogi\config.toml` — device `serial:2545zaz672c8`.

## Folder layout (`C:\Users\MoonZhe\.config\openlogi\`)
```
config.toml                 OpenLogi config (slots reference bin\snap-<zone>.exe)
openlogi-snap-context.md    this file
bin\snap-<zone>.exe         20 identical builds of snap.cs, one per zone (see below)
*.lock, overlay.claim, config.toml.backup.*, config.toml.pre-snap-*.bak   OpenLogi's own / installer backups
```
Only `left`, `middle`, `right` are wired to the ring today; the other 17 exes are ready to
be mapped whenever wanted.

## Available zones (all built, in `bin\`)
| Zone                                                 | Placement                                  |
|------------------------------------------------------|--------------------------------------------|
| `left` `right`                                       | left / right half                          |
| `top` `bottom`                                       | top / bottom half                          |
| `middle`                                             | centered half (¼ · ½ · ¼)                  |
| `top-left` `top-right` `bottom-left` `bottom-right`  | quarters                                   |
| `left-third` `middle-third` `right-third`            | thirds                                     |
| `left-two-thirds` `right-two-thirds`                 | two thirds, anchored left / right          |
| `center`                                             | floating ~83% × 83%, centered              |
| `maximize` `minimize` `restore`                      | window states                              |
| `next-monitor` `prev-monitor`                        | move to next/prev screen (left→right), same relative size + position |

## Changing the ring layout
From the repo clone — slots not mentioned are left untouched, config is backed up first:
```
powershell -ExecutionPolicy Bypass -File C:\Users\MoonZhe\repos\openlogi-window-snap\install.ps1 -Slots @{ BottomLeft='left'; Bottom='middle'; BottomRight='right'; Top='maximize' }
```
Then relaunch OpenLogi (it only reads `config.toml` at startup).

Manual form of one slot:
```toml
[devices."serial:2545zaz672c8".action_ring.default.slots.<Slot>]
label = "Snap Left"
icon = "ArrowLeft"

[devices."serial:2545zaz672c8".action_ring.default.slots.<Slot>.action.OpenApplication]
path = 'C:\Users\MoonZhe\.config\openlogi\bin\snap-<zone>.exe'
display_name = "Snap <zone>"
```

## How the exe works (`snap.cs` in the repo)
Windowless native exe built with `csc.exe /target:winexe` (.NET Framework 4.x, ships with
Windows — `build.ps1` compiles once and stamps out every `bin\snap-<zone>.exe`).
- Zone comes from the first arg, or from the exe's **own filename** when there are no args
  (OpenLogi's `OpenApplication` passes none).
- Walks the Z-order from the foreground window to the first real app window (skips consoles,
  the OpenLogi overlay, tool windows, DWM-cloaked windows, desktop/taskbar).
- Work area of **the monitor the window is on** (`MonitorFromWindow` + `GetMonitorInfo`).
- Declares per-monitor DPI awareness v2 → correct on mixed scaling (laptop 150% + ext 100%).
- Compensates for Win10/11 invisible resize borders (`GetWindowRect` vs
  `DWMWA_EXTENDED_FRAME_BOUNDS`); zones use shared edge fractions (twelfths) → no seams.
- `next-/prev-monitor` enumerate monitors left→right and rescale the window's rect relative
  to the destination work area.

## Replicating on another PC
```
gh repo clone MoonZhe/openlogi-window-snap
powershell -ExecutionPolicy Bypass -File .\openlogi-window-snap\install.ps1
```
Relaunch OpenLogi. The installer reads that PC's serial from its own `config.toml`, installs
into `%USERPROFILE%\.config\openlogi\bin\`, and writes the default 3-slot layout (or pass
`-Slots`). Unsigned exe → possible one-time SmartScreen prompt.

## OpenLogi facts learned (from its binaries / error messages, not docs)
- `RunShellCommand` on Windows runs through **`cmd.exe /c`** → always a brief console flash,
  and at exe start the *foreground window is that console* (hence the Z-order walk).
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
  (e.g. a duplicate `[...slots.Right]`) → parse error. Check after GUI edits.

## Approaches ruled out (kept for history)
1. **Windows Snap Layouts via `Win+Z` → 9 → 2**: a `Win+E` test macro injected by OpenLogi
   did nothing, so OpenLogi's key injection can't drive Win-key shortcuts. (An OpenLogi
   limitation, not an OS rule — AutoHotkey injects Win combos fine via `SendInput`.)
2. **PowerShell script + VBScript launcher (`wscript.exe`)** to hide the console: failed with
   WSH "Not enough memory resources" despite correct `vbscript.dll`/`WScript.Shell`
   registration and clean ASCII — almost certainly corporate policy (AppLocker/WDAC or a
   Defender ASR rule) blocking `wscript.exe` on this Azure AD-joined machine. The native exe
   removed the need for WSH entirely.
3. Third-party tools (PowerToys FancyZones etc.) — not wanted.
