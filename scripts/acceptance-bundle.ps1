# Shared PS 5.1-compatible validation. No engine launch or filesystem writes
# occur when this file is dot-sourced.
function Read-AcceptanceCapture {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$ExpectedSession,
        [Parameter(Mandatory = $true)][ValidateSet('idle','pan','zoom','paint')][string]$ExpectedScenario,
        [Parameter(Mandatory = $true)][ValidateSet('direct','region-mirror')][string]$ExpectedBackend,
        [Parameter(Mandatory = $true)][int]$ExpectedSeed,
        [switch]$AllowHeadless,
        [ValidateSet('legacy-v2','viewport-v2-fallback','viewport-v3-workers')][string]$ExpectedPlanner = 'legacy-v2'
    )
    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version Latest
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    foreach ($name in @('capture.json','frames.csv','camera.csv')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $name) -PathType Leaf)) {
            throw "Incomplete capture: missing $name in $Directory. The run did not reach automatic completion; early window closure is one possible cause. Inspect godot.log, stdout.log and stderr.log."
        }
    }
    $m = Get-Content -LiteralPath (Join-Path $Directory 'capture.json') -Raw | ConvertFrom-Json
    if ($m.capture_valid -isnot [bool] -or $m.headless -isnot [bool] -or
        $m.statistics.complete -isnot [bool]) { throw 'Invalid boolean evidence fields.' }
    if ($m.evidence_schema -ne 2 -or $m.capture_valid -ne $true -or
        $m.statistics.complete -ne $true -or $m.physical_acceptance -ne 'PENDING') {
        throw 'Capture is invalid, unfinished or claims unsupported physical approval.'
    }
    if ($m.session_id -cne $ExpectedSession -or $m.scenario_id -cne $ExpectedScenario -or
        $m.backend -cne $ExpectedBackend -or $m.fixture.seed -ne $ExpectedSeed -or
        $m.fixture.scenario -cne $ExpectedScenario) { throw 'Capture identity mismatch.' }
    if ($m.clock_source -cne 'Godot.Time.GetTicksUsec' -or
        $m.percentile_method -cne 'nearest-rank' -or
        $m.fixture.version -cne 'v3.3-reference-1') { throw 'Unsupported clock or fixture schema.' }
    if ($m.fixture.streaming_planner -cne $ExpectedPlanner) { throw 'Streaming planner identity mismatch.' }
    if ($ExpectedPlanner -ceq 'viewport-v3-workers') {
        if (-not $m.streaming_settled -or -not $m.worker_window.enabled -or
            $m.worker_window.max_failed_chunks -ne 0 -or $m.worker_window.max_coast_failures -ne 0 -or
            $m.worker_window.max_active -gt 2 -or $m.worker_window.max_retained -gt 4 -or
            $m.worker_window.max_reserved_bytes -gt 16777216 -or
            $m.worker_window.max_upload_bytes -gt 2097152 -or
            $null -ne $m.worker_failure) { throw 'Worker capture failed its bounded/settled contract.' }
    }
    if ($m.headless -and -not $AllowHeadless) { throw 'Headless output is not Windows host evidence.' }
    if (-not $AllowHeadless -and ($m.os -cne 'Windows' -or
        $m.fixture.renderer -cne 'gl_compatibility')) { throw 'Expected Windows Compatibility capture.' }
    if ($m.input_interference -or $m.focus_lost -or $null -ne $m.failure -or
        $m.statistics.dropped_samples -ne 0 -or $m.statistics.invalid_samples -ne 0) {
        throw 'Capture contains interference, lost samples or clock failures.'
    }
    if ($m.fixture.simulation_tick -ne 0 -or $m.fixture.simulation_speed -ne 0 -or
        $m.fixture.width -ne 120 -or $m.fixture.height -ne 88 -or
        $m.fixture.viewport_width -ne 1280 -or $m.fixture.viewport_height -ne 800 -or
        $m.fixture.camera.pivot_x -ne 60 -or $m.fixture.camera.pivot_z -ne 44 -or
        $m.fixture.camera.size -ne 79) { throw 'Reference world/camera/viewport fixture changed.' }
    if (-not $m.headless) {
        foreach ($name in @('before.png','after.png')) {
            $path = Join-Path $Directory $name
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing screenshot: $name" }
            $bytes = [IO.File]::ReadAllBytes($path)
            if ($bytes.Length -lt 24 -or
                [BitConverter]::ToString($bytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') {
                throw "Invalid PNG artifact: $name"
            }
        }
    }
    $frames = @(Import-Csv -LiteralPath (Join-Path $Directory 'frames.csv'))
    $camera = @(Import-Csv -LiteralPath (Join-Path $Directory 'camera.csv'))
    if ($frames.Count -eq 0 -or $frames.Count -ne $m.statistics.count -or
        $frames.Count -ne $m.statistics.total_samples -or $camera.Count -ne $frames.Count) {
        throw 'Frame/pose sample counts do not agree.'
    }
    [long]$elapsed = 0
    $intervals = New-Object 'System.Collections.Generic.List[double]'
    [long]$over50 = 0
    [long]$over100 = 0
    for ($i = 0; $i -lt $frames.Count; $i++) {
        $frame = $frames[$i]
        [long]$sequence = [long]::Parse($frame.sequence, $invariant)
        [long]$interval = [long]::Parse($frame.interval_us, $invariant)
        [long]$timestamp = [long]::Parse($frame.elapsed_us, $invariant)
        [double]$delta = [double]::Parse($frame.engine_delta_ms, $invariant)
        if ($sequence -ne ($i + 1) -or $interval -le 0 -or
            $frame.session_id -cne $ExpectedSession -or
            $frame.scenario_id -cne $ExpectedScenario -or $delta -lt 0 -or
            [double]::IsNaN($delta) -or [double]::IsInfinity($delta)) {
            throw "Invalid raw sample at row $i"
        }
        $elapsed += $interval
        if ($timestamp -ne $elapsed -or
            [long]::Parse($camera[$i].sequence, $invariant) -ne $sequence -or
            [long]::Parse($camera[$i].elapsed_us, $invariant) -ne $timestamp) {
            throw 'Truncated, reordered or mismatched frame/pose trace.'
        }
        foreach ($property in @('pivot_x','pivot_z','size')) {
            [double]$value = [double]::Parse($camera[$i].$property, $invariant)
            if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) { throw 'Invalid camera sample.' }
        }
        [double]$ms = $interval / 1000.0
        $intervals.Add($ms)
        if ($ms -gt 50) { $over50++ }
        if ($ms -gt 100) { $over100++ }
    }
    [double]$required = $m.fixture.duration_seconds
    if ([double]::IsNaN($required) -or [double]::IsInfinity($required) -or
        $required -lt 0.1 -or $required -gt 180 -or
        $elapsed -ne $m.statistics.duration_us -or $elapsed / 1000000.0 -lt $required) {
        throw 'Requested capture window is incomplete.'
    }
    $sorted = @($intervals | Sort-Object)
    $checks = @{
        p50_ms = $sorted[[int][Math]::Ceiling(.50 * $sorted.Count) - 1]
        p95_ms = $sorted[[int][Math]::Ceiling(.95 * $sorted.Count) - 1]
        p99_ms = $sorted[[int][Math]::Ceiling(.99 * $sorted.Count) - 1]
        max_ms = $sorted[-1]
        average_ms = $elapsed / 1000.0 / $frames.Count
        average_fps = $frames.Count * 1000000.0 / $elapsed
        retained_duration_ms = $elapsed / 1000.0
    }
    foreach ($key in $checks.Keys) {
        [double]$actual = $m.statistics.$key
        if ([double]::IsNaN($actual) -or [double]::IsInfinity($actual) -or
            [Math]::Abs($actual - $checks[$key]) -gt 0.000001) {
            throw "Summary disagrees with raw intervals: $key"
        }
    }
    if ($m.statistics.stalls_over50_ms -ne $over50 -or
        $m.statistics.stalls_over100_ms -ne $over100) { throw 'Stall counts disagree with raw intervals.' }
    if ($ExpectedScenario -eq 'paint' -and
        $m.paint_actions -ne [int][Math]::Ceiling($required * 4)) {
        throw 'Paint workload is incomplete.'
    }
    return $m
}

