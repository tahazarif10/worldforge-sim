# Launch opt-in independent regional map preview without changing project.godot or main scene.
# Uses existing portable Godot/.NET on H: and writes its diagnostic log to H:.
param([switch]$ExperimentalV3)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing regional 3D map preview outside H: ($root)"
}
if (-not $ExperimentalV3) {
    Write-Warning 'The independent V3 scene failed user visual and camera/movement acceptance.'
    Write-Host 'Launching approved V2.9 visuals and controls instead.'
    Write-Host 'Pass -ExperimentalV3 ONLY for engineering/debug work on the rejected V3 prototype.'
    & (Join-Path $PSScriptRoot 'run-visual-lab.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Approved V2 3D scene launch failed.' }
    return
}
Write-Warning 'EXPERIMENTAL: V3 regional scene has known visual holes and degraded controls. Not a playable successor to V2.'
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$editor = Get-ChildItem -Path (Join-Path $root 'Tools\Godot') -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if (-not $editor) { throw 'Godot .NET editor missing under H:\WorldForge-Sim\Tools\Godot.' }
if (-not $editor.FullName.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Godot executable is not located on H:.'
}
if (-not (Test-Path (Join-Path $editor.DirectoryName '_sc_'))) {
    throw 'Portable Godot _sc_ marker is missing.'
}
$build = Join-Path $root '.godot\mono\temp\bin\Debug\WorldForgeSim.dll'
if (-not (Test-Path $build)) {
    throw 'Godot C# build not found. Run the H:-based dotnet build command first.'
}
$dir = Join-Path $root 'artifacts\regional-world'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$log = Join-Path $dir 'godot-regional-3d-console.log'
Write-Host 'Launching stand-alone 3D regional 3D map preview, preserving the 2D main scene.'
Write-Host "Log: $log"
& $editor.FullName --path $root --scene 'res://Scenes/RegionalWorldLab3D.tscn' --rendering-method gl_compatibility --log-file $log
