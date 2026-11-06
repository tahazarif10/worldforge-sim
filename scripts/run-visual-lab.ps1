# Launch opt-in 3D style prototype without changing project.godot or main scene.
# Uses existing portable Godot/.NET on H: and writes its diagnostic log to H:.
param([Nullable[int]]$WorldSeed = $null, [switch]$ViewportStreaming, [switch]$MeshWorkers, [switch]$RiggedCitizenDemo,
    [ValidateSet('Compact', 'Expanded')][string]$WorldSize = 'Expanded',
    [switch]$NoCitizenAI, [switch]$FreshWorld)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing visual lab outside H: ($root)"
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$editor = Get-ChildItem -Path (Join-Path $root 'Tools\Godot') -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if (-not $editor) { throw 'Godot .NET editor missing under H:\WorldForge-Sim\Tools\Godot.' }
Assert-WorldForgePortableGodot -Root $root -Executable $editor.FullName
$sdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
Assert-WorldForgePortablePath -Root $root -Path $sdk -Kind File

Write-Host 'Building current C# sources before launch...'
& $sdk build (Join-Path $root 'WorldForgeSim.csproj') -c Debug --disable-build-servers -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'WorldForge C# build failed. Game was not launched.' }

$build = Join-Path $root '.godot\mono\temp\bin\Debug\WorldForgeSim.dll'
if (-not (Test-Path $build -PathType Leaf)) {
    throw 'Current Godot C# build output was not created.'
}
$dir = Join-Path $root 'artifacts\visual-lab'
$log = Join-Path $dir 'godot-3d-console.log'
Assert-WorldForgePortablePath -Root $root -Path $log -Kind File -AllowMissing
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Write-Host 'Launching stand-alone 3D visual lab, preserving the 2D main scene.'
Write-Host "Log: $log"
$godotArgs = @('--path', $root, '--scene', 'res://Scenes/VisualLab3D.tscn',
    '--rendering-method', 'gl_compatibility', '--log-file', $log)
# The launcher previews the larger world by default; the unchanged direct
# scene and acceptance fixtures retain 120x88 when no size is supplied.
$godotArgs += '--'
# Only the manual H: launcher shows the menu; direct scenes and CI keep
# their original startup behavior.
$godotArgs += '--game-menu'
$dimensions = if ($WorldSize -eq 'Compact') { '120x88' } else { '192x144' }
$godotArgs += "--world-size=$dimensions"
Write-Host "World preview size: $dimensions (use -WorldSize Compact to compare)."
if (-not $NoCitizenAI) {
    $godotArgs += '--citizen-ai'
    $godotArgs += '--citizen-housing'
    if (-not $FreshWorld) {
        $godotArgs += '--resume-world'
        Write-Host 'Start menu ON: New World starts with ZERO citizens; Continue explicitly loads old saves.'
        Write-Host 'Esc opens pause/settings/help; Ctrl+S saves, Ctrl+L asks before loading.'
    }
    else {
        Write-Host 'Fresh world mode: no save loaded or written. Start menu still opens with zero citizens.'
    }
    Write-Host 'Autonomous gathering and three-house village ON after you manually place citizens.'
}
if ($null -ne $WorldSeed) {
    Write-Host "Replaying approved 3D world seed $WorldSeed for mountain QA."
    $godotArgs += "--world-seed=$WorldSeed"
}
if ($ViewportStreaming -or $MeshWorkers) {
    Write-Host 'Experimental viewport streaming ON: surface fallback enabled; Windows performance acceptance is pending.'
    $godotArgs += '--viewport-streaming'
}
if ($MeshWorkers) {
    Write-Host 'Experimental mesh workers ON: bounded numeric jobs and main-thread publication; host performance pending.'
    $godotArgs += '--mesh-workers'
}
if ($RiggedCitizenDemo) {
    Write-Host 'One rigged citizen preview ON: all other villagers remain instanced.'
    $godotArgs += '--rigged-citizen-demo'
}
& $editor.FullName @godotArgs
