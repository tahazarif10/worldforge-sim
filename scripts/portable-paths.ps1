#requires -Version 5.1
# Process-scoped portable Windows paths. This is not an OS/driver I/O sandbox.
# Definitions only: callers must initialize before tool/build/evidence writes.

function Assert-WorldForgePortablePath {
    param(
        [Parameter(Mandatory=$true)][string]$Root,
        [Parameter(Mandatory=$true)][string]$Path,
        [ValidateSet('Directory','File')][string]$Kind = 'Directory',
        [switch]$AllowMissing
    )
    if ($env:OS -ne 'Windows_NT' -or -not $Root.Replace('/','\').StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "WorldForge portable paths require an H: Windows checkout: $Root"
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $full = [IO.Path]::GetFullPath($Path)
    if (-not ($full.TrimEnd('\').Equals($base, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($base + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "Portable path leaves the project: $Path"
    }
    $cursor = $full
    while ($cursor) {
        $exists = $true
        try { $attributes = [IO.File]::GetAttributes($cursor) }
        catch [IO.FileNotFoundException] { $exists = $false }
        catch [IO.DirectoryNotFoundException] { $exists = $false }
        if ($exists) {
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Portable path must not use a junction or symbolic link: $cursor"
            }
            $isDirectory = ($attributes -band [IO.FileAttributes]::Directory) -ne 0
            $expectedDirectory = ($cursor -ne $full -or $Kind -eq 'Directory')
            if ($isDirectory -ne $expectedDirectory) { throw "Wrong file/directory kind for portable path: $cursor" }
        } elseif ($cursor -eq $full -and -not $AllowMissing) {
            throw "Required portable path is missing: $full"
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
}

function Assert-WorldForgePortableTree {
    param([Parameter(Mandatory=$true)][string]$Root, [Parameter(Mandatory=$true)][string]$Directory)
    Assert-WorldForgePortablePath -Root $Root -Path $Directory -Kind Directory -AllowMissing
    if (-not [IO.Directory]::Exists($Directory)) { return }
    # Installers may overwrite many existing descendants. Validate those without
    # following links before extraction/install, not after their first write.
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($Directory)
    while ($pending.Count -gt 0) {
        $currentDirectory = $pending.Pop()
        foreach ($item in @(Get-ChildItem -LiteralPath $currentDirectory -Force -ErrorAction Stop)) {
            $kind = if ($item.PSIsContainer) { 'Directory' } else { 'File' }
            Assert-WorldForgePortablePath -Root $Root -Path $item.FullName -Kind $kind
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
        }
    }
}

function Assert-WorldForgePortableGodot {
    param([Parameter(Mandatory=$true)][string]$Root, [Parameter(Mandatory=$true)][string]$Executable)
    Assert-WorldForgePortablePath -Root $Root -Path $Executable -Kind File
    $directory = Split-Path -Parent $Executable
    Assert-WorldForgePortablePath -Root $Root -Path (Join-Path $directory '_sc_') -Kind File
    Assert-WorldForgePortablePath -Root $Root -Path (Join-Path $directory 'editor_data') -Kind Directory -AllowMissing
}

function Initialize-WorldForgePortableEnvironment {
    param([Parameter(Mandatory=$true)][string]$Root)
    Assert-WorldForgePortablePath -Root $Root -Path $Root -Kind Directory
    $projectPath = [IO.Path]::GetFullPath($Root)
    $paths = [ordered]@{
        WORLDFORGE_HOME = $projectPath
        TEMP = Join-Path $projectPath 'Temp'
        TMP = Join-Path $projectPath 'Temp'
        TMPDIR = Join-Path $projectPath 'Temp'
        NUGET_PACKAGES = Join-Path $projectPath 'Cache\NuGet'
        NUGET_HTTP_CACHE_PATH = Join-Path $projectPath 'Cache\NuGetHttp'
        NUGET_PLUGINS_CACHE_PATH = Join-Path $projectPath 'Cache\NuGetPlugins'
        NUGET_SCRATCH = Join-Path $projectPath 'Temp\NuGetScratch'
        DOTNET_CLI_HOME = Join-Path $projectPath 'Cache\DotNetHome'
        DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $projectPath 'Temp\DotNetBundle'
        APPDATA = Join-Path $projectPath 'Cache\AppData\Roaming'
        LOCALAPPDATA = Join-Path $projectPath 'Cache\AppData\Local'
        MSBuildUserExtensionsPath = Join-Path $projectPath 'Cache\MSBuild'
        DOTNET_ROOT = Join-Path $projectPath 'Tools\dotnet'
        DOTNET_ROOT_X64 = Join-Path $projectPath 'Tools\dotnet'
    }
    # Validate the entire plan before creating directories or changing environment.
    # Existing descendant paths are checked as each configured destination is used.
    $directories = @($paths.Values) + @(
        (Join-Path $projectPath 'Cache\Downloads'),
        (Join-Path $projectPath 'Tools\Godot'),
        (Join-Path $projectPath 'artifacts'),
        (Join-Path $projectPath '.godot'),
        (Join-Path $paths.APPDATA 'NuGet'),
        (Join-Path $paths.LOCALAPPDATA 'NuGet\Migrations'))
    foreach ($directory in $directories) {
        Assert-WorldForgePortablePath -Root $projectPath -Path $directory -Kind Directory -AllowMissing
    }
    $sdk = Join-Path $paths.DOTNET_ROOT 'dotnet.exe'
    Assert-WorldForgePortablePath -Root $projectPath -Path $sdk -Kind File -AllowMissing
    Assert-WorldForgePortablePath -Root $projectPath -Path (Join-Path $paths.APPDATA 'NuGet\NuGet.Config') -Kind File -AllowMissing
    foreach ($directory in @($paths.Values | Select-Object -Unique)) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    foreach ($entry in $paths.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    # Current supported switches; SKIP_FIRST_TIME_EXPERIENCE has been obsolete since .NET 3.
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = '0'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:DOTNET_HOST_PATH = $sdk
    # Windows PowerShell reads this at startup. RUN-TEST.cmd sets it before launching;
    # in an already-open shell this setting only controls subsequent children.
    $env:PSModuleAnalysisCachePath = 'nul'
    $sdkDirectory = $paths.DOTNET_ROOT
    $otherPaths = @($env:PATH -split ';' | Where-Object { $_ -and -not $_.TrimEnd('\').Equals($sdkDirectory.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) })
    $env:PATH = (@($sdkDirectory) + $otherPaths) -join ';'
}
