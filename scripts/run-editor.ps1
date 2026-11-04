# Launch the H:-based self-contained Godot .NET editor.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$dest = Join-Path $root 'Tools\Godot'
$editor = Get-ChildItem -LiteralPath $dest -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if ($null -eq $editor) { throw 'Godot editor not found. Run scripts/install-godot.ps1 first.' }
Assert-WorldForgePortableGodot -Root $root -Executable $editor.FullName
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
Assert-WorldForgePortablePath -Root $root -Path $dotnet -Kind File
$log = Join-Path $root 'artifacts\editor\godot-editor.log'
Assert-WorldForgePortablePath -Root $root -Path $log -Kind File -AllowMissing
[IO.Directory]::CreateDirectory((Split-Path -Parent $log)) | Out-Null
Push-Location $root
try { & $editor.FullName --editor --path $root --log-file $log }
finally { Pop-Location }
