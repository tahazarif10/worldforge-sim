# Standalone game run from the portable Godot editor binary, without editor/debugger.
# The project and all deliberate data must remain on H:.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to run project outside H: ($root)"
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$editor = Get-ChildItem -Path (Join-Path $root 'Tools\Godot') -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if (-not $editor) { throw 'Godot executable missing from H:. Run scripts/install-godot.ps1.' }
if (-not $editor.FullName.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Godot executable is outside H:.' }
if (-not (Test-Path (Join-Path $editor.DirectoryName '_sc_'))) { throw 'Portable _sc_ marker is missing.' }
if (-not (Test-Path (Join-Path $root '.godot\mono\temp\bin\Debug\WorldForgeSim.dll'))) {
    Write-Warning 'Godot C# build missing. Build project with H:-based dotnet before launching.'
}
Write-Host 'Launching standalone game WITHOUT Godot editor/debugger (helps isolate editor overhead).'
Write-Host "Godot executable: $($editor.FullName)"
& $editor.FullName --path $root
