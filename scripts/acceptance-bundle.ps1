# Schema 3 joins completed owner work to the interval START, not its ending callback.
# Parent/child timings overlap; outside_callback_ms is an unclassified remainder.
function Test-AcceptanceFrameWork {
    param([string]$Directory, $Metadata, [object[]]$Frames, [string]$ExpectedPlanner)
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    $m = $Metadata
    $expectedPolicy = if ($ExpectedPlanner -ceq 'viewport-v3-workers') { 'terrain-foliage-staged-v1' } else { 'synchronous-v2' }
    if ($m.work_trace -cne 'work.csv' -or $m.fixture.work_trace_schema -ne 1 -or
        $m.fixture.worker_publication_policy -cne $expectedPolicy -or
        $m.work_window.schema -ne 1 -or $m.work_window.complete -isnot [bool] -or
        -not $m.work_window.complete -or $m.work_window.count -ne $Frames.Count -or
        $m.work_window.invalid_rows -ne 0 -or $m.work_window.dropped_rows -ne 0) {
        throw 'Invalid work-trace identity, completeness or publication policy.'
    }
    $path = Join-Path $Directory 'work.csv'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Missing required work.csv.' }
    $rows = @(Import-Csv -LiteralPath $path)
    if ($rows.Count -ne $Frames.Count) { throw 'Work/frame row counts do not agree.' }
    $durations = @('callback_ms','action_ms','visibility_ms','flush_ms','snapshot_ms',
        'terrain_ms','foliage_ms','coast_ms','pump_ms','outside_callback_ms')
    $maxima = @{}
    foreach ($key in $durations) { $maxima[$key] = 0.0 }
    $ranked = New-Object 'System.Collections.Generic.List[object]'
    $unitLimit = if ($m.fixture.quality -ceq 'Eco') { 2 } else { 4 }
    for ($i=0; $i -lt $rows.Count; $i++) {
        $r = $rows[$i]; $f = $Frames[$i]
        [long]$sequence = [long]::Parse($r.sequence,$invariant)
        [long]$elapsed = [long]::Parse($r.elapsed_us,$invariant)
        [long]$interval = [long]::Parse($r.interval_us,$invariant)
        [long]$start = [long]::Parse($r.callback_start_us,$invariant)
        [long]$end = [long]::Parse($r.callback_end_us,$invariant)
        if ($sequence -ne [long]$f.sequence -or $elapsed -ne [long]$f.elapsed_us -or
            $interval -ne [long]$f.interval_us -or $start -ne ($elapsed-$interval) -or
            $end -lt $start -or $end -gt $elapsed) {
            throw "Work trace is shifted, reordered or outside its interval at row $i."
        }
        $values = @{}
        foreach ($key in $durations) {
            [double]$value = [double]::Parse($r.$key,$invariant)
            if ($value -lt 0 -or [double]::IsNaN($value) -or [double]::IsInfinity($value)) {
                throw "Nonfinite or negative work duration: $key at row $i."
            }
            $values[$key] = $value
            $maxima[$key] = [Math]::Max($maxima[$key],$value)
        }
        [double]$callback = ($end-$start)/1000.0
        if ([Math]::Abs($values.callback_ms-$callback) -gt 0.000001 -or
            [Math]::Abs($values.outside_callback_ms-($interval/1000.0-$callback)) -gt 0.000001 -or
            $values.action_ms+$values.visibility_ms+$values.flush_ms -gt $callback+0.25 -or
            $values.snapshot_ms+$values.terrain_ms+$values.foliage_ms+$values.coast_ms -gt $values.pump_ms+0.25 -or
            $values.pump_ms -gt $values.visibility_ms+0.25) {
            throw "Work duration arithmetic/nesting mismatch at row $i."
        }
        [long]$units = 0
        foreach ($key in @('terrain_units','foliage_units','coast_units')) {
            [int]$value = [int]::Parse($r.$key,$invariant)
            if ($value -lt 0 -or $value -gt $unitLimit) { throw 'Invalid publication admission count.' }
            $units += $value
        }
        [int]$staged = [int]::Parse($r.staged_chunks,$invariant)
        if ($units -gt $unitLimit -or $staged -lt 0 -or $staged -gt 1) {
            throw 'Work trace violates publication/staged ownership bounds.'
        }
        if ($ExpectedPlanner -cne 'viewport-v3-workers' -and
            ($units -ne 0 -or $staged -ne 0 -or $values.pump_ms -ne 0)) {
            throw 'Synchronous fixture cannot claim worker publication activity.'
        }
        $values['sequence'] = $sequence
        $values['interval_ms'] = $interval/1000.0
        $ranked.Add([pscustomobject]$values)
    }
    foreach ($key in @('callback_ms','action_ms','visibility_ms','flush_ms','snapshot_ms',
        'terrain_ms','foliage_ms','coast_ms','pump_ms')) {
        [double]$actual = $m.work_window.("max_"+$key)
        if ([double]::IsNaN($actual) -or [double]::IsInfinity($actual) -or
            [Math]::Abs($actual-$maxima[$key]) -gt 0.000001) {
            throw "Work summary disagrees with raw rows: max_$key"
        }
    }
    $slow = @($ranked | Sort-Object @{Expression='interval_ms';Descending=$true},@{Expression='sequence';Descending=$false} | Select-Object -First 3)
    $reported = @($m.work_window.slowest_intervals)
    if ($reported.Count -ne $slow.Count) { throw 'Slow-interval evidence count mismatch.' }
    for ($i=0; $i -lt $slow.Count; $i++) {
        if ($reported[$i].sequence -ne $slow[$i].sequence) { throw 'Slow-interval sequence identity mismatch.' }
        foreach ($key in @('interval_ms')+$durations) {
            [double]$actual = $reported[$i].$key
            if ([double]::IsNaN($actual) -or [double]::IsInfinity($actual) -or
                [Math]::Abs($actual-$slow[$i].$key) -gt 0.000001) {
                throw "Slow-interval work disagrees with raw row: $key"
            }
        }
    }
}

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
    if ($m.evidence_schema -notin @(2,3) -or $m.capture_valid -ne $true -or
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
    if ($m.evidence_schema -eq 3) {
        Test-AcceptanceFrameWork -Directory $Directory -Metadata $m -Frames $frames -ExpectedPlanner $ExpectedPlanner
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
        [Parameter(Mandatory = $true)][ValidateRange(1,3600)][int]$TimeoutSeconds,
        [ValidateRange(100,10000)][int]$StreamDrainMilliseconds = 3000
    )
    $ErrorActionPreference = 'Stop'
    # Own both process and file streams. A Start-Process returned handle alone
    # does not give this caller ownership of the cmdlet's redirection writers.
    $quoted = @($Arguments | ForEach-Object {
        if ($_ -match "[\x00\r\n]") { throw 'Invalid native argument.' }
        $value = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $value = [regex]::Replace($value, '(\\+)$', '$1$1')
        '"' + $value + '"'
    })
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Executable
    $info.Arguments = $quoted -join ' '
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $started = $false
    $nativeExit = $null
    $outcome = 'start-failed'
    $streamsComplete = $false
    $primary = $null
    $streamFailure = $null
    $stdout = $null
    $stderr = $null
    [Threading.Tasks.Task[]]$copies = @()
    try {
        $stdout = [IO.File]::Open((Join-Path $OutputDirectory 'stdout.log'),
            [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $stderr = [IO.File]::Open((Join-Path $OutputDirectory 'stderr.log'),
            [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $started = $process.Start()
        if (-not $started) { throw 'Native process did not start.' }
        $outcome = 'interrupted'
        # Drain both pipes concurrently, including stderr with a zero exit status.
        # Raw bytes never become PowerShell NativeCommandError records.
        $copies = @($process.StandardOutput.BaseStream.CopyToAsync($stdout),
                    $process.StandardError.BaseStream.CopyToAsync($stderr))
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $outcome = 'timeout'
            throw "Acceptance process exceeded $TimeoutSeconds seconds."
        }
        $nativeExit = $process.ExitCode
        $outcome = 'exited'
        if ($nativeExit -ne 0) {
            throw "Acceptance process exited $nativeExit; inspect godot.log, stdout.log and stderr.log."
        }
    }
    catch { $primary = $_ }
    finally {
        # Stop only the owned running child/tree; never kill all Godot/dotnet processes.
        try {
            if ($started -and -not $process.HasExited) {
                if ($env:OS -eq 'Windows_NT') {
                    & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $process.Id /T /F 2>&1 | Out-Null
                } else { $process.Kill() }
                [void]$process.WaitForExit(5000)
            }
        } catch {
            try { if ($started -and -not $process.HasExited) { $process.Kill() } } catch { }
        }
        try {
            if ($copies.Count -eq 2) {
                $streamsComplete = [Threading.Tasks.Task]::WaitAll($copies, $StreamDrainMilliseconds)
                if (-not $streamsComplete) { $streamFailure = 'Output pipes did not reach EOF within the bounded drain period.' }
            }
        } catch { $streamFailure = $_.Exception.Message }
        # Dispose our actual file handles even after timeout, pipe failure or cancellation.
        # This boundary precedes hash/report/ZIP collection.
        if ($started) {
            try { $process.StandardOutput.Dispose() } catch { }
            try { $process.StandardError.Dispose() } catch { }
        }
        foreach ($stream in @($stdout,$stderr)) {
            if ($null -ne $stream) {
                try { $stream.Dispose() } catch { if (-not $streamFailure) { $streamFailure = $_.Exception.Message } }
            }
        }
        foreach ($copy in $copies) {
            if ($copy.IsFaulted) { $null = $copy.Exception } # Observe completed faults.
        }
        $clock.Stop()
        try {
            [IO.File]::WriteAllText((Join-Path $OutputDirectory 'process.json'),
                ([ordered]@{
                    outcome=$outcome; exit_code=$nativeExit; streams_complete=$streamsComplete
                    stream_failure=$streamFailure; elapsed_seconds=$clock.Elapsed.TotalSeconds
                    timeout_seconds=$TimeoutSeconds
                    primary_failure=$(if ($null -ne $primary) { $primary.Exception.Message } else { $null })
                } | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
        } catch { Write-Warning 'Could not preserve process outcome metadata.' }
        $process.Dispose()
    }
    if ($null -ne $primary) { throw $primary }
    if (-not $streamsComplete -or $streamFailure) { throw "Incomplete native output: $streamFailure" }
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
