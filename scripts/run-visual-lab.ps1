# Launch opt-in 3D style prototype without changing project.godot or main scene.
# Uses existing portable Godot/.NET on H: and writes its diagnostic log to H:.
param([Nullable[int]]$WorldSeed = $null)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing visual lab outside H: ($root)"
}
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
$dir = Join-Path $root 'artifacts\visual-lab'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$log = Join-Path $dir 'godot-3d-console.log'
Write-Host 'Launching stand-alone 3D visual lab, preserving the 2D main scene.'
Write-Host "Log: $log"
$godotArgs = @('--path', $root, '--scene', 'res://Scenes/VisualLab3D.tscn',
    '--rendering-method', 'gl_compatibility', '--log-file', $log)
if ($null -ne $WorldSeed) {
    Write-Host "Replaying approved 3D world seed $WorldSeed for mountain QA."
    $godotArgs += @('--', "--world-seed=$WorldSeed")
}
& $editor.FullName @godotArgs
