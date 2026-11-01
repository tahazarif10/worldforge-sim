param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Directory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# The missing-UID fixture mutates only this disposable CI checkout.
if ($env:GITHUB_ACTIONS -cne 'true') { throw 'Upgrade fixture is CI-only; do not run on the user checkout.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$directoryFull = [IO.Path]::GetFullPath($Directory)
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
$initial = Get-Content -LiteralPath (Join-Path $directoryFull 'worktree-before-import.json') -Raw | ConvertFrom-Json
$existing = Get-AcceptanceWorktreeState -Root $root
Assert-AcceptanceResourcesReady -State $existing
Export-AcceptanceSidecars -Root $root -State $existing -Directory (Join-Path $directoryFull 'upgrade-fixture/original-sidecars')

# Reproduce the user's exact three missing companions after the initial import/build.
$missingPaths = @('Scripts/Instrumentation/AcceptanceOptions.cs.uid',
    'Scripts/Instrumentation/FrameCapture.cs.uid','Scripts/VisualLab/VisualLab3D.Acceptance.cs.uid')
$trackedText = Invoke-AcceptanceGit -Root $root -Arguments @('ls-files','--cached','-z')
$tracked = $trackedText.Split([char[]]@([char]0), [StringSplitOptions]::RemoveEmptyEntries)
foreach ($path in $missingPaths) {
    if ($tracked -ccontains $path) { throw "Upgrade fixture must not remove a tracked companion: $path" }
    $destination = Join-Path $directoryFull ('upgrade-fixture/moved/' + $path)
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    Move-Item -LiteralPath (Join-Path $root $path) -Destination $destination
}
$beforeRuntime = Get-AcceptanceWorktreeState -Root $root
if ($beforeRuntime.missing_godot_sidecars.Count -ne 3) { throw 'Upgrade fixture did not reproduce the three missing UIDs.' }
foreach ($path in $missingPaths) {
    if ($beforeRuntime.missing_godot_sidecars -cnotcontains $path) { throw "Expected missing UID: $path" }
}
$beforeRuntime | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $directoryFull 'worktree-before-runtime.json')
Initialize-AcceptanceRuntime -Executable $Executable -Root $root -Directory (Join-Path $directoryFull 'preparation') -WorldSeed 1315511732
$prepared = Get-AcceptanceWorktreeState -Root $root
Assert-AcceptanceResourcesReady -State $prepared
Assert-AcceptanceWorktreeStable -Baseline $initial -Current $prepared -AllowSidecarRefresh
Assert-AcceptanceWorktreeStable -Baseline $beforeRuntime -Current $prepared -AllowSidecarRefresh
if (@($prepared.godot_sidecars | Where-Object path -CLike '*.glb.import').Count -ne 10) {
    throw 'Expected actual import metadata for all 10 tracked GLBs.'
}
$prepared | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $directoryFull 'worktree-prepared.json')
Export-AcceptanceSidecars -Root $root -State $prepared -Directory (Join-Path $directoryFull 'godot-sidecars/capture')
Write-Host "PASS post-build preparation recovered all three late UIDs; $($prepared.godot_sidecars.Count) companions ready before capture"
