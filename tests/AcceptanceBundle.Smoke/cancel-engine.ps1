param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Directory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
$baseline = Get-AcceptanceWorktreeState -Root $root
foreach ($mode in @('close','escape')) {
    $output = Join-Path ([IO.Path]::GetFullPath($Directory)) ("cancel-" + $mode)
    New-Item -ItemType Directory -Path $output | Out-Null
    $arguments = @('--headless','--path',$root,'--scene','res://tests/AcceptanceBundle.Smoke/CancelProbe.tscn',
        '--rendering-method','gl_compatibility','--resolution','1280x800','--max-fps','120',
        '--log-file',(Join-Path $output 'godot.log'),'--','--mesh-workers',"--cancel-probe=$mode",
        '--world-seed=1315511732','--terrain-backend=direct',
        "--acceptance-session=cancel-$mode",'--acceptance-scenario=idle',
        "--acceptance-output=$output",'--acceptance-duration=8','--acceptance-warmup=3',
        '--acceptance-quality=Eco')
    $rejected = $false
    try {
        Invoke-AcceptanceObservedProcess -Executable $Executable -Arguments $arguments -WorkingDirectory $root -OutputDirectory $output -TimeoutSeconds 45 | Out-Null
    } catch {
        if ($_.Exception.Message -notlike 'Acceptance process exited 2;*') { throw }
        $rejected = $true
    }
    if (-not (Test-Path -LiteralPath (Join-Path $output 'incomplete.json'))) {
        foreach ($name in @('stdout.log','stderr.log','godot.log')) {
            Write-Host "[$mode/$name]"
            Get-Content -LiteralPath (Join-Path $output $name) -Tail 35
        }
    }
    $incomplete = Get-Content -LiteralPath (Join-Path $output 'incomplete.json') -Raw | ConvertFrom-Json
    $expected = if ($mode -eq 'close') { 'Operator closed' } else { 'Operator pressed Esc' }
    if (-not $rejected -or $incomplete.capture_valid -ne $false -or
        $incomplete.physical_acceptance -cne 'PENDING' -or
        -not $incomplete.failure.StartsWith($expected) -or
        (Test-Path -LiteralPath (Join-Path $output 'capture.json'))) {
        throw "Cancellation was misreported as valid: $mode"
    }
    Assert-AcceptanceWorktreeStable -Baseline $baseline -Current (Get-AcceptanceWorktreeState -Root $root)
    Write-Host "PASS actual engine cancellation evidence and exit code 2: $mode"
}
