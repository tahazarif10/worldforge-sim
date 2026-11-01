param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Directory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$directoryFull = [IO.Path]::GetFullPath($Directory)
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
$baseline = Get-Content -LiteralPath (Join-Path $directoryFull 'worktree-prepared.json') -Raw | ConvertFrom-Json
foreach ($scenario in @('idle','pan','zoom','paint')) {
    foreach ($backend in @('direct','region-mirror')) {
        Assert-AcceptanceWorktreeStable -Baseline $baseline -Current (Get-AcceptanceWorktreeState -Root $root)
        $output = Join-Path $directoryFull "$scenario-$backend"
        New-Item -ItemType Directory -Path $output | Out-Null
        $arguments = @('--headless','--path',$root,'--scene','res://Scenes/VisualLab3D.tscn',
            '--rendering-method','gl_compatibility','--resolution','1280x800','--max-fps','120',
            '--log-file',(Join-Path $output 'godot.log'),'--',
            '--world-seed=1315511732',"--terrain-backend=$backend",
            "--acceptance-session=ci-$scenario","--acceptance-scenario=$scenario",
            "--acceptance-output=$output",'--acceptance-duration=1.1',
            '--acceptance-warmup=0.1','--acceptance-quality=Eco')
        $processArgs = @{
            Executable = $Executable; Arguments = $arguments; WorkingDirectory = $root
            OutputDirectory = $output; TimeoutSeconds = 50
        }
        $after = Invoke-AcceptanceObservedProcess @processArgs
        $captureArgs = @{
            Directory = $output; ExpectedSession = "ci-$scenario"; ExpectedScenario = $scenario
            ExpectedBackend = $backend; ExpectedSeed = 1315511732; AllowHeadless = $true
        }
        $capture = Read-AcceptanceCapture @captureArgs
        Assert-AcceptanceWorktreeStable -Baseline $baseline -Current $after
        Write-Host "PASS observed real Godot capture and fixed sidecar identity: $scenario / $backend"
    }
}
