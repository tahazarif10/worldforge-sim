# Shared preparation/observation; dot-source bundle and worktree helpers first.
# Preparation runs are excluded from the measured scenario windows.
function Initialize-AcceptanceRuntime {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][int]$WorldSeed,
        [ValidateSet('--mesh-workers','--viewport-streaming')][string[]]$FeatureArguments = @()
    )
    $ErrorActionPreference = 'Stop'
    $seedText = $WorldSeed.ToString([Globalization.CultureInfo]::InvariantCulture)
    foreach ($stage in @('post-build-import','direct','region-mirror')) {
        $output = Join-Path $Directory $stage
        New-Item -ItemType Directory -Path $output | Out-Null
        $arguments = @('--headless','--path',$Root,'--rendering-method','gl_compatibility',
            '--log-file',(Join-Path $output 'godot.log'))
        if ($stage -eq 'post-build-import') {
            $arguments += @('--editor','--import')
        } else {
            $arguments += @('--scene','res://Scenes/VisualLab3D.tscn','--quit-after','24',
                '--',"--world-seed=$seedText","--terrain-backend=$stage") + $FeatureArguments
        }
        Write-Host "Preparing runtime: $stage (outside capture)."
        $processArgs = @{
            Executable = $Executable; Arguments = $arguments; WorkingDirectory = $Root
            OutputDirectory = $output; TimeoutSeconds = 120
        }
        Invoke-AcceptanceProcess @processArgs
        if ($stage -ne 'post-build-import') {
            # GUI Windows builds may route messages to the Godot log rather than stdout.
            $parts = @(foreach ($name in @('godot.log','stdout.log')) {
                $path = Join-Path $output $name
                if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw }
            })
            $log = [string]::Join([Environment]::NewLine, [string[]]$parts)
            if (-not $log.Contains("V2 reference world seed: $seedText") -or
                -not $log.Contains("V2 approved renderer and camera: terrain source = $stage")) {
                throw "Preparation did not initialize the expected C# scene/backend: $stage. Inspect $output"
            }
        }
    }
    Assert-AcceptanceResourcesReady -State (Get-AcceptanceWorktreeState -Root $Root)
}

function Invoke-AcceptanceObservedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )
    $ErrorActionPreference = 'Stop'
    $utf8 = New-Object Text.UTF8Encoding($false)
    $state = $null
    $snapshotFailure = $null
    try {
        Invoke-AcceptanceProcess @PSBoundParameters
    }
    finally {
        # Preserve evidence even after a crash, timeout or operator cancellation.
        # A snapshot failure must not replace an existing native-process exception.
        try {
            $state = Get-AcceptanceWorktreeState -Root $WorkingDirectory
            [IO.File]::WriteAllText((Join-Path $OutputDirectory 'worktree-after.json'),
                ($state | ConvertTo-Json -Depth 8), $utf8)
        }
        catch {
            $snapshotFailure = $_
            try {
                [IO.File]::WriteAllText((Join-Path $OutputDirectory 'worktree-after.error.txt'),
                    $_.Exception.Message, $utf8)
            } catch { }
        }
    }
    if ($null -ne $snapshotFailure) { throw $snapshotFailure }
    return $state
}
