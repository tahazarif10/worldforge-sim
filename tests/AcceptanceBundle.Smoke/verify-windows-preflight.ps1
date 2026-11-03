#requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$Sdk
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:OS -ne 'Windows_NT') {
    throw 'This hosted Windows integration fixture must run only in GitHub Actions.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-preflight.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
. (Join-Path $root 'scripts/self-test-report.ps1')
$before = Get-AcceptanceWorktreeState -Root $root
$directory = Join-Path $root ('artifacts/acceptance/windows-preflight-' + [Guid]::NewGuid().ToString('N'))
$result = Invoke-SelfTestBundle -Directory $directory -NoClipboard -Action {
    param($output)
    Invoke-AcceptancePreflight -Root $root -Executable $Executable -Sdk $Sdk -Directory (Join-Path $output 'preflight')
    Initialize-AcceptanceRuntime -Executable $Executable -Root $root -Directory (Join-Path $output 'preparation') -WorldSeed 1315511732 -FeatureArguments @('--mesh-workers')
    $after = Get-AcceptanceWorktreeState -Root $root
    Assert-AcceptanceWorktreeStable -Baseline $before -Current $after -AllowSidecarRefresh
    Assert-AcceptanceResourcesReady -State $after
    $processes = @(Get-ChildItem -LiteralPath $output -Filter 'process.json' -File -Recurse)
    if ($processes.Count -ne 12) { throw "Expected 9 preflight + 3 preparation processes; got $($processes.Count)." }
    foreach ($file in $processes) {
        $m = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ($m.exit_code -ne 0 -or $m.streams_complete -ne $true -or $m.outcome -cne 'exited') {
            throw "Native stage did not finish and drain: $($file.Directory.Name)"
        }
        foreach ($name in @('stdout.log','stderr.log')) {
            $stream = [IO.File]::Open((Join-Path $file.Directory.FullName $name), [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $stream.Dispose()
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $root '.godot/mono/temp/bin/Debug/WorldForgeSim.dll'))) {
        throw 'Real game assembly was not built.'
    }
    Write-Host 'PASS official Windows Godot import, seven art tests, actual game build and both worker boots; 12 completed native stages with closed logs'
}
if ($result.Status -ne 'PASS' -or -not $result.Archive) {
    Get-Content -LiteralPath (Join-Path $directory 'REPORT.txt')
    throw "Real Windows preflight failed. Evidence: $directory"
}
Write-Host 'PASS real Windows preflight report, SHA256 manifest and ZIP are immediately readable; GPU acceptance remains pending'
