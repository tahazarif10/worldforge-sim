# First-time import of the 10 bundled CC0 GLB models; all activity on H:.
# Does not reset, overwrite or modify project.godot itself.
param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "V2 art import must run from H:\WorldForge-Sim (actual: $root)"
}

Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$editor = Join-Path $root 'Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $editor)) { throw "Godot executable missing: $editor" }
if (-not (Test-Path -LiteralPath $sdk)) { throw "Portable .NET SDK missing: $sdk" }
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $editor -Parent) '_sc_'))) {
    throw 'Portable Godot marker _sc_ missing. Refusing to create Godot cache on C:.'
}
$assets = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets\Art\KenneyNature') -Filter '*.glb' -File)
if ($assets.Count -ne 10) {
    throw "Expected 10 tracked CC0 nature GLBs; found $($assets.Count). Run git fetch/switch first."
}
$logs = Join-Path $root 'artifacts\visual-lab'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

Write-Host 'Importing actual Kenney CC0 glTF models into H:-portable Godot cache.'
& $editor --headless --editor --import --path $root --log-file (Join-Path $logs 'godot-nature-import.log')
if ($LASTEXITCODE -ne 0) {
    throw 'Headless Godot glTF import failed. Inspect H:\WorldForge-Sim\artifacts\visual-lab\godot-nature-import.log'
}

& $sdk run --project (Join-Path $root 'tests\NatureScatter.Smoke\NatureScatter.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Nature art scatter smoke tests failed' }

& $sdk run --project (Join-Path $root 'tests\EnvironmentArt.Smoke\EnvironmentArt.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Environment art / shoreline tests failed' }

& $sdk run --project (Join-Path $root 'tests\WaterAndStreaming.Smoke\WaterAndStreaming.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Water and chunk budget tests failed' }

& $sdk run --project (Join-Path $root 'tests\TerrainRelief.Smoke\TerrainRelief.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw '3D relief geometry and ray picking tests failed' }

& $sdk run --project (Join-Path $root 'tests\SharedHeightfield.Smoke\SharedHeightfield.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Continuous terrain border and ray picking tests failed' }

& $sdk build (Join-Path $root 'WorldForgeSim.csproj') -c Debug --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Godot C# game build failed' }

Write-Host 'GLB import, art scatter checks and game build succeeded.'
Write-Host 'F4 Eco/Balanced; F5 facet relief; F6 smooth / legacy terrain; F8 PNG; F9 frame intervals; F10 chunks. All artifacts stay H:.'
if (-not $NoLaunch) {
    & (Join-Path $PSScriptRoot 'run-visual-lab.ps1')
}
