# openlogi-window-snap

Window snapping for the Logitech MX Master **Action Ring** via OpenLogi on Windows —
halves, quarters, thirds, maximize/minimize, move-to-other-monitor — with **no third-party
software**. Tiny native exes built with the C# compiler that ships inside Windows.

## Quick start

Requires OpenLogi already installed and launched once (so `config.toml` exists), plus
`git`/`gh`. In PowerShell:

```bash
gh repo clone MoonZhe/openlogi-window-snap
```

```bash
powershell -ExecutionPolicy Bypass -File .\openlogi-window-snap\install.ps1
```

Then **relaunch OpenLogi**. That gives you the default layout:

| Ring slot     | Zone     | Placement                                |
|---------------|----------|------------------------------------------|
| `BottomLeft`  | `left`   | left half                                |
| `Bottom`      | `middle` | centered half (Windows' ¼ · ½ · ¼ layout)|
| `BottomRight` | `right`  | right half                               |

### Pick your own layout

Pass a slot → zone map. Slots you don't mention are left exactly as they are:

```bash
powershell -ExecutionPolicy Bypass -File .\openlogi-window-snap\install.ps1 -Slots @{ BottomLeft='left'; Bottom='middle'; BottomRight='right'; Top='maximize'; Left='prev-monitor'; Right='next-monitor' }
```

Slots: `Top` `TopRight` `Right` `BottomRight` `Bottom` `BottomLeft` `Left` `TopLeft`

## Zones

| Zone                                  | Placement (of the monitor's work area)             |
|---------------------------------------|----------------------------------------------------|
| `left` `right`                        | left / right half                                  |
| `top` `bottom`                        | top / bottom half                                  |
| `middle`                              | centered half (¼ · ½ · ¼)                          |
| `top-left` `top-right` `bottom-left` `bottom-right` | quarters                             |
| `left-third` `middle-third` `right-third` | thirds                                         |
| `left-two-thirds` `right-two-thirds`  | two thirds, anchored left / right                  |
| `center`                              | floating, ~83% × 83%, centered                     |
| `maximize` `minimize` `restore`       | standard window states                             |
| `next-monitor` `prev-monitor`         | move to the next/previous screen (left→right order), keeping the same relative size and position |

Every zone is a separate `bin\snap-<zone>.exe` — same binary, it reads the zone from its
own filename because OpenLogi's `OpenApplication` action passes no arguments.
`snap.exe <zone>` also works from a shell.

## How it works

`snap.cs` compiles to a ~10 KB windowless exe (`csc.exe /target:winexe`, .NET Framework 4.x,
present on every Windows 10/11). It:

- finds the real foreground app window (skipping consoles, the OpenLogi overlay, tool
  windows, DWM-cloaked windows, desktop/taskbar),
- uses the work area of **the monitor that window is on**,
- is **per-monitor DPI aware** (mixed scaling works, e.g. laptop 150% + external 100%),
- compensates for Windows 10/11 invisible resize borders so adjacent zones tile with
  **no gap** — zones are defined by shared edge fractions, so `left`+`right` or three
  `*-third`s meet exactly.

OpenLogi launches it via `OpenApplication` (`ShellExecuteW`), so there is **no console
flash** — unlike `RunShellCommand`, which goes through `cmd.exe /c`.

## Manual setup

1. Copy the exes you want from `bin\` into `%USERPROFILE%\.config\openlogi\bin\`.
2. In `%USERPROFILE%\.config\openlogi\config.toml`, find your device serial
   (`selected_device = "serial:…"` near the top) and add a block per slot, replacing
   `serial:XXXX`, `<you>`, `<Slot>` and `<zone>`. If the slot already exists, **replace** its
   tables — OpenLogi rejects duplicate headers.

   ```toml
   [devices."serial:XXXX".action_ring.default.slots.<Slot>]
   label = "Snap Left"
   icon = "ArrowLeft"

   [devices."serial:XXXX".action_ring.default.slots.<Slot>.action.OpenApplication]
   path = 'C:\Users\<you>\.config\openlogi\bin\snap-<zone>.exe'
   display_name = "Snap <zone>"
   ```

   Valid `icon` names are listed in [NOTES.md](NOTES.md).
3. Relaunch OpenLogi. It only reads `config.toml` at startup; a "Configuration" error names
   the exact line.

The exes are unsigned, so SmartScreen may prompt once the first time a button is pressed.

## Building from source

```bash
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Compiles `snap.cs` once and stamps out every `bin\snap-<zone>.exe`. To add a zone, add a
row to the `Zones` table in `snap.cs` (edge fractions in twelfths) and to the list in
`build.ps1`.

## Files

| File           | Purpose                                                 |
|----------------|---------------------------------------------------------|
| `snap.cs`      | Source                                                  |
| `bin\snap-*.exe` | Prebuilt binaries, one per zone                       |
| `build.ps1`    | Compile + stamp out the per-zone exes                   |
| `install.ps1`  | One-shot installer (copies exes, edits `config.toml`)   |
| `NOTES.md`     | Working notes: OpenLogi internals, icon list, dead ends |
