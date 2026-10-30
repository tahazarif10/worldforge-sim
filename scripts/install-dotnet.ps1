# Install .NET 10 SDK to this H:-based project only. No machine-wide installer or PATH changes.
# Run from H:\WorldForge-Sim: .\scripts\install-dotnet.ps1
# Official installer source: https://dot.net/v1/dotnet-install.ps1
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing install outside H: ($root)"
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Requires 64-bit Windows' }
$tools = Join-Path $root 'Tools'
$sdkDir = Join-Path $tools 'dotnet'
$downloads = Join-Path $root 'Cache\Downloads'
New-Item -ItemType Directory -Force -Path $tools,$sdkDir,$downloads | Out-Null
$installer = Join-Path $downloads 'dotnet-install.ps1'
$archive = Join-Path $downloads 'dotnet-sdk-10.0.403-win-x64.zip'
$source = 'https://dot.net/v1/dotnet-install.ps1'
Write-Host "Downloading official Microsoft installation script to: $installer"
Invoke-WebRequest -UseBasicParsing -Uri $source -OutFile $installer
Write-Host "Installing SDK 10.0.403 to: $sdkDir"
Write-Host "SDK download archive (H:): $archive"
# Isolate the Microsoft script's exit behavior in a child shell.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Version '10.0.403' -Architecture 'x64' -InstallDir $sdkDir -NoPath -KeepZip -ZipPath $archive
if ($LASTEXITCODE -ne 0) { throw "Microsoft installer exited with code $LASTEXITCODE" }
$exe = Join-Path $sdkDir 'dotnet.exe'
if (-not (Test-Path $exe)) { throw "Expected H:-based SDK not found: $exe" }
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$resolved = (Get-Command dotnet -ErrorAction Stop).Source
if (-not $resolved.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Blocked: dotnet still resolves outside H:: $resolved"
}
Write-Host "Verified SDK binary: $resolved"
& $exe --list-sdks
