# Opt-in parity mode only: SAME V2 VisualLab3D scene and camera, but
# terrain tiles read from a lazy 64x64 WorldState-backed region cache.
# Never launch RegionalWorldLab3D here. All project tools and logs on H:.
param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing region parity outside H:\WorldForge-Sim."
}
Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { throw "H:-based .NET missing: $sdk" }
& $sdk run --project (Join-Path $root 'tests\TerrainBackendParity.Smoke\TerrainBackendParity.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Region-to-approved-V2 art parity smoke failed" }
# Import existing CC0 GLBs, run previous V2 tests, and compile C# on H:.
& (Join-Path $PSScriptRoot 'verify-v2-art.ps1') -NoLaunch
if ($LASTEXITCODE -ne 0) { throw "Approved V2 regression tests failed" }
Write-Host 'Verified parity for every V2 tile, coast shader mask, shared-corner geometry and citizen RNG.'
if ($NoLaunch) { return }
$editor = Get-ChildItem -Path (Join-Path $root 'Tools\Godot') -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if (-not $editor) { throw "Missing portable Godot Mono under H:" }
if (-not $editor.FullName.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Godot executable is outside H:."
}
if (-not (Test-Path (Join-Path $editor.DirectoryName '_sc_'))) {
    throw "Godot portable _sc_ marker missing; refusing C:-profile cache"
}
$folder = Join-Path $root 'artifacts\visual-lab'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$log = Join-Path $folder 'godot-v2-region-parity.log'
Write-Host 'Running APPROVED V2 scene with experimental REGION DATA only. Right-drag and water visuals must look identical.'
Write-Host "Engine log: $log"
& $editor.FullName --path $root --scene 'res://Scenes/VisualLab3D.tscn' --rendering-method gl_compatibility --log-file $log -- --terrain-backend=region-mirror
if ($LASTEXITCODE -ne 0) { throw "Godot V2 region parity scene failed; see H:-based log" }
