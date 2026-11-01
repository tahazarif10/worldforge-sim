param([Parameter(Mandatory = $true)][string]$Directory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
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
