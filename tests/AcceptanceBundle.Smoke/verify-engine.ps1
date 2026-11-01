param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][string]$WorktreeBaseline
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
$baseline = Get-Content -LiteralPath $WorktreeBaseline -Raw | ConvertFrom-Json
Assert-AcceptanceWorktreeStable -Baseline $baseline -Current (Get-AcceptanceWorktreeState -Root $root)
Write-Host 'PASS actual Godot sidecar identity stayed fixed across scene and capture runs'
foreach ($scenario in @('idle','pan','zoom','paint')) {
    $direct = Read-AcceptanceCapture -Directory (Join-Path $Directory "$scenario-direct") `
        -ExpectedSession "ci-$scenario" -ExpectedScenario $scenario -ExpectedBackend direct `
        -ExpectedSeed 1315511732 -AllowHeadless
    $mirror = Read-AcceptanceCapture -Directory (Join-Path $Directory "$scenario-region-mirror") `
        -ExpectedSession "ci-$scenario" -ExpectedScenario $scenario -ExpectedBackend region-mirror `
        -ExpectedSeed 1315511732 -AllowHeadless
    Test-AcceptancePair -Direct $direct -Mirror $mirror
    Write-Host "PASS actual Godot capture integrity and fixture parity: $scenario (not GPU/Windows acceptance)"
}
