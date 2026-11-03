#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/self-test-report.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temp = Join-Path ([IO.Path]::GetTempPath()) ('self-test space ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
function Check($condition,[string]$name) {
    if (-not $condition) { throw "FAIL $name" }
    Write-Host "PASS $name"
}
$script:clipboardText = ''
function Set-Clipboard {
    param([string]$Value, [string]$ErrorAction)
    $script:clipboardText = $Value
}
try {
    $pass = Invoke-SelfTestBundle -Directory (Join-Path $temp 'pass with spaces') -Action {
        param($directory)
        New-Item -ItemType Directory -Path (Join-Path $directory 'capture') | Out-Null
        Write-SelfTestText -Path (Join-Path $directory 'capture\result.md') -Text 'Capture integrity: VALID'
        Write-Output 'Synthetic success; no host or GPU acceptance claimed.'
    }
    Check ($pass.Status -eq 'PASS' -and $pass.Copied -and (Test-Path -LiteralPath $pass.Archive)) 'success archives evidence and copies the report'
    $report = Get-Content -LiteralPath (Join-Path $pass.Directory 'REPORT.txt') -Raw -Encoding UTF8
    Check ($script:clipboardText -ceq $report -and $report.Contains('PENDING')) 'clipboard matches the persisted report without fabricated physical approval'
    $zip = [IO.Compression.ZipFile]::OpenRead($pass.Archive)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        Check (@($entries | Where-Object { $_ -match '/REPORT.txt$' }).Count -eq 1) 'ZIP contains the report'
        Check (@($entries | Where-Object { $_ -match '/SHA256SUMS.txt$' }).Count -eq 1) 'ZIP contains the hash manifest'
    } finally { $zip.Dispose() }
    $failure = Invoke-SelfTestBundle -Directory (Join-Path $temp 'failure') -NoClipboard -Action {
        param($directory)
        Write-SelfTestText -Path (Join-Path $directory 'stderr.log') -Text 'Synthetic native failure details'
        throw 'Intentional game cancellation/failure'
    }
    $report = Get-Content -LiteralPath (Join-Path $failure.Directory 'REPORT.txt') -Raw -Encoding UTF8
    Check ($failure.Status -eq 'FAIL_OR_INCOMPLETE' -and (Test-Path -LiteralPath $failure.Archive) -and
        $report.Contains('Intentional game cancellation/failure') -and $report.Contains('Synthetic native failure details')) 'failed/canceled run preserves partial logs and ZIP'
    $early = Invoke-SelfTestBundle -Directory (Join-Path $temp 'early') -NoClipboard -Action {
        throw 'Missing tool or dirty worktree before capture'
    }
    Check ($early.Status -eq 'FAIL_OR_INCOMPLETE' -and (Test-Path -LiteralPath $early.Archive)) 'preflight failure before capture still produces report and ZIP'
    $refused = $false
    try { Invoke-SelfTestBundle -Directory $pass.Directory -NoClipboard -Action { throw 'Must not run' } | Out-Null }
    catch { $refused = $true }
    Check $refused 'existing run cannot be overwritten'
    function Set-Clipboard { param([string]$Value,[string]$ErrorAction) throw 'Clipboard busy' }
    $noClipboard = Invoke-SelfTestBundle -Directory (Join-Path $temp 'clipboard busy') -Action { Write-Output 'done' }
    Check ($noClipboard.Status -eq 'PASS' -and -not $noClipboard.Copied -and
        (Test-Path -LiteralPath (Join-Path $noClipboard.Directory 'REPORT.txt'))) 'clipboard failure does not lose evidence or change test verdict'
    # Exercise the actual CMD entry point in a fixture, without triggering H: engine work.
    $launcher = Join-Path $temp 'launcher spaces'
    New-Item -ItemType Directory -Path (Join-Path $launcher 'scripts') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'RUN-TEST.cmd') -Destination $launcher
    Write-SelfTestText -Path (Join-Path $launcher 'scripts\run-self-test.ps1') -Text 'Write-Output "CMD_ENTRY_OK"; exit 7'
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /c RUN-TEST.cmd <nul'
    $start.WorkingDirectory = $launcher
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $child = [Diagnostics.Process]::Start($start)
    try {
        if (-not $child.WaitForExit(20000)) { $child.Kill(); throw 'CMD fixture timed out.' }
        $entryOutput = $child.StandardOutput.ReadToEnd()
        $entryError = $child.StandardError.ReadToEnd()
        Check ($child.ExitCode -eq 7 -and $entryOutput.Contains('CMD_ENTRY_OK') -and -not $entryError) 'real CMD launcher quotes paths and preserves child exit code'
    } finally { $child.Dispose() }
    # Run the actual H:-guarded entry script with a stub capture action in disposable hosted CI.
    # This checks parameter plumbing and script-relative resolution; it is not engine/GPU evidence.
    if ($env:GITHUB_ACTIONS -eq 'true') {
        if (Test-Path -LiteralPath 'H:\') { throw 'Hosted fixture requires an unused H: drive.' }
        $mapped = $false
        $hostFixture = Join-Path $temp 'host fixture'
        New-Item -ItemType Directory -Path (Join-Path $hostFixture 'scripts') | Out-Null
        foreach ($name in @('run-self-test.ps1','self-test-report.ps1','acceptance-bundle.ps1')) {
            Copy-Item -LiteralPath (Join-Path $root ('scripts\' + $name)) -Destination (Join-Path $hostFixture 'scripts')
        }
        $stub = @'
param([switch]$Quick,[switch]$MeshWorkers,[string]$Scenario,[int]$Repeats,[string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
if (-not $Quick -or -not $MeshWorkers -or $Scenario -cne 'all' -or $Repeats -ne 1 -or
    -not $EvidenceDirectory.StartsWith('H:\artifacts\acceptance\self-test\')) { throw 'Wrong wrapper parameters.' }
New-Item -ItemType Directory -Path $EvidenceDirectory | Out-Null
Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'result.md') -Value 'WRAPPER_PARAMETERS_VERIFIED; synthetic only'
'@
        Write-SelfTestText -Path (Join-Path $hostFixture 'scripts\run-v3-3-acceptance.ps1') -Text $stub
        try {
            & (Join-Path $env:SystemRoot 'System32\subst.exe') H: $hostFixture
            if ($LASTEXITCODE -ne 0) { throw 'Could not create the disposable hosted drive.' }
            $mapped = $true
            $entryLog = Join-Path $temp 'entry logs'
            New-Item -ItemType Directory -Path $entryLog | Out-Null
            Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') -Arguments @(
                '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File','H:\scripts\run-self-test.ps1','-NoClipboard') -WorkingDirectory 'H:\' -OutputDirectory $entryLog -TimeoutSeconds 30
            $reports = @(Get-ChildItem -LiteralPath 'H:\artifacts\acceptance\self-test' -Filter 'REPORT.txt' -Recurse -File)
            Check ($reports.Count -eq 1 -and
                (Get-Content -LiteralPath $reports[0].FullName -Raw).Contains('WRAPPER_PARAMETERS_VERIFIED')) 'actual H: wrapper resolves scripts and selects quick worker all-scenario paired capture'
        } finally {
            if ($mapped) { & (Join-Path $env:SystemRoot 'System32\subst.exe') H: /D | Out-Null }
        }
    }
    Write-Host 'PASS ALL Windows self-test packaging, failure recovery, clipboard fallback and CMD entry point'
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
