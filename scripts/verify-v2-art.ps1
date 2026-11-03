# First-time import of the 10 bundled CC0 GLB models; all activity on H:.
# Does not reset, overwrite or modify project.godot itself.
param([switch]$NoLaunch, [Nullable[int]]$WorldSeed = $null, [switch]$ViewportStreaming, [switch]$MeshWorkers)
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
. (Join-Path $PSScriptRoot 'acceptance-bundle.ps1')
. (Join-Path $PSScriptRoot 'acceptance-preflight.ps1')
$session = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$logs = Join-Path $root ('artifacts\visual-lab\preflight-' + $session)
try { Invoke-AcceptancePreflight -Root $root -Executable $editor -Sdk $sdk -Directory $logs }
finally { Write-Host "Preflight logs: $logs" }

Write-Host 'F4 Eco/Balanced; F5 facet relief; F6 smooth / legacy terrain; F8 PNG; F9 frame intervals; F10 chunks. All artifacts stay H:.'
if (-not $NoLaunch) {
    if ($null -ne $WorldSeed) {
        & (Join-Path $PSScriptRoot 'run-visual-lab.ps1') -WorldSeed $WorldSeed -ViewportStreaming:$ViewportStreaming -MeshWorkers:$MeshWorkers
    } else {
        & (Join-Path $PSScriptRoot 'run-visual-lab.ps1') -ViewportStreaming:$ViewportStreaming -MeshWorkers:$MeshWorkers
    }
}
