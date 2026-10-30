# Launch the H:-based self-contained Godot .NET editor.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$dest = Join-Path $root 'Tools\Godot'
$editor = Get-ChildItem -LiteralPath $dest -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' -ErrorAction Stop |
    Select-Object -First 1
if ($null -eq $editor) { throw 'Godot editor not found. Run scripts/install-godot.ps1 first.' }
if (-not $editor.FullName.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Blocked: Godot outside H: $($editor.FullName)"
}
if (-not (Test-Path (Join-Path $editor.DirectoryName '_sc_'))) {
    throw 'Blocked: Godot _sc_ marker missing. Re-run scripts/install-godot.ps1.'
}
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
if (-not $dotnet.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Blocked: .NET SDK resolves outside H: $dotnet"
}
Push-Location $root
try { & $editor.FullName --editor --path $root }
finally { Pop-Location }
