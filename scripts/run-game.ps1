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
Assert-WorldForgePortableGodot -Root $root -Executable $editor.FullName
if (-not (Test-Path (Join-Path $root '.godot\mono\temp\bin\Debug\WorldForgeSim.dll'))) {
    Write-Warning 'Godot C# build missing. Build project with H:-based dotnet before launching.'
}
Write-Host 'Launching standalone game WITHOUT Godot editor/debugger (helps isolate editor overhead).'
Write-Host "Godot executable: $($editor.FullName)"
$log = Join-Path $root 'artifacts\game\godot-game.log'
Assert-WorldForgePortablePath -Root $root -Path $log -Kind File -AllowMissing
[IO.Directory]::CreateDirectory((Split-Path -Parent $log)) | Out-Null
& $editor.FullName --path $root --log-file $log
