# Verify the new V2 camera/region/foliage integration on the user H: machine.
# Never touches project.godot, C: or any Git working-tree changes.
param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing a project path outside H: ($root)"
}
Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { throw "Missing H:-based .NET SDK at $sdk" }
$tests = @(
    'tests\Camera3D.Smoke\Camera3D.Smoke.csproj',
    'tests\WorldRegion.Smoke\WorldRegion.Smoke.csproj',
    'tests\FrameTime.Smoke\FrameTime.Smoke.csproj',
    'tests\VisibleChunks.Smoke\VisibleChunks.Smoke.csproj'
)
foreach ($test in $tests) {
    Write-Host "Verifying $test"
    & $sdk run --project (Join-Path $root $test) -c Release
    if ($LASTEXITCODE -ne 0) { throw "Test FAILED: $test" }
}
& $sdk build (Join-Path $root 'WorldForgeSim.csproj') -c Debug --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "Game build failed" }
Write-Host 'Camera, region, frame-time tests and Godot C# build succeeded.'
if (-not $NoLaunch) {
    Write-Host 'Running experimental 3D scene. Test right-drag, wheel zoom over mountains, brush.'
    Write-Host 'Press F9 in the game to write frame time CSV to H:\WorldForge-Sim\artifacts\performance.'
    & (Join-Path $PSScriptRoot 'run-visual-lab.ps1')
}
