# install.ps1 — one-shot setup for openlogi-window-snap
#
#   git clone https://github.com/MoonZhe/openlogi-window-snap
#   cd openlogi-window-snap
#   powershell -ExecutionPolicy Bypass -File .\install.ps1
#
# What it does:
#   1. copies snap-left/middle/right.exe into %USERPROFILE%\.config\openlogi\
#   2. reads your device serial (selected_device) from config.toml
#   3. backs up config.toml, removes any existing BottomLeft/Bottom/BottomRight slot
#      blocks, and appends the three Snap slots
# Then relaunch OpenLogi.

$ErrorActionPreference = 'Stop'

$src       = $PSScriptRoot
$configDir = Join-Path $env:USERPROFILE '.config\openlogi'
$config    = Join-Path $configDir 'config.toml'

if (-not (Test-Path $config)) {
    Write-Error "OpenLogi config not found at $config. Install/launch OpenLogi once first."
}

# 1. copy exes
foreach ($z in 'left', 'middle', 'right') {
    Copy-Item (Join-Path $src "snap-$z.exe") (Join-Path $configDir "snap-$z.exe") -Force
}
Write-Host "Copied snap-left/middle/right.exe to $configDir"

# 2. device serial
$toml   = [IO.File]::ReadAllText($config)
$serial = [regex]::Match($toml, '(?m)^selected_device\s*=\s*"([^"]+)"').Groups[1].Value
if (-not $serial) { Write-Error "Could not find selected_device in $config" }
Write-Host "Device: $serial"

# 3. rewrite slots
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Copy-Item $config "$config.pre-snap-$stamp.bak"
Write-Host "Backed up config to config.toml.pre-snap-$stamp.bak"

$base = "devices.`"$serial`".action_ring.default.slots"
$slots = @{ left = 'BottomLeft'; middle = 'Bottom'; right = 'BottomRight' }
$icons = @{ left = 'ArrowLeft';  middle = 'Layers'; right = 'ArrowRight' }
$labels = @{ left = 'Snap Left'; middle = 'Snap Middle'; right = 'Snap Right' }

# drop every existing table whose header starts with one of the three slot paths
foreach ($slot in $slots.Values) {
    $pattern = '(?ms)^\[' + [regex]::Escape("$base.$slot") + '(\.[^\]]*)?\]\r?\n.*?(?=^\[|\z)'
    $toml = [regex]::Replace($toml, $pattern, '')
}

$blocks = foreach ($z in 'left', 'middle', 'right') {
    $slot = $slots[$z]
    @"

[$base.$slot]
label = "$($labels[$z])"
icon = "$($icons[$z])"

[$base.$slot.action.OpenApplication]
path = '$configDir\snap-$z.exe'
display_name = "Snap $z"
"@
}

$toml = $toml.TrimEnd() + "`n" + ($blocks -join "`n") + "`n"
[IO.File]::WriteAllText($config, $toml)

Write-Host ''
Write-Host 'Done. Relaunch OpenLogi to load the new ring slots.' -ForegroundColor Green
