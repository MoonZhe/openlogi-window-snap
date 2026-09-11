# openlogi-window-snap

Snap the active window to the **left half**, **centered half**, or **right half** of the
screen from the Logitech MX Master Action Ring, using OpenLogi on Windows — with no
third-party software.

| Ring slot     | Label       | Placement                                  |
|---------------|-------------|--------------------------------------------|
| `BottomLeft`  | Snap Left   | left 50% of the monitor's work area        |
| `Bottom`      | Snap Middle | centered 50% (Windows' ¼ · ½ · ¼ layout)   |
| `BottomRight` | Snap Right  | right 50%                                  |

## Quick start

Requires OpenLogi already installed and launched once (so `config.toml` exists), and
`git`/`gh`. Run in PowerShell:

```bash
gh repo clone MoonZhe/openlogi-window-snap
```

```bash
powershell -ExecutionPolicy Bypass -File .\openlogi-window-snap\install.ps1
```

Then **relaunch OpenLogi**. That's it.

`install.ps1` copies the three exes into `%USERPROFILE%\.config\openlogi\`, reads your
mouse's serial from `config.toml`, backs the config up (`config.toml.pre-snap-<timestamp>.bak`),
replaces whatever is in the `BottomLeft` / `Bottom` / `BottomRight` ring slots with the
Snap actions, and leaves every other slot and setting untouched.

Prefer to do it by hand? See [Manual setup](#manual-setup) below.

## How it works

`snap-third.cs` is a ~7 KB windowless native exe (built with `csc.exe`, which ships with
Windows' .NET Framework 4.x). It:

- finds the real foreground app window (skipping consoles, the OpenLogi overlay, tool
  windows, DWM-cloaked windows, desktop/taskbar),
- uses the work area of **the monitor that window is on** (multi-monitor safe),
- is **per-monitor DPI aware** (works with mixed scaling, e.g. laptop 150% + external 100%),
- compensates for Windows 10/11 invisible resize borders so adjacent snapped windows have
  **no gap** between them,
- infers its zone from its **own filename** (`snap-left.exe`, `snap-middle.exe`,
  `snap-right.exe`), because OpenLogi's `OpenApplication` action takes no arguments.

OpenLogi launches it via its `OpenApplication` action (`ShellExecuteW`), so there is
**no console flash** — unlike `RunShellCommand`, which goes through `cmd.exe /c`.

## Manual setup

### 1. Copy the exes

Put `snap-left.exe`, `snap-middle.exe`, `snap-right.exe` (and optionally `snap-third.cs`)
into your OpenLogi config folder:

```
%USERPROFILE%\.config\openlogi\
```

No build step needed. If you'd rather build from source yourself:

```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:snap-third.exe snap-third.cs
```

then copy `snap-third.exe` to the three `snap-<zone>.exe` names.

### 2. Edit `config.toml`

Open `%USERPROFILE%\.config\openlogi\config.toml`. Note two values you need:

- **your username** in the path (`C:\Users\<you>\...`)
- **your device serial** — the `selected_device` line near the top, e.g.
  `selected_device = "serial:2545zaz672c8"`

Paste the following, replacing `<you>` and `serial:XXXX`. If the slots already exist
(e.g. set to something else in the GUI), replace those blocks rather than adding duplicates —
OpenLogi errors on duplicate table headers.

```toml
[devices."serial:XXXX".action_ring.default.slots.BottomLeft]
label = "Snap Left"
icon = "ArrowLeft"

[devices."serial:XXXX".action_ring.default.slots.BottomLeft.action.OpenApplication]
path = 'C:\Users\<you>\.config\openlogi\snap-left.exe'
display_name = "Snap left"

[devices."serial:XXXX".action_ring.default.slots.Bottom]
label = "Snap Middle"
icon = "Layers"

[devices."serial:XXXX".action_ring.default.slots.Bottom.action.OpenApplication]
path = 'C:\Users\<you>\.config\openlogi\snap-middle.exe'
display_name = "Snap middle"

[devices."serial:XXXX".action_ring.default.slots.BottomRight]
label = "Snap Right"
icon = "ArrowRight"

[devices."serial:XXXX".action_ring.default.slots.BottomRight.action.OpenApplication]
path = 'C:\Users\<you>\.config\openlogi\snap-right.exe'
display_name = "Snap right"
```

### 3. Relaunch OpenLogi

OpenLogi only reads `config.toml` at startup. If it shows a "Configuration" parse error,
it tells you the exact line — usually a duplicate slot header or a typo in the serial.

The exes are unsigned, so SmartScreen may prompt once the first time a button is pressed.

## Customising the layout

The zone math lives in one `switch` in `snap-third.cs` (`half` / `quarter` of the work-area
width). Change it, rebuild, and re-copy to the three names. Valid `icon` values and other
OpenLogi internals learned along the way are listed in [NOTES.md](NOTES.md).

## Files

| File               | Purpose                                                    |
|--------------------|------------------------------------------------------------|
| `snap-third.cs`    | Source                                                     |
| `snap-left.exe`    | Build, snaps to left half                                  |
| `snap-middle.exe`  | Build, snaps to centered half                              |
| `snap-right.exe`   | Build, snaps to right half                                 |
| `install.ps1`      | One-shot installer (see Quick start)                       |
| `NOTES.md`         | Working notes: OpenLogi internals, dead ends, icon list    |
