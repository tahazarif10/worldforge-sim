#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# Native Windows PowerShell 5.1 parser catches syntax that pwsh alone cannot.
foreach ($file in @(Get-ChildItem (Join-Path $root 'scripts') -Filter '*.ps1') +
    @(Get-ChildItem $PSScriptRoot -Filter '*.ps1')) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,
        [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw "PowerShell parse failure: $($file.Name): $parseErrors" }
}
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('acceptance-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
function Check($condition, [string]$name) {
    if (-not $condition) { throw "FAIL $name" }
    Write-Host "PASS $name"
}
function Reject([scriptblock]$action, [string]$name) {
    $rejected = $false
    try { & $action | Out-Null } catch { $rejected = $true }
    Check $rejected $name
}
function Fixture([string]$backend) {
    return [ordered]@{
        evidence_schema = 2; session_id = 'test'; scenario_id = 'idle'; backend = $backend
        clock_source = 'Godot.Time.GetTicksUsec'; percentile_method = 'nearest-rank'
        capture_valid = $true; headless = $true; physical_acceptance = 'PENDING'
        input_interference = $false; focus_lost = $false; failure = $null
        fixture_sha256 = ('A' * 64); os = 'Linux'; paint_actions = 0
        fixture = [ordered]@{
            streaming_planner = 'legacy-v2'
            version = 'v3.3-reference-1'; seed = 1315511732; scenario = 'idle'
            duration_seconds = 0.2; warmup_seconds = 0; quality = 'Eco'
            width = 120; height = 88; viewport_width = 1280; viewport_height = 800
            camera = @{pivot_x=60; pivot_z=44; size=79}
            simulation_tick = 0; simulation_speed = 0; renderer = 'gl_compatibility'
        }
        statistics = [ordered]@{
            complete = $true; count = 2; total_samples = 2; dropped_samples = 0
            invalid_samples = 0; duration_us = 200000; retained_duration_ms = 200
            p50_ms = 100; p95_ms = 100; p99_ms = 100; max_ms = 100
            average_ms = 100; average_fps = 10; stalls_over50_ms = 2; stalls_over100_ms = 0
        }
    }
}
function Save-Fixture($metadata, [string]$directory) {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'capture.json'),
        ($metadata | ConvertTo-Json -Depth 12), $utf8)
    [IO.File]::WriteAllText((Join-Path $directory 'frames.csv'),
        "session_id,scenario_id,sequence,elapsed_us,interval_us,engine_delta_ms`n" +
        "test,idle,1,100000,100000,16.667`ntest,idle,2,200000,100000,16.667`n", $utf8)
    [IO.File]::WriteAllText((Join-Path $directory 'camera.csv'),
        "sequence,elapsed_us,pivot_x,pivot_z,size`n1,100000,60,44,79`n2,200000,60,44,79`n", $utf8)
}
function Read-Fixture([string]$directory, [string]$backend = 'direct', [string]$planner = 'legacy-v2') {
    Read-AcceptanceCapture -Directory $directory -ExpectedSession test -ExpectedScenario idle `
        -ExpectedBackend $backend -ExpectedSeed 1315511732 -AllowHeadless -ExpectedPlanner $planner
}

function Work-Fixture {
    $m = Fixture 'direct'
    $m.evidence_schema = 3
    $m.fixture.streaming_planner = 'viewport-v3-workers'
    $m.fixture.worker_publication_policy = 'terrain-foliage-staged-v1'
    $m.fixture.work_trace_schema = 1
    $m.streaming_settled = $true; $m.worker_failure = $null
    $m.worker_window = @{ enabled=$true; max_failed_chunks=0; max_coast_failures=0;
        max_active=1; max_retained=1; max_reserved_bytes=4194304; max_upload_bytes=1000 }
    $m.work_trace = 'work.csv'
    $m.work_window = [ordered]@{
        schema=1; count=2; invalid_rows=0; dropped_rows=0; complete=$true
        max_callback_ms=5; max_action_ms=1; max_visibility_ms=3; max_flush_ms=0.5
        max_snapshot_ms=0.5; max_terrain_ms=1; max_foliage_ms=1; max_coast_ms=0; max_pump_ms=2
        slowest_intervals=@(
            @{sequence=1; interval_ms=100; callback_ms=5; action_ms=1; visibility_ms=3; flush_ms=0.5;
                snapshot_ms=0.5; terrain_ms=1; foliage_ms=0; coast_ms=0; pump_ms=2; outside_callback_ms=95},
            @{sequence=2; interval_ms=100; callback_ms=3; action_ms=0.1; visibility_ms=1.5; flush_ms=0.1;
                snapshot_ms=0; terrain_ms=0; foliage_ms=1; coast_ms=0; pump_ms=1.2; outside_callback_ms=97}
        )
    }
    return $m
}
$workCsv = "sequence,elapsed_us,interval_us,callback_start_us,callback_end_us,callback_ms,action_ms,visibility_ms,flush_ms,snapshot_ms,terrain_ms,foliage_ms,coast_ms,pump_ms,terrain_units,foliage_units,coast_units,staged_chunks,outside_callback_ms" +
    [Environment]::NewLine + "1,100000,100000,0,5000,5,1,3,0.5,0.5,1,0,0,2,1,0,0,1,95" +
    [Environment]::NewLine + "2,200000,100000,100000,103000,3,0.1,1.5,0.1,0,0,1,0,1.2,0,1,0,0,97" +
    [Environment]::NewLine
function Save-WorkFixture($metadata,[string]$directory,[string]$csv=$workCsv) {
    Save-Fixture $metadata $directory
    [IO.File]::WriteAllText((Join-Path $directory 'work.csv'),$csv,$utf8)
}

try {
    $selection = Resolve-AcceptanceSelection -Scenario PAN -Quality eco
    Check ($selection.Scenario -ceq 'pan' -and $selection.Quality -ceq 'Eco') 'PowerShell choices canonicalized for engine protocol'
    $directDir = Join-Path $tempRoot 'direct'
    $mirrorDir = Join-Path $tempRoot 'mirror'
    Save-Fixture (Fixture 'direct') $directDir
    Save-Fixture (Fixture 'region-mirror') $mirrorDir
    $direct = Read-Fixture $directDir
    $mirror = Read-Fixture $mirrorDir 'region-mirror'
    Test-AcceptancePair -Direct $direct -Mirror $mirror
    Check ($direct.capture_valid -eq $true) 'valid raw intervals, statistics and matched pair'
    Reject { Read-AcceptanceCapture -Directory $directDir -ExpectedSession test -ExpectedScenario idle `
        -ExpectedBackend direct -ExpectedSeed 1315511732 } 'headless cannot impersonate Windows evidence'
    $mirror.fixture.quality = 'Balanced'
    Reject { Test-AcceptancePair -Direct $direct -Mirror $mirror } 'actual fixture mismatch even with matching hash'
    $cases = @(
        @{name='overflow'; edit={param($m) $m.statistics.dropped_samples=1}},
        @{name='unfinished'; edit={param($m) $m.statistics.complete=$false}},
        @{name='wrong planner'; edit={param($m) $m.fixture.streaming_planner='viewport-v3-workers'}},
        @{name='wrong seed'; edit={param($m) $m.fixture.seed=99}},
        @{name='wrong session'; edit={param($m) $m.session_id='another'}},
        @{name='false physical approval'; edit={param($m) $m.physical_acceptance='PASS'}},
        @{name='engine delta clock'; edit={param($m) $m.clock_source='delta'}},
        @{name='incomplete duration'; edit={param($m) $m.fixture.duration_seconds=1}},
        @{name='incorrect percentile'; edit={param($m) $m.statistics.p99_ms=16.667}},
        @{name='incorrect FPS'; edit={param($m) $m.statistics.average_fps=60}},
        @{name='lost focus'; edit={param($m) $m.focus_lost=$true}},
        @{name='manual interference'; edit={param($m) $m.input_interference=$true}}
    )
    foreach ($case in $cases) {
        $m = Fixture 'direct'
        & $case.edit $m
        Save-Fixture $m $directDir
        Reject { Read-Fixture $directDir } $case.name
    }
    Save-Fixture (Fixture 'direct') $directDir
    Add-Content -LiteralPath (Join-Path $directDir 'frames.csv') 'test,idle,3,300000,100000,16.667'
    Reject { Read-Fixture $directDir } 'extra raw row refused'
    Save-Fixture (Fixture 'direct') $directDir
    [IO.File]::WriteAllText((Join-Path $directDir 'camera.csv'),
        "sequence,elapsed_us,pivot_x,pivot_z,size`n1,99999,60,44,79`n2,200000,60,44,79`n")
    Reject { Read-Fixture $directDir } 'misaligned camera timestamp refused'
    Save-Fixture (Fixture 'direct') $directDir
    Remove-Item -LiteralPath (Join-Path $directDir 'frames.csv')
    Reject { Read-Fixture $directDir } 'missing artifact refused'
    Save-Fixture (Fixture 'direct') $directDir
    Write-AcceptanceHashes -Directory $directDir
    Check ((Get-Content (Join-Path $directDir 'SHA256SUMS.txt')).Count -eq 3) 'artifact hash manifest'
    $workDir = Join-Path $tempRoot 'capture\worker-trace'
    Save-WorkFixture (Work-Fixture) $workDir
    $work = Read-Fixture $workDir 'direct' 'viewport-v3-workers'
    Check ($work.work_window.complete -and $work.work_window.count -eq 2) 'schema 3 aligned work, split phases and exact slow interval evidence'
    $workCases = @(
        @{name='work summary mismatch'; edit={param($m) $m.work_window.max_terrain_ms=2}},
        @{name='work invalid row'; edit={param($m) $m.work_window.invalid_rows=1}},
        @{name='work overflow'; edit={param($m) $m.work_window.dropped_rows=1}},
        @{name='work unfinished'; edit={param($m) $m.work_window.complete=$false}},
        @{name='wrong publication policy'; edit={param($m) $m.fixture.worker_publication_policy='combined'}},
        @{name='wrong trace schema'; edit={param($m) $m.fixture.work_trace_schema=99}},
        @{name='work filename mismatch'; edit={param($m) $m.work_trace='elsewhere.csv'}},
        @{name='slow interval shifted'; edit={param($m) $m.work_window.slowest_intervals[0].sequence=2}},
        @{name='slow work mismatch'; edit={param($m) $m.work_window.slowest_intervals[1].foliage_ms=0}}
    )
    foreach ($case in $workCases) {
        $m = Work-Fixture; & $case.edit $m
        Save-WorkFixture $m $workDir
        Reject { Read-Fixture $workDir 'direct' 'viewport-v3-workers' } $case.name
    }
    $rawCases = @(
        @{name='callback attached to ending interval'; from=',0,5000,5,'; to=',100000,105000,5,'},
        @{name='nonfinite work phase'; from=',0.5,1,0,0,2,'; to=',0.5,NaN,0,0,2,'},
        @{name='negative work phase'; from=',0.5,1,0,0,2,'; to=',0.5,-1,0,0,2,'},
        @{name='incorrect callback duration'; from=',5000,5,'; to=',5000,6,'},
        @{name='incorrect unclassified remainder'; from=',0,0,1,95'; to=',0,0,1,94'},
        @{name='publication outside measured pump'; from=',0.5,1,0,0,2,'; to=',0.5,9,0,0,2,'},
        @{name='work admission bound exceeded'; from=',2,1,0,0,1,95'; to=',2,2,1,0,1,95'},
        @{name='two staged replacements'; from=',0,0,1,95'; to=',0,0,2,95'},
        @{name='work sequence reordered'; from='2,200000,100000,'; to='1,200000,100000,'}
    )
    foreach ($case in $rawCases) {
        Check ($workCsv.Contains($case.from)) ("active mutation fixture: "+$case.name)
        Save-WorkFixture (Work-Fixture) $workDir ($workCsv.Replace($case.from,$case.to))
        Reject { Read-Fixture $workDir 'direct' 'viewport-v3-workers' } $case.name
    }
    Save-WorkFixture (Work-Fixture) $workDir
    Remove-Item -LiteralPath (Join-Path $workDir 'work.csv')
    Reject { Read-Fixture $workDir 'direct' 'viewport-v3-workers' } 'schema 3 missing work artifact rejected'
    Save-WorkFixture (Work-Fixture) $workDir
    Add-Content -LiteralPath (Join-Path $workDir 'work.csv') '3,300000,100000,200000,203000,3,0,0,0,0,0,0,0,0,0,0,0,0,97'
    Reject { Read-Fixture $workDir 'direct' 'viewport-v3-workers' } 'extra work row rejected'
    Save-WorkFixture (Work-Fixture) $workDir
    . (Join-Path $root 'scripts/self-test-report.ps1')
    $report = New-SelfTestReport -Directory $tempRoot -Status PASS -Failure '' -Seconds 1
    Check ($report.Contains('Frame work [worker-trace]') -and $report.Contains('"slowest_intervals"')) 'clipboard report retains correlated phase evidence'
    # Verify real Windows argv, working directory and child exit handling.
    $processDir = Join-Path $tempRoot 'native process with spaces'
    New-Item -ItemType Directory -Path $processDir | Out-Null
    $child = Join-Path $processDir 'child.ps1'
    $destination = Join-Path $processDir 'received.txt'
    [IO.File]::WriteAllText($child,
        'param([string]$Destination,[string]$Value); [IO.File]::WriteAllText($Destination,$Value)')
    $value = 'H:\a path with spaces\trailing\'
    Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') `
        -Arguments @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$child,
            '-Destination',$destination,'-Value',$value) `
        -WorkingDirectory $processDir -OutputDirectory $processDir -TimeoutSeconds 15
    Check ([IO.File]::ReadAllText($destination) -ceq $value) 'Windows argument roundtrip with spaces and trailing slash'
    [IO.File]::WriteAllText($child, 'exit 7')
    Reject { Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') `
        -Arguments @('-NoProfile','-NonInteractive','-File',$child) `
        -WorkingDirectory $processDir -OutputDirectory $processDir -TimeoutSeconds 15 } 'nonzero child exit refused'
    [IO.File]::WriteAllText($child, 'Start-Sleep -Seconds 5')
    Reject { Invoke-AcceptanceProcess -Executable (Join-Path $PSHOME 'powershell.exe') `
        -Arguments @('-NoProfile','-NonInteractive','-File',$child) `
        -WorkingDirectory $processDir -OutputDirectory $processDir -TimeoutSeconds 1 } 'hung child bounded by timeout'
    Write-Host 'PASS ALL PowerShell parser, capture failure cases and native process tests'
}
finally { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
