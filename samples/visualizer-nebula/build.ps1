# Compiles nebula.hlsl into nebula.cso, a Direct2D pixel shader the player can run.
# Needs the Windows SDK (fxc.exe and d2d1effecthelpers.hlsli).
param([string]$Source = "$PSScriptRoot\nebula.hlsl", [string]$Out = "$PSScriptRoot\nebula.cso")
$ErrorActionPreference = 'Stop'
$kits = "${env:ProgramFiles(x86)}\Windows Kits\10"
$ver = Get-ChildItem "$kits\bin" -Directory | Where-Object Name -match '^10\.' | Sort-Object Name -Descending | Select-Object -First 1
$fxc = Join-Path $ver.FullName 'x64\fxc.exe'
$inc = Join-Path "$kits\Include\$($ver.Name)" 'um'
$ErrorActionPreference = 'Continue'  # fxc writes warnings to stderr
& $fxc $Source /nologo /T ps_4_0 /D D2D_FULL_SHADER /D D2D_ENTRY=main /E main /O3 /I $inc /Fo $Out
if ($LASTEXITCODE) { throw 'fxc failed' }
Write-Host "compiled $Out"
