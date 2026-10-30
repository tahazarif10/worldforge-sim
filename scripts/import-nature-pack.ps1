# Import CC0 glTF/GLB nature models into the optional V2 3D visual lab.
# Usage: .\scripts\import-nature-pack.ps1 -ZipPath 'H:\Downloads\nature-pack.zip'
# Only extracts/copies on H:, does NOT download files to Windows C:.
param(
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [string]$TreePattern = '*tree*.glb',
    [string]$RockPattern = '*rock*.glb'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Project must be on H: ($root)"
}
$zip = (Resolve-Path -LiteralPath $ZipPath).Path
if (-not $zip.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Pack ZIP must already be on H: ($zip). Move it there before importing."
}
if (-not $zip.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only .zip pack archives are supported.'
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$expand = Join-Path $root 'Temp\V2NatureImport'
$target = Join-Path $root 'Assets\Nature'
if (Test-Path -LiteralPath $expand) {
    throw "Existing staging folder: $expand. To prevent data loss, move or remove it explicitly."
}
New-Item -ItemType Directory -Path $expand -Force | Out-Null
try {
    # System.IO.Compression validates archive format; reject suspicious traversal
    # entries explicitly before extraction.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('/', '\')
            if ($name.StartsWith('\') -or
                $name -match '(^|\\)\.\.($|\\)' -or
                $name -match '^[A-Za-z]:') {
                throw "Unsafe ZIP entry rejected: $name"
            }
        }
    }
    finally { $archive.Dispose() }
    Expand-Archive -LiteralPath $zip -DestinationPath $expand -Force

    $glbs = @(Get-ChildItem -LiteralPath $expand -Filter '*.glb' -File -Recurse)
    if ($glbs.Count -eq 0) {
        throw 'No self-contained .glb files found. Obtain the GLB version; separate .gltf/.bin packs need a dedicated importer.'
    }
    $tree = $glbs | Where-Object { $_.Name -like $TreePattern } | Sort-Object Length -Descending | Select-Object -First 1
    $rock = $glbs | Where-Object { $_.Name -like $RockPattern } | Sort-Object Length -Descending | Select-Object -First 1
    if (-not $tree -or -not $rock) {
        throw "Could not auto-select both Tree/Rock GLBs. $($glbs.Count) GLBs found; specify -TreePattern and -RockPattern."
    }

    New-Item -ItemType Directory -Path $target -Force | Out-Null
    # Explicitly replace only these two known imported preview assets.
    Copy-Item -LiteralPath $tree.FullName -Destination (Join-Path $target 'Tree.glb') -Force
    Copy-Item -LiteralPath $rock.FullName -Destination (Join-Path $target 'Rock.glb') -Force

    $manifest = @(
        '# V2 imported preview model provenance',
        '',
        'The user supplied an independently obtained asset archive; this script did not download it.',
        'Source ZIP: ' + $zip,
        'Source SHA256: ' + (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash,
        'Tree: ' + $tree.FullName.Substring($expand.Length),
        'Rock: ' + $rock.FullName.Substring($expand.Length),
        '',
        'VERIFY LICENSE before redistribution or commercial shipping.',
        'Supported suggested origin: Quaternius Stylized Nature MegaKit (CC0), https://quaternius.com/packs/stylizednaturemegakit.html',
        'Alternative: Kenney Nature Kit (CC0), https://kenney.nl/assets/nature-kit'
    )
    $manifest | Set-Content -Path (Join-Path $target 'SOURCE_NOTES.md') -Encoding UTF8

    Write-Host "Copied original model samples to $target"
    Write-Host 'Close game. From H:-configured environment, reimport before running:'
    Write-Host "  & '<H:-based Godot executable>' --headless --path '$root' --editor --import"
    Write-Host '  .\scripts\run-visual-lab.ps1'
}
finally {
    # Staging only: do not delete original ZIP or the user's source pack.
    if (Test-Path -LiteralPath $expand) {
        Remove-Item -LiteralPath $expand -Recurse -Force
    }
}
