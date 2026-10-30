# Dot-source this script: . .\scripts\enter-dev.ps1
# Applies to the CURRENT PowerShell process only.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $project.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "WorldForge-Sim must live on H:, got: $project"
}
$env:WORLDFORGE_HOME = $project
$cache = Join-Path $project 'Cache'
$temp = Join-Path $project 'Temp'
foreach ($path in @($cache, $temp, (Join-Path $cache 'NuGet'), (Join-Path $cache 'NuGetHttp'), (Join-Path $cache 'NuGetPlugins'), (Join-Path $cache 'DotNetHome'))) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}
$env:TEMP = $temp
$env:TMP = $temp
$env:TMPDIR = $temp
$env:NUGET_PACKAGES = Join-Path $cache 'NuGet'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $cache 'NuGetHttp'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $cache 'NuGetPlugins'
$env:DOTNET_CLI_HOME = Join-Path $cache 'DotNetHome'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
Write-Host "WorldForge dev environment active: $project"
Write-Host "Project temp: $env:TEMP"
Write-Host "NuGet packages: $env:NUGET_PACKAGES"
Write-Host "Godot must be self-contained: create _sc_ beside H:-based editor EXE before launching."
