#requires -Version 5.1
# Runs fresh same-scene direct/mirror fixtures. Writes only under H:.
# No Git update, reset, stash, download, project.godot edit or product auto-approval.
param(
    [ValidateSet('all','idle','pan','zoom','paint')][string]$Scenario = 'all',
    [int]$WorldSeed = 1315511732,
    [ValidateSet('Eco','Balanced')][string]$Quality = 'Eco',
    [ValidateRange(1,3)][int]$Repeats = 3,
    [ValidateRange(0,240)][int]$MaxFps = 60
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($env:OS -ne 'Windows_NT' -or
    -not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run acceptance from the existing H:\WorldForge-Sim Windows checkout.'
}
Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
. (Join-Path $PSScriptRoot 'acceptance-bundle.ps1')
$selection = Resolve-AcceptanceSelection -Scenario $Scenario -Quality $Quality
$Scenario = $selection.Scenario
$Quality = $selection.Quality
$seedText = $WorldSeed.ToString([Globalization.CultureInfo]::InvariantCulture)
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
$editor = Join-Path $root 'Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
foreach ($tool in @($sdk, $editor)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Missing H: tool: $tool" }
}
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $editor -Parent) '_sc_'))) {
    throw 'Portable Godot _sc_ marker missing; refusing a profile cache outside H:.'
}
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Unable to identify Git commit.' }
$status = @(& git -C $root status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect working tree.' }
# Allow the established user-local project.godot change; every other change
# requires reconciliation so an unknown binary/source is not measured.
foreach ($line in $status) {
    if ($line.Length -lt 4 -or $line.Substring(3) -cne 'project.godot') {
        throw "Unreviewed working-tree change: $line. Preserve/reconcile it before acceptance."
    }
}
$projectHash = (Get-FileHash -LiteralPath (Join-Path $root 'project.godot') -Algorithm SHA256).Hash
$session = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$bundle = Join-Path $root ("artifacts\acceptance\v3-3\" + $session)
New-Item -ItemType Directory -Path $bundle | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$rows = New-Object 'System.Collections.Generic.List[string]'
$report = Join-Path $bundle 'result.md'
$failed = $null
try {
    # Rebuild once using the repository's existing H: import/build preflight.
    & (Join-Path $PSScriptRoot 'verify-v2-art.ps1') -NoLaunch 2>&1 |
        Out-File -LiteralPath (Join-Path $bundle 'preflight.log') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'V2 preflight failed.' }
    foreach ($project in @('FrameTime.Smoke','TerrainBackendParity.Smoke')) {
        & $sdk run --project (Join-Path $root ("tests\$project\$project.csproj")) -c Release 2>&1 |
            Out-File -LiteralPath (Join-Path $bundle ($project + '.log')) -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "Acceptance correctness test failed: $project" }
    }
    $assembly = Join-Path $root '.godot\mono\temp\bin\Debug\WorldForgeSim.dll'
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Fresh game assembly missing.' }
    $assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    $driver = $null
    try { $driver = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion) }
    catch { Write-Warning 'GPU driver metadata unavailable; the field remains null.' }
    $sdkVersion = (& $sdk --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'SDK version query failed.' }
    $metadata = [ordered]@{
        evidence_schema = 2; session_id = $session; candidate_commit_sha = $commit
        git_status = $status; project_godot_sha256 = $projectHash
        assembly_sha256 = $assemblyHash; dotnet_sdk = $sdkVersion
        powershell_version = $PSVersionTable.PSVersion.ToString()
        requested_seed = $WorldSeed; quality = $Quality; max_fps = $MaxFps
        repeats = $Repeats; gpu_driver = $driver
        physical_acceptance = 'PENDING'
        notes = 'Paused deterministic renderer fixtures. User input, live simulation and art approval require separate review.'
    }
    [IO.File]::WriteAllText((Join-Path $bundle 'metadata.json'),
        ($metadata | ConvertTo-Json -Depth 10), $utf8)
    $scenarios = if ($Scenario -eq 'all') { @('idle','pan','zoom','paint') } else { @($Scenario) }
    foreach ($name in $scenarios) {
        $seconds = if ($name -eq 'pan') { 60 } else { 30 }
        for ($repeat = 1; $repeat -le $Repeats; $repeat++) {
            $captures = @{}
            # Alternate order across repeats to reduce systematic first-run bias.
            $backends = if ($repeat % 2 -eq 1) { @('direct','region-mirror') } else { @('region-mirror','direct') }
            foreach ($backend in $backends) {
                $currentCommit = (& git -C $root rev-parse HEAD).Trim()
                $currentStatus = @(& git -C $root status --porcelain=v1 --untracked-files=all)
                if ($LASTEXITCODE -ne 0 -or $currentCommit -cne $commit -or
                    ($currentStatus -join "`n") -cne ($status -join "`n") -or
                    (Get-FileHash -LiteralPath (Join-Path $root 'project.godot') -Algorithm SHA256).Hash -cne $projectHash -or
                    (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -cne $assemblyHash) {
                    throw 'Source/configuration/build identity changed during capture.'
                }
                $directory = Join-Path $bundle ("$name-$repeat-$backend")
                New-Item -ItemType Directory -Path $directory | Out-Null
                $runId = "$session-$name-$repeat"
                $arguments = @('--path', $root, '--scene', 'res://Scenes/VisualLab3D.tscn',
                    '--rendering-method','gl_compatibility','--resolution','1280x800','--windowed',
                    '--max-fps', $MaxFps.ToString([Globalization.CultureInfo]::InvariantCulture),
                    '--log-file',(Join-Path $directory 'godot.log'),'--',
                    "--world-seed=$seedText", "--terrain-backend=$backend",
                    "--acceptance-session=$runId", "--acceptance-scenario=$name",
                    "--acceptance-output=$directory", "--acceptance-duration=$seconds",
                    '--acceptance-warmup=10', "--acceptance-quality=$Quality")
                Write-Host "Capturing $name / $backend / pass $repeat. Leave the window focused; Esc cancels."
                Invoke-AcceptanceProcess -Executable $editor -Arguments $arguments `
                    -WorkingDirectory $root -OutputDirectory $directory -TimeoutSeconds ($seconds + 130)
                $capture = Read-AcceptanceCapture -Directory $directory -ExpectedSession $runId `
                    -ExpectedScenario $name -ExpectedBackend $backend -ExpectedSeed $WorldSeed
                $captures[$backend] = $capture
                $s = $capture.statistics
                $rows.Add("| $name | $repeat | $backend | $($s.count) | $($s.p50_ms) | $($s.p95_ms) | $($s.p99_ms) | $($s.max_ms) |")
            }
            Test-AcceptancePair -Direct $captures['direct'] -Mirror $captures['region-mirror']
        }
    }
    $text = "# V3.3 capture bundle`n`nCapture integrity: VALID`nPhysical acceptance: PENDING`n" +
        "Commit: $commit`nSession: $session`n`n" +
        "Paused simulation; continuous scripted traces. Timing is callback intervals, not GPU or physical input latency.`n`n" +
        "| Scenario | Pass | Backend | Samples | p50 ms | p95 ms | p99 ms | Max ms |`n" +
        "| --- | --- | --- | --- | --- | --- | --- | --- |`n" +
        ($rows -join "`n") + "`n`nUser mountain/coast/camera verdict: PENDING`n" +
        "Manual input, live simulation, save/load and no-hole streaming gates: PENDING`n"
    [IO.File]::WriteAllText($report, $text, $utf8)
}
catch {
    $failed = $_
    [IO.File]::WriteAllText($report,
        "# V3.3 capture bundle`n`nCapture integrity: FAIL`nPhysical acceptance: PENDING`n`n" +
        $_.Exception.Message + "`nCompleted rows:`n" + ($rows -join "`n") + "`n", $utf8)
}
finally {
    Write-AcceptanceHashes -Directory $bundle
    Write-Host "Evidence: $bundle"
}
if ($null -ne $failed) { throw $failed }
