#requires -Version 5.1
# One-click short diagnostics for the opt-in worker candidate. No upload or Git mutation.
param([switch]$NoClipboard)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($env:OS -ne 'Windows_NT' -or -not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run RUN-TEST.cmd from the existing H:\WorldForge-Sim Windows checkout.'
}
Set-Location $root
. (Join-Path $PSScriptRoot 'acceptance-bundle.ps1')
. (Join-Path $PSScriptRoot 'self-test-report.ps1')
$session = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$directory = Join-Path $root ('artifacts\acceptance\self-test\' + $session)
Write-Host 'AUTOMATED SHORT TEST: idle, pan, zoom, paint on direct and region-mirror.'
Write-Host 'Eight windows open in sequence and close themselves: 3s warm-up + 8s capture each.'
Write-Host 'Build/import and worker settling add time. Do not click, resize, close or leave a test window.'
Write-Host 'Esc cancels. Logs and a report are preserved even when a test fails or is canceled.'
$result = Invoke-SelfTestBundle -Directory $directory -NoClipboard:$NoClipboard -Action {
    param($output)
    & (Join-Path $PSScriptRoot 'run-v3-3-acceptance.ps1') -Quick -MeshWorkers -Scenario all -Repeats 1 -EvidenceDirectory (Join-Path $output 'capture')
}
if ($result.Status -ne 'PASS') { exit 1 }
exit 0
