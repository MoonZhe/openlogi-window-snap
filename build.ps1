# build.ps1 — compile snap.cs and produce one snap-<zone>.exe per zone in .\bin\
# Uses csc.exe from the Windows-bundled .NET Framework 4.x; no SDK install needed.

$ErrorActionPreference = 'Stop'

$zones = @(
    'left', 'right', 'top', 'bottom', 'middle',
    'top-left', 'top-right', 'bottom-left', 'bottom-right',
    'left-third', 'middle-third', 'right-third', 'left-two-thirds', 'right-two-thirds',
    'center', 'maximize', 'minimize', 'restore', 'next-monitor', 'prev-monitor'
)

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { Write-Error 'csc.exe not found (.NET Framework 4.x is required).' }

$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$out = Join-Path $bin 'snap.exe'
& $csc /nologo /target:winexe /optimize /out:$out (Join-Path $PSScriptRoot 'snap.cs')
if ($LASTEXITCODE -ne 0) { Write-Error "csc failed with exit code $LASTEXITCODE" }

foreach ($z in $zones) { Copy-Item $out (Join-Path $bin "snap-$z.exe") -Force }
Remove-Item $out

Write-Host "Built $($zones.Count) exes into $bin" -ForegroundColor Green