function Test-AcceptancePair {
    param([Parameter(Mandatory = $true)]$Direct, [Parameter(Mandatory = $true)]$Mirror)
    if ($Direct.backend -cne 'direct' -or $Mirror.backend -cne 'region-mirror' -or
        $Direct.session_id -cne $Mirror.session_id -or
        $Direct.scenario_id -cne $Mirror.scenario_id -or
        $Direct.fixture_sha256 -cne $Mirror.fixture_sha256 -or
        ($Direct.fixture | ConvertTo-Json -Depth 12 -Compress) -cne
        ($Mirror.fixture | ConvertTo-Json -Depth 12 -Compress)) {
        throw 'Direct/mirror fixture mismatch. Performance comparison is invalid.'
    }
}

function Write-AcceptanceHashes {
    param([Parameter(Mandatory = $true)][string]$Directory)
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $lines = @(Get-ChildItem -LiteralPath $Directory -File -Recurse |
        Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
            $relative = $_.FullName.Substring($root.Length).Replace('\','/')
            '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
        })
    [IO.File]::WriteAllLines((Join-Path $Directory 'SHA256SUMS.txt'), $lines,
        (New-Object Text.UTF8Encoding($false)))
}

function Invoke-AcceptanceProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )
    # Start-Process joins ArgumentList; explicitly apply Windows argv quoting.
    # No cmd.exe, expression evaluation or shell expansion is involved.
    $quoted = @($Arguments | ForEach-Object {
        if ($_ -match "[\x00\r\n]") { throw 'Invalid native argument.' }
        $value = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $value = [regex]::Replace($value, '(\\+)$', '$1$1')
        '"' + $value + '"'
    })
    $process = Start-Process -FilePath $Executable -ArgumentList ($quoted -join ' ') `
        -WorkingDirectory $WorkingDirectory -PassThru `
        -RedirectStandardOutput (Join-Path $OutputDirectory 'stdout.log') `
        -RedirectStandardError (Join-Path $OutputDirectory 'stderr.log')
    $processClock = [Diagnostics.Stopwatch]::StartNew()
    $processOutcome = 'interrupted'
    $nativeExit = $null
    try {
        $handle = $process.Handle
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $processOutcome = 'timeout'
            throw "Acceptance process exceeded $TimeoutSeconds seconds."
        }
        $process.WaitForExit()
        $nativeExit = $process.ExitCode
        $processOutcome = 'exited'
        if ($nativeExit -ne 0) {
            throw "Acceptance process exited $nativeExit; inspect godot.log, stdout.log and stderr.log."
        }
    }
    finally {
        # Kill only the owned child/tree on timeout or interruption; never leave an import/build orphan.
        try {
            if (-not $process.HasExited) {
                if ($env:OS -eq 'Windows_NT') {
                    & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $process.Id /T /F 2>&1 | Out-Null
                } else { $process.Kill() }
                [void]$process.WaitForExit(5000)
            }
        } catch {
            try { if (-not $process.HasExited) { $process.Kill() } } catch { }
        }
        $processClock.Stop()
        try {
            [IO.File]::WriteAllText((Join-Path $OutputDirectory 'process.json'),
                ([ordered]@{ outcome=$processOutcome; exit_code=$nativeExit;
                    elapsed_seconds=$processClock.Elapsed.TotalSeconds; timeout_seconds=$TimeoutSeconds } | ConvertTo-Json),
                (New-Object Text.UTF8Encoding($false)))
        } catch { Write-Warning 'Could not preserve process outcome metadata.' }
        $process.Dispose()
    }
}

function Resolve-AcceptanceSelection {
    param(
        [ValidateSet('all','idle','pan','zoom','paint')][string]$Scenario,
        [ValidateSet('Eco','Balanced')][string]$Quality
    )
    # PowerShell ValidateSet is case-insensitive; the engine protocol is exact.
    return [pscustomobject]@{
        Scenario = $Scenario.ToLowerInvariant()
        Quality = $(if ($Quality -ieq 'Eco') { 'Eco' } else { 'Balanced' })
    }
}
