# Opt-in V3 regional-world validation. All Windows tools, imported assets,
# cache and logs stay on H:. Never modifies local project.godot.
param([switch]$NoLaunch)
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
Write-Host 'Regional world + existing V2 checks succeeded.'
if (-not $NoLaunch) {
    & (Join-Path $PSScriptRoot 'run-regional-world.ps1')
}
