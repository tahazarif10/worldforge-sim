# Opt-in V3 regional-world validation. All Windows tools, imported assets,
# cache and logs stay on H:. Never modifies local project.godot.
param([switch]$NoLaunch, [switch]$ExperimentalV3)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Regional-world verification requires H:\WorldForge-Sim."
}
Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { throw "H:-portable .NET missing: $sdk" }
& $sdk run --project (Join-Path $root 'tests\RegionalWorld.Smoke\RegionalWorld.Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Regional world persistence or 8192-size test failed' }
# Also run existing V2 art regression, Godot GLB import and C# compilation,
# without launching the original V2 scene.
& (Join-Path $PSScriptRoot 'verify-v2-art.ps1') -NoLaunch
if ($LASTEXITCODE -ne 0) { throw 'Original V2 art regression checks failed' }
Write-Host 'Regional data and V2 scene checks succeeded.'
if (-not $ExperimentalV3) {
    Write-Warning 'V3 experimental preview was rejected for visual quality and camera/streaming responsiveness.'
    Write-Host 'Opening the approved V2 3D scene instead; regional tests above still ran.'
    Write-Host 'For engineering-only V3 testing, pass -ExperimentalV3 explicitly.'
}
if (-not $NoLaunch) {
    & (Join-Path $PSScriptRoot 'run-regional-world.ps1') -ExperimentalV3:$ExperimentalV3
}
