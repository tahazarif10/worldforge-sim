# Install the official Godot 4.7.2 .NET x64 ZIP in H: only.
# Does not launch Godot before its self-contained _sc_ marker is created.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing installation outside H: ($root)"
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Requires 64-bit Windows' }
$dest = Join-Path $root 'Tools\Godot'
$downloads = Join-Path $root 'Cache\Downloads'
$zip = Join-Path $downloads 'Godot_v4.7.2-stable_mono_win64.zip'
$url = 'https://downloads.godotengine.org/?flavor=stable&platform=windows.64&slug=mono_win64.zip&version=4.7.2'
New-Item -ItemType Directory -Force -Path $dest,$downloads | Out-Null
$editor = Get-ChildItem -LiteralPath $dest -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' |
    Select-Object -First 1
if ($null -eq $editor) {
    if (-not (Test-Path -LiteralPath $zip)) {
        Write-Host "Downloading official Godot .NET ZIP to: $zip"
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zip
    } else {
        Write-Host "Reusing existing H:-based archive: $zip"
    }
    $file = Get-Item -LiteralPath $zip
    if ($file.Length -lt 40000000) { throw "Godot download too small: $($file.Length) bytes; delete bad archive and retry" }
    $expectedSha256 = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
    $actualSha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) { throw "Godot archive SHA-256 mismatch. Delete: $zip; actual: $actualSha256" }
    Write-Host 'Godot ZIP SHA-256 verified.'
    Write-Host "Extracting to: $dest"
    Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
    $editor = Get-ChildItem -LiteralPath $dest -Recurse -File -Filter 'Godot_v4.7.2-stable_mono_win64.exe' |
        Select-Object -First 1
}
if ($null -eq $editor) { throw 'Expected Godot .NET editor binary was not found after extraction' }
if (-not $editor.FullName.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Blocked: editor binary outside H: ($($editor.FullName))"
}
$signature = Get-AuthenticodeSignature -LiteralPath $editor.FullName
Write-Host "Authenticode status: $($signature.Status); signer: $($signature.SignerCertificate.Subject)"
if ($signature.Status -ne 'Valid') { throw 'Official Godot editor signature did not verify. Do not execute unverified download.' }
$marker = Join-Path $editor.DirectoryName '_sc_'
if (-not (Test-Path -LiteralPath $marker)) {
    New-Item -ItemType File -Force -Path $marker | Out-Null
}
$editorData = Join-Path $editor.DirectoryName 'editor_data'
New-Item -ItemType Directory -Force -Path $editorData | Out-Null
Write-Host "Verified portable Godot editor: $($editor.FullName)"
Write-Host "Self-contained marker: $marker"
Write-Host "Editor data: $editorData"
Write-Host "Installation finished. Do NOT launch a copy of Godot from C:."
