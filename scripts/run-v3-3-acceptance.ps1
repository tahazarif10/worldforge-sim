#requires -Version 5.1
# Runs fresh same-scene direct/mirror fixtures. Writes only under H:.
# No Git update, reset, stash, download, project.godot edit or product auto-approval.
param(
    [ValidateSet('all','idle','pan','zoom','paint')][string]$Scenario = 'all',
    [int]$WorldSeed = 1315511732,
    [ValidateSet('Eco','Balanced')][string]$Quality = 'Eco',
    [ValidateRange(1,3)][int]$Repeats = 3,
    [ValidateRange(0,240)][int]$MaxFps = 60,
    [switch]$Quick,
    [switch]$ViewportStreaming,
    [switch]$MeshWorkers,
    [string]$EvidenceDirectory = ''
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
. (Join-Path $PSScriptRoot 'acceptance-worktree.ps1')
. (Join-Path $PSScriptRoot 'acceptance-runtime.ps1')
. (Join-Path $PSScriptRoot 'acceptance-preflight.ps1')
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
$initialWorktree = Get-AcceptanceWorktreeState -Root $root
$commit = $initialWorktree.commit_sha
$projectHash = $initialWorktree.project_godot_sha256
Write-Host "Recognized $($initialWorktree.godot_sidecars.Count) Godot sidecars; preserving their contents and hashes."
$session = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$bundle = Join-Path $root ("artifacts\acceptance\v3-3\" + $session)
if ($EvidenceDirectory) {
    $bundle = [IO.Path]::GetFullPath($EvidenceDirectory)
    $allowed = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\acceptance')) + '\'
    if (-not $bundle.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'EvidenceDirectory must be inside this checkout artifacts\acceptance.'
    }
}
if (Test-Path -LiteralPath $bundle) { throw "Refusing to overwrite evidence: $bundle" }
New-Item -ItemType Directory -Path $bundle | Out-Null
$planner = if ($MeshWorkers) { 'viewport-v3-workers' } elseif ($ViewportStreaming) { 'viewport-v2-fallback' } else { 'legacy-v2' }
$featureArgs = if ($MeshWorkers) { @('--mesh-workers') } elseif ($ViewportStreaming) { @('--viewport-streaming') } else { @() }
$warmup = if ($Quick) { 3 } else { 10 }
$profile = if ($Quick) { 'short-diagnostic' } else { 'acceptance-window' }
$utf8 = New-Object Text.UTF8Encoding($false)
$rows = New-Object 'System.Collections.Generic.List[string]'
$report = Join-Path $bundle 'result.md'
$failed = $null
try {
    [IO.File]::WriteAllText((Join-Path $bundle 'worktree-before-import.json'),
        ($initialWorktree | ConvertTo-Json -Depth 8), $utf8)
    Export-AcceptanceSidecars -Root $root -State $initialWorktree -Directory (Join-Path $bundle 'godot-sidecars/before-import')
    # Rebuild once using the repository's existing H: import/build preflight.
    $preflight = Join-Path $bundle 'preflight'
    Write-Host 'Preparing assets and building the game (up to 10 minutes; see preflight logs).'
    Invoke-AcceptancePreflight -Root $root -Executable $editor -Sdk $sdk -Directory $preflight
    $correctness = @('FrameTime.Smoke','TerrainBackendParity.Smoke')
    if ($MeshWorkers) { $correctness += @('MeshWorkers.Smoke','CoastPatches.Smoke') }
    foreach ($project in $correctness) {
        $output = Join-Path $bundle $project
        New-Item -ItemType Directory -Path $output | Out-Null
        Write-Host "Correctness test: $project"
        Invoke-AcceptanceProcess -Executable $sdk -Arguments @('run','--project',
            (Join-Path $root ("tests\$project\$project.csproj")),'-c','Release') -WorkingDirectory $root -OutputDirectory $output -TimeoutSeconds 120
    }
    # Load the newly built C# assembly before sealing companion identity.
    Initialize-AcceptanceRuntime -Executable $editor -Root $root -Directory (Join-Path $bundle 'preparation') -WorldSeed $WorldSeed -FeatureArguments $featureArgs
    # Preparation may generate/refresh companions. Source/config must stay fixed.
    $captureWorktree = Get-AcceptanceWorktreeState -Root $root
    Assert-AcceptanceResourcesReady -State $captureWorktree
    Assert-AcceptanceWorktreeStable -Baseline $initialWorktree -Current $captureWorktree -AllowSidecarRefresh
    [IO.File]::WriteAllText((Join-Path $bundle 'worktree-prepared.json'),
        ($captureWorktree | ConvertTo-Json -Depth 8), $utf8)
    Export-AcceptanceSidecars -Root $root -State $captureWorktree -Directory (Join-Path $bundle 'godot-sidecars/capture')
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
        git_status_before_import = $initialWorktree.git_status
        git_status = $captureWorktree.git_status; project_godot_sha256 = $projectHash
        godot_sidecars = $captureWorktree.godot_sidecars
        godot_sidecar_snapshot = 'godot-sidecars/capture'
        sidecar_policy = 'Tracked-resource companions; frozen after post-build import and both runtime boots.'
        runtime_preparation = @('post-build-import','direct','region-mirror')
        assembly_sha256 = $assemblyHash; dotnet_sdk = $sdkVersion
        powershell_version = $PSVersionTable.PSVersion.ToString()
        requested_seed = $WorldSeed; quality = $Quality; max_fps = $MaxFps
        repeats = $Repeats; gpu_driver = $driver
        evidence_profile = $profile; streaming_planner = $planner; quick_diagnostic = [bool]$Quick
        physical_acceptance = 'PENDING'
        notes = 'Paused deterministic renderer fixtures. User input, live simulation and art approval require separate review.'
    }
    [IO.File]::WriteAllText((Join-Path $bundle 'metadata.json'),
        ($metadata | ConvertTo-Json -Depth 10), $utf8)
    function Assert-HostCaptureIdentity {
        param($CurrentWorktree = $null)
        if ($null -eq $CurrentWorktree) { $CurrentWorktree = Get-AcceptanceWorktreeState -Root $root }
        Assert-AcceptanceWorktreeStable -Baseline $captureWorktree -Current $currentWorktree
        if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -cne $assemblyHash) {
            throw 'Build identity changed during capture.'
        }
    }
    $scenarios = if ($Scenario -eq 'all') { @('idle','pan','zoom','paint') } else { @($Scenario) }
    foreach ($name in $scenarios) {
        $seconds = if ($Quick) { 8 } elseif ($name -eq 'pan') { 60 } else { 30 }
        for ($repeat = 1; $repeat -le $Repeats; $repeat++) {
            $captures = @{}
            # Alternate order across repeats to reduce systematic first-run bias.
            $backends = if ($repeat % 2 -eq 1) { @('direct','region-mirror') } else { @('region-mirror','direct') }
            foreach ($backend in $backends) {
                Assert-HostCaptureIdentity
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
                    "--acceptance-warmup=$warmup", "--acceptance-quality=$Quality") + $featureArgs
                Write-Host "AUTOMATED TEST: $name / $backend / pass $repeat. Controls are paused. Wait for automatic close (${warmup}s warm-up + ${seconds}s capture, plus settling/saving); Esc cancels."
                $afterCapture = Invoke-AcceptanceObservedProcess -Executable $editor -Arguments $arguments `
                    -WorkingDirectory $root -OutputDirectory $directory -TimeoutSeconds ($seconds + 130)
                # Missing output is reported before identity drift; the post-run manifest is already saved.
                $capture = Read-AcceptanceCapture -Directory $directory -ExpectedSession $runId `
                    -ExpectedScenario $name -ExpectedBackend $backend -ExpectedSeed $WorldSeed -ExpectedPlanner $planner
                Assert-HostCaptureIdentity -CurrentWorktree $afterCapture
                $captures[$backend] = $capture
                $s = $capture.statistics
                $rows.Add("| $name | $repeat | $backend | $($s.count) | $($s.p50_ms) | $($s.p95_ms) | $($s.p99_ms) | $($s.max_ms) |")
            }
            Test-AcceptancePair -Direct $captures['direct'] -Mirror $captures['region-mirror']
        }
    }
    $text = "# V3.3 capture bundle`n`nCapture integrity: VALID`nPhysical acceptance: PENDING`n" +
        "Commit: $commit`nSession: $session`nProfile: $profile`nPlanner: $planner`n`n" +
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
    try { Write-AcceptanceHashes -Directory $bundle }
    catch {
        # Preserve the first failure. Evidence collection must not replace it.
        if ($null -eq $failed) { $failed = $_ }
        try {
            [IO.File]::WriteAllText((Join-Path $bundle 'collection-error.txt'),
                $_.Exception.Message, $utf8)
        } catch { }
        Write-Warning 'Artifact hashing incomplete; see collection-error.txt and the preserved primary failure.'
    }
    Write-Host "Evidence: $bundle"
}
if ($null -ne $failed) { throw $failed }
