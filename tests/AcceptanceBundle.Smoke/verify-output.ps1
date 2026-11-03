#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/self-test-report.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('owned-output-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
function Check($ok,[string]$name) { if (-not $ok) { throw "FAIL $name" }; Write-Host "PASS $name" }
$script:heldLog = $null
function Assert-ClosedLogs([string]$Directory) {
    foreach ($name in @('stdout.log','stderr.log')) {
        $stream = [IO.File]::Open((Join-Path $Directory $name), [IO.FileMode]::Open,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Dispose()
    }
    Write-AcceptanceHashes -Directory $Directory
}
try {
    $child = Join-Path $temp 'child.ps1'
    foreach ($exitCode in @(0,7)) {
        $out = Join-Path $temp ("exit-" + $exitCode)
        New-Item -ItemType Directory -Path $out | Out-Null
        # Both outputs exceed pipe capacity and finish without a newline.
        [IO.File]::WriteAllText($child,
            '[Console]::Out.Write("O" * 262144); [Console]::Error.Write("E" * 262144); exit ' + $exitCode)
        $errorText = ''
        try {
            Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') -Arguments @(
                '-NoProfile','-NonInteractive','-File',$child) -WorkingDirectory $temp -OutputDirectory $out -TimeoutSeconds 15
        } catch { $errorText = $_.Exception.Message }
        if ($exitCode -eq 0) { Check (-not $errorText) 'stderr bytes alone do not turn exit zero into failure' }
        else { Check ($errorText -like 'Acceptance process exited 7;*') 'original nonzero exit survives stream cleanup' }
        Check ((Get-Item (Join-Path $out 'stdout.log')).Length -eq 262144 -and
            (Get-Item (Join-Path $out 'stderr.log')).Length -eq 262144) 'both large byte streams fully drained without deadlock'
        $m = Get-Content -LiteralPath (Join-Path $out 'process.json') -Raw | ConvertFrom-Json
        Check ($m.streams_complete -eq $true -and $m.exit_code -eq $exitCode) 'exit and output completion recorded independently'
        Assert-ClosedLogs $out
        Check $true 'logs are exclusively reopenable and hashable immediately on return'
    }
    $out = Join-Path $temp 'timeout'
    New-Item -ItemType Directory -Path $out | Out-Null
    [IO.File]::WriteAllText($child,'[Console]::Error.Write("before-timeout"); Start-Sleep -Seconds 20')
    $errorText = ''
    try {
        Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') -Arguments @(
            '-NoProfile','-NonInteractive','-File',$child) -WorkingDirectory $temp -OutputDirectory $out -TimeoutSeconds 1
    } catch { $errorText = $_.Exception.Message }
    Check ($errorText -like 'Acceptance process exceeded 1 seconds.*') 'timeout remains the primary failure'
    Assert-ClosedLogs $out
    Check ((Get-Content (Join-Path $out 'stderr.log') -Raw) -ceq 'before-timeout') 'timeout preserves partial stderr and releases both files'
    # Reproduce an independently held log: collection must not replace or hide the run failure.
    $script:heldLog = $null
    $result = Invoke-SelfTestBundle -Directory (Join-Path $temp 'locked-log') -NoClipboard -Action {
        param($directory)
        $path = Join-Path $directory 'stderr.log'
        [IO.File]::WriteAllText($path,'held native detail')
        $script:heldLog = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        throw 'PRIMARY_PREFLIGHT_EXIT_7'
    }
    $report = Get-Content -LiteralPath (Join-Path $result.Directory 'REPORT.txt') -Raw -Encoding UTF8
    $m = Get-Content -LiteralPath (Join-Path $result.Directory 'status.json') -Raw | ConvertFrom-Json
    Check ($result.Status -eq 'FAIL_OR_INCOMPLETE' -and $m.failure -ceq 'PRIMARY_PREFLIGHT_EXIT_7' -and
        $report.Contains('Failure: PRIMARY_PREFLIGHT_EXIT_7') -and $report.Contains('UNAVAILABLE')) 'locked log cannot mask primary failure or prevent plain report'
    Check (Test-Path -LiteralPath (Join-Path $result.Directory 'collection-warnings.txt')) 'secondary hash/archive errors are recorded separately'
    $script:heldLog.Dispose(); $script:heldLog = $null
    Write-Host 'PASS ALL native output ownership, immediate log closure and locked-file failure precedence'
} finally {
    if ($null -ne $script:heldLog) { $script:heldLog.Dispose() }
    Remove-Item -LiteralPath $temp -Recurse -Force
}
