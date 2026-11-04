#requires -Version 5.1
# Dot-source this script: . .\scripts\enter-dev.ps1
# Applies to the CURRENT PowerShell process and its children only.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'portable-paths.ps1')
Initialize-WorldForgePortableEnvironment -Root $project
if (-not (Test-Path -LiteralPath (Join-Path $env:DOTNET_ROOT 'dotnet.exe') -PathType Leaf)) {
    Write-Warning 'H:-based .NET SDK not installed. Run scripts/install-dotnet.ps1 before building.'
}
Write-Host "WorldForge dev environment active: $project"
Write-Host "Project temp: $env:TEMP"
Write-Host "NuGet packages: $env:NUGET_PACKAGES"
Write-Host "Tool application data: $env:APPDATA"
Write-Host 'Godot launchers require a real _sc_ file beside the H:-based editor.'
