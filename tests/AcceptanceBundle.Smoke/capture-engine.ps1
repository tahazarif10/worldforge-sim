param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Directory,
    [switch]$MeshWorkers
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$directoryFull = [IO.Path]::GetFullPath($Directory)
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
$baseline = Get-Content -LiteralPath (Join-Path $directoryFull 'worktree-prepared.json') -Raw | ConvertFrom-Json
$prefix = if ($MeshWorkers) { 'workers-' } else { '' }
$planner = if ($MeshWorkers) { 'viewport-v3-workers' } else { 'legacy-v2' }
foreach ($scenario in @('idle','pan','zoom','paint')) {
    $pairs = @{}
    foreach ($backend in @('direct','region-mirror')) {
        Assert-AcceptanceWorktreeStable -Baseline $baseline -Current (Get-AcceptanceWorktreeState -Root $root)
        $output = Join-Path $directoryFull "$prefix$scenario-$backend"
        New-Item -ItemType Directory -Path $output | Out-Null
        $arguments = @('--headless','--path',$root,'--scene','res://Scenes/VisualLab3D.tscn',
            '--rendering-method','gl_compatibility','--resolution','1280x800','--max-fps','120',
            '--log-file',(Join-Path $output 'godot.log'),'--',
            '--world-seed=1315511732',"--terrain-backend=$backend",
            "--acceptance-session=ci-$prefix$scenario","--acceptance-scenario=$scenario",
            "--acceptance-output=$output",'--acceptance-duration=1.1',
            '--acceptance-warmup=0.1','--acceptance-quality=Eco')
        if ($MeshWorkers) { $arguments += '--mesh-workers' }
        $processArgs = @{
            Executable = $Executable; Arguments = $arguments; WorkingDirectory = $root
            OutputDirectory = $output; TimeoutSeconds = 90
        }
        $after = Invoke-AcceptanceObservedProcess @processArgs
        $captureArgs = @{
            Directory = $output; ExpectedSession = "ci-$prefix$scenario"; ExpectedScenario = $scenario
            ExpectedBackend = $backend; ExpectedSeed = 1315511732; AllowHeadless = $true; ExpectedPlanner = $planner
        }
        $capture = Read-AcceptanceCapture @captureArgs
        if ($capture.evidence_schema -ne 3 -or -not $capture.work_window.complete) {
            throw 'Real engine did not produce complete schema 3 work evidence.'
        }
        if ($scenario -ceq 'paint') {
            if ($capture.work_window.max_action_ms -le 0) { throw 'Paint action was not timed.' }
            if ($MeshWorkers) {
                $rows = @(Import-Csv -LiteralPath (Join-Path $output 'work.csv'))
                foreach ($phase in @('terrain_units','foliage_units','coast_units')) {
                    if (-not @($rows | Where-Object { [int]($_.$phase) -gt 0 }).Count) {
                        throw "Paint capture did not exercise $phase."
                    }
                }
            }
        }
        Write-Host "PASS correlated work evidence: $planner / $scenario / $backend"
        $pairs[$backend] = $capture
        Assert-AcceptanceWorktreeStable -Baseline $baseline -Current $after
        Write-Host "PASS observed real Godot capture and fixed sidecar identity: $planner / $scenario / $backend"
    }
    Test-AcceptancePair -Direct $pairs.direct -Mirror $pairs['region-mirror']
    Write-Host "PASS paired real capture: $planner / $scenario"
}
