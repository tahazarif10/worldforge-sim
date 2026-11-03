# Shared direct native import/build sequence. Dot-source acceptance-bundle.ps1 first.
function Invoke-AcceptancePreflight {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$Sdk,
        [Parameter(Mandatory = $true)][string]$Directory,
        [ValidateRange(30,1800)][int]$TimeoutSeconds = 600
    )
    $ErrorActionPreference = 'Stop'
    foreach ($tool in @($Executable,$Sdk)) {
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Preflight tool missing: $tool" }
    }
    $assets = @(Get-ChildItem -LiteralPath (Join-Path $Root 'Assets/Art/KenneyNature') -Filter '*.glb' -File)
    if ($assets.Count -ne 10) { throw "Expected 10 bundled nature GLBs; found $($assets.Count)." }
    if (Test-Path -LiteralPath $Directory) { throw "Refusing to overwrite preflight: $Directory" }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    $budget = [Diagnostics.Stopwatch]::StartNew()
    function Invoke-PreflightStage {
        param([string]$Name,[string]$Tool,[string[]]$ToolArguments)
        $remaining = [int][Math]::Floor($TimeoutSeconds - $budget.Elapsed.TotalSeconds)
        if ($remaining -lt 1) { throw 'Total preflight time budget exhausted.' }
        $output = Join-Path $Directory $Name
        New-Item -ItemType Directory -Path $output | Out-Null
        Write-Host "Preflight: $Name"
        try {
            Invoke-AcceptanceProcess -Executable $Tool -Arguments $ToolArguments -WorkingDirectory $Root -OutputDirectory $output -TimeoutSeconds $remaining
        } catch { throw "Preflight stage '$Name' failed: $($_.Exception.Message) Logs: $output" }
    }
    Write-Host 'Importing actual Kenney CC0 glTF models into the portable Godot cache.'
    Invoke-PreflightStage -Name 'import' -Tool $Executable -ToolArguments @(
        '--headless','--editor','--import','--path',$Root,'--rendering-method','gl_compatibility',
        '--log-file',(Join-Path $Directory 'import/godot.log'))
    foreach ($project in @('NatureScatter.Smoke','MountainSummit.Smoke','EnvironmentArt.Smoke',
        'WaterAndStreaming.Smoke','TerrainRelief.Smoke','SharedHeightfield.Smoke','ElevationRegion.Smoke')) {
        Invoke-PreflightStage -Name $project -Tool $Sdk -ToolArguments @('run','--project',
            (Join-Path $Root ("tests/$project/$project.csproj")),'-c','Release',
            '--disable-build-servers','-p:UseSharedCompilation=false')
    }
    Invoke-PreflightStage -Name 'build' -Tool $Sdk -ToolArguments @('build',
        (Join-Path $Root 'WorldForgeSim.csproj'),'-c','Debug','--disable-build-servers','-p:UseSharedCompilation=false')
    Write-Host 'GLB import, art scatter checks and game build succeeded.'
}
