# install.ps1 — one-shot setup for openlogi-window-snap
#
#   gh repo clone MoonZhe/openlogi-window-snap
#   powershell -ExecutionPolicy Bypass -File .\openlogi-window-snap\install.ps1
#
# Default ring layout (BottomLeft=left, Bottom=middle, BottomRight=right). To choose your own,
# pass a slot -> zone map; any slot you don't mention is left exactly as it is:
#
#   .\install.ps1 -Slots @{ BottomLeft='left'; Bottom='middle'; BottomRight='right'; Top='maximize'; Left='prev-monitor'; Right='next-monitor' }
#
# Slots: Top TopRight Right BottomRight Bottom BottomLeft Left TopLeft
# Zones: see README (left, right, top, bottom, middle, top-left, ..., maximize, minimize,
#        restore, next-monitor, prev-monitor)
#
# What it does:
#   1. copies every bin\snap-*.exe into %USERPROFILE%\.config\openlogi\bin\
#   2. reads your device serial (selected_device) from config.toml
#   3. backs up config.toml, replaces the chosen slots, leaves everything else untouched
# Then relaunch OpenLogi.

param(
    [hashtable] $Slots = @{ BottomLeft = 'left'; Bottom = 'middle'; BottomRight = 'right' }
)

$ErrorActionPreference = 'Stop'

$bin       = Join-Path $PSScriptRoot 'bin'
$configDir = Join-Path $env:USERPROFILE '.config\openlogi'
$exeDir    = Join-Path $configDir 'bin'
$config    = Join-Path $configDir 'config.toml'

if (-not (Test-Path $config)) { Write-Error "OpenLogi config not found at $config. Install/launch OpenLogi once first." }
if (-not (Test-Path (Join-Path $bin 'snap-left.exe'))) { Write-Error "bin\ is empty — run .\build.ps1 first (or pull the repo's prebuilt binaries)." }

$validSlots = 'Top','TopRight','Right','BottomRight','Bottom','BottomLeft','Left','TopLeft'
foreach ($k in $Slots.Keys) {
    if ($validSlots -notcontains $k) { Write-Error "Unknown slot '$k'. Valid: $($validSlots -join ', ')" }
    if (-not (Test-Path (Join-Path $bin "snap-$($Slots[$k]).exe"))) { Write-Error "Unknown zone '$($Slots[$k])' for slot $k (no bin\snap-$($Slots[$k]).exe)." }
}

# 1. copy exes
New-Item -ItemType Directory -Force $exeDir | Out-Null
Get-ChildItem $bin -Filter 'snap-*.exe' | Copy-Item -Destination $exeDir -Force
Write-Host "Copied $((Get-ChildItem $bin -Filter 'snap-*.exe').Count) snap-*.exe files to $exeDir"

# 2. device serial
$toml   = [IO.File]::ReadAllText($config)
$serial = [regex]::Match($toml, '(?m)^selected_device\s*=\s*"([^"]+)"').Groups[1].Value
if (-not $serial) { Write-Error "Could not find selected_device in $config" }
Write-Host "Device: $serial"

# 3. rewrite chosen slots
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Copy-Item $config "$config.pre-snap-$stamp.bak"
Write-Host "Backed up config to config.toml.pre-snap-$stamp.bak"

$icons = @{
    'left' = 'ArrowLeft'; 'right' = 'ArrowRight'; 'top' = 'ArrowUp'; 'bottom' = 'ArrowDown'; 'middle' = 'Layers'
    'top-left' = 'Grid'; 'top-right' = 'Grid'; 'bottom-left' = 'Grid'; 'bottom-right' = 'Grid'
    'left-third' = 'Applications'; 'middle-third' = 'Applications'; 'right-third' = 'Applications'
    'left-two-thirds' = 'Applications'; 'right-two-thirds' = 'Applications'
    'center' = 'Layers'; 'maximize' = 'Monitor'; 'minimize' = 'ArrowDown'; 'restore' = 'Refresh'
    'next-monitor' = 'Monitor'; 'prev-monitor' = 'Monitor'
}

$base = "devices.`"$serial`".action_ring.default.slots"
$blocks = foreach ($slot in $Slots.Keys) {
    $zone  = $Slots[$slot]
    $label = (Get-Culture).TextInfo.ToTitleCase($zone.Replace('-', ' '))

    # drop every existing table whose header starts with this slot's path
    $pattern = '(?ms)^\[' + [regex]::Escape("$base.$slot") + '(\.[^\]]*)?\]\r?\n.*?(?=^\[|\z)'
    $toml = [regex]::Replace($toml, $pattern, '')

    @"

[$base.$slot]
label = "$label"
icon = "$($icons[$zone])"

[$base.$slot.action.OpenApplication]
path = '$exeDir\snap-$zone.exe'
display_name = "Snap $zone"
"@
    Write-Host "  $slot -> $zone"
}

$toml = $toml.TrimEnd() + "`n" + ($blocks -join "`n") + "`n"
[IO.File]::WriteAllText($config, $toml)

Write-Host ''
Write-Host 'Done. Relaunch OpenLogi to load the new ring slots.' -ForegroundColor Green
