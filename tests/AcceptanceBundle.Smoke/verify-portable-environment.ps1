#requires -Version 5.1
# Synthetic hosted-Windows regression coverage; this is not physical H: or GPU evidence.
# Every production script runs in a child process. Never borrow an existing H: drive.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:OS -ne 'Windows_NT') {
    throw 'This disposable portable-environment fixture must run only in hosted Windows GitHub Actions.'
}
if (@([IO.Directory]::GetLogicalDrives() | Where-Object { $_ -ieq 'H:\' }).Count -ne 0 -or
    (Get-PSDrive -Name H -ErrorAction SilentlyContinue)) {
    throw 'Portable-environment fixture requires an unused H: drive; an existing drive is never changed.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$commandShell = Join-Path $env:SystemRoot 'System32\cmd.exe'
$subst = Join-Path $env:SystemRoot 'System32\subst.exe'
$utf8 = New-Object Text.UTF8Encoding($false)
$fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('worldforge portable ' + [Guid]::NewGuid().ToString('N'))
$driveDirectory = Join-Path $fixtureDirectory 'drive'
$userDirectory = Join-Path $fixtureDirectory 'original user data'
$escapeDirectory = Join-Path $fixtureDirectory 'outside checkout'
$fixtureProject = 'H:\WorldForge Sim fixture'
$junctions = New-Object 'System.Collections.Generic.List[string]'
$mapped = $false

function Check($condition, [string]$name) {
    if (-not $condition) { throw "FAIL $name" }
    Write-Host "PASS $name"
}

function Get-EnvironmentSnapshot([EnvironmentVariableTarget]$Target) {
    $snapshot = [ordered]@{}
    foreach ($entry in @([Environment]::GetEnvironmentVariables($Target).GetEnumerator() | Sort-Object Key)) {
        $snapshot[[string]$entry.Key] = [string]$entry.Value
    }
    return ($snapshot | ConvertTo-Json -Compress)
}

# Do not traverse a junction even while collecting evidence about a failed guard.
function Get-FixtureTreeSnapshot([string]$Directory) {
    if (-not (Test-Path -LiteralPath $Directory)) { return '<missing>' }
    $entries = New-Object 'System.Collections.Generic.List[string]'
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $pending.Push($Directory)
    while ($pending.Count -gt 0) {
        $current = $pending.Pop()
        foreach ($item in @(Get-ChildItem -LiteralPath $current -Force)) {
            $relative = $item.FullName.Substring($Directory.TrimEnd('\').Length)
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                $entries.Add('LINK ' + $relative)
            } elseif ($item.PSIsContainer) {
                $entries.Add('DIR ' + $relative)
                $pending.Push($item.FullName)
            } else {
                $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
                $entries.Add('FILE ' + $relative + ' ' + $hash + ' ' + $item.LastWriteTimeUtc.Ticks)
            }
        }
    }
    return (@($entries | Sort-Object) -join "`n")
}

function Remove-FixtureJunctions {
    for ($i = $junctions.Count - 1; $i -ge 0; $i--) {
        # Directory.Delete without recursive deletion removes the link, never its target.
        [IO.Directory]::Delete($junctions[$i])
    }
    $junctions.Clear()
}

function New-ProjectFixture([string]$Directory = $fixtureProject) {
    Remove-FixtureJunctions
    if (Test-Path -LiteralPath $Directory) { Remove-Item -LiteralPath $Directory -Recurse -Force }
    [IO.Directory]::CreateDirectory((Join-Path $Directory 'scripts')) | Out-Null
    foreach ($name in @('portable-paths.ps1', 'enter-dev.ps1')) {
        Copy-Item -LiteralPath (Join-Path $root ('scripts\' + $name)) -Destination (Join-Path $Directory 'scripts')
    }
    [IO.File]::WriteAllText((Join-Path $Directory 'project.godot'), '; user-owned fixture configuration; do not rewrite', $utf8)
}

function New-FixtureJunction([string]$Path, [string]$Target = $escapeDirectory) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    New-Item -ItemType Junction -Path $Path -Target $Target | Out-Null
    $junctions.Add($Path)
}

function Invoke-FixtureProcess([string]$Executable, [string]$Arguments, [string]$Directory, [hashtable]$Environment) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Executable
    $start.Arguments = $Arguments
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($entry in $Environment.GetEnumerator()) { $start.EnvironmentVariables[$entry.Key] = [string]$entry.Value }
    $child = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $child.StandardOutput.ReadToEndAsync()
        $stderr = $child.StandardError.ReadToEndAsync()
        if (-not $child.WaitForExit(30000)) {
            try { & (Join-Path $env:SystemRoot 'System32\taskkill.exe') /PID $child.Id /T /F 2>&1 | Out-Null }
            catch { Write-Host ('Owned child-tree cleanup: ' + $_.Exception.Message) }
            try { if (-not $child.WaitForExit(5000)) { $child.Kill() } }
            catch { Write-Host ('Owned child cleanup: ' + $_.Exception.Message) }
            foreach ($capture in @(@{Name='stdout'; Task=$stdout}, @{Name='stderr'; Task=$stderr})) {
                try {
                    $partial = if ($capture.Task.Wait(5000)) { [string]$capture.Task.Result } else { '<pipe did not close>' }
                    if ($partial.Length -gt 8000) { $partial = $partial.Substring($partial.Length - 8000) }
                    Write-Host ("Timed-out child $($capture.Name):`n" + $partial)
                } catch { Write-Host ("Timed-out child $($capture.Name) drain: " + $_.Exception.Message) }
            }
            throw 'Portable-environment child timed out.'
        }
        if (-not $stdout.Wait(5000) -or -not $stderr.Wait(5000)) { throw 'Portable-environment child streams did not close.' }
        return [pscustomobject]@{ ExitCode = $child.ExitCode; Output = $stdout.Result; Error = $stderr.Result }
    } finally { $child.Dispose() }
}

$probePrelude = @'
[Console]::Out.WriteLine('PORTABLE_STAGE ' + [DateTime]::UtcNow.ToString('o') + ': PowerShell script execution began')
[Console]::Out.Flush()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Write-ProbeStage([string]$Stage) {
    [Console]::Out.WriteLine('PORTABLE_STAGE ' + [DateTime]::UtcNow.ToString('o') + ': ' + $Stage)
    [Console]::Out.Flush()
}
Write-ProbeStage 'before explicit built-in Management import'
Import-Module ([IO.Path]::Combine($PSHOME, 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')) -ErrorAction Stop
Write-ProbeStage 'after explicit built-in Management import'
Write-ProbeStage 'before explicit built-in Utility import'
Import-Module ([IO.Path]::Combine($PSHOME, 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')) -ErrorAction Stop
Write-ProbeStage 'after explicit built-in Utility import'
function Get-ProbeEnvironment {
    $state = [ordered]@{}
    foreach ($name in @('WORLDFORGE_HOME','TEMP','TMP','TMPDIR','NUGET_PACKAGES','NUGET_HTTP_CACHE_PATH',
        'NUGET_PLUGINS_CACHE_PATH','NUGET_SCRATCH','DOTNET_CLI_HOME','DOTNET_BUNDLE_EXTRACT_BASE_DIR',
        'APPDATA','LOCALAPPDATA','MSBuildUserExtensionsPath','DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_HOST_PATH',
        'DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH','DOTNET_CLI_TELEMETRY_OPTOUT',
        'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE','MSBUILDDISABLENODEREUSE','PSModuleAnalysisCachePath',
        'USERPROFILE','HOME','PATH')) {
        $state[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    return $state
}
function Write-ProbeResult($Value) {
    Write-ProbeStage 'before ConvertTo-Json'
    [Console]::Out.WriteLine('PORTABLE_PROBE:' + ($Value | ConvertTo-Json -Depth 5 -Compress))
    [Console]::Out.Flush()
    Write-ProbeStage 'JSON result emitted'
}
'@

function Read-ProbeResult($Result) {
    $lines = @($Result.Output -split '\r?\n' | Where-Object { $_.StartsWith('PORTABLE_PROBE:') })
    if ($lines.Count -ne 1) {
        throw "Expected one child probe result (exit $($Result.ExitCode)). $($Result.Error)"
    }
    return ($lines[0].Substring('PORTABLE_PROBE:'.Length) | ConvertFrom-Json)
}

function Invoke-EnvironmentProbe([string]$Code, [string]$Directory = $fixtureProject) {
    $childEnvironment = $hostileEnvironment.Clone()
    $childEnvironment['WORLDFORGE_PORTABLE_TEST_ROOT'] = $Directory
    # An already-running shell cannot undo its own startup writes. Disable module-cache
    # writes before these direct PowerShell probes; the real CMD bootstrap is tested below.
    $childEnvironment['PSModuleAnalysisCachePath'] = 'nul'
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($probePrelude + "`r`n" + $Code))
    $result = Invoke-FixtureProcess -Executable $powershell -Arguments ("-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + $encoded) `
        -Directory $Directory -Environment $childEnvironment
    return [pscustomobject]@{ ExitCode = $result.ExitCode; Data = (Read-ProbeResult $result) }
}

function Assert-ConfinedEnvironment($State, [string]$Project) {
    $expectedPaths = [ordered]@{
        WORLDFORGE_HOME = ''; TEMP = 'Temp'; TMP = 'Temp'; TMPDIR = 'Temp'
        NUGET_PACKAGES = 'Cache\NuGet'; NUGET_HTTP_CACHE_PATH = 'Cache\NuGetHttp'
        NUGET_PLUGINS_CACHE_PATH = 'Cache\NuGetPlugins'; NUGET_SCRATCH = 'Temp\NuGetScratch'
        DOTNET_CLI_HOME = 'Cache\DotNetHome'; DOTNET_BUNDLE_EXTRACT_BASE_DIR = 'Temp\DotNetBundle'
        APPDATA = 'Cache\AppData\Roaming'; LOCALAPPDATA = 'Cache\AppData\Local'
        MSBuildUserExtensionsPath = 'Cache\MSBuild'; DOTNET_ROOT = 'Tools\dotnet'; DOTNET_ROOT_X64 = 'Tools\dotnet'
        DOTNET_HOST_PATH = 'Tools\dotnet\dotnet.exe'
    }
    foreach ($entry in $expectedPaths.GetEnumerator()) {
        $expected = if ($entry.Value) { Join-Path $Project $entry.Value } else { $Project }
        $observed = [string]$State.PSObject.Properties[$entry.Key].Value
        if ($observed.TrimEnd('\') -ine $expected.TrimEnd('\')) { throw "FAIL child environment mapping: $($entry.Key)" }
        if ($entry.Key -ne 'DOTNET_HOST_PATH' -and -not (Test-Path -LiteralPath $expected -PathType Container)) {
            throw "FAIL child environment directory was not created: $($entry.Key)"
        }
    }
    $expectedFlags = @{
        DOTNET_GENERATE_ASPNET_CERTIFICATE = '0'; DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
        MSBUILDDISABLENODEREUSE = '1'; PSModuleAnalysisCachePath = 'nul'
    }
    foreach ($entry in $expectedFlags.GetEnumerator()) {
        if ([string]$State.PSObject.Properties[$entry.Key].Value -cne $entry.Value) { throw "FAIL child environment switch: $($entry.Key)" }
    }
    if ($State.USERPROFILE -cne $hostileEnvironment.USERPROFILE -or $State.HOME -cne $inheritedHomeValue) {
        throw 'FAIL activation changed USERPROFILE or HOME.'
    }
    $sdkDirectory = Join-Path $Project 'Tools\dotnet'
    $pathEntries = @($State.PATH -split ';')
    if ($pathEntries[0] -ine $sdkDirectory -or @($pathEntries | Where-Object { $_.TrimEnd('\') -ieq $sdkDirectory }).Count -ne 1) {
        throw 'FAIL portable SDK PATH precedence or idempotence.'
    }
}

$rejectedActivation = @'
$before = Get-ProbeEnvironment
$accepted = $false
$reason = ''
try {
    . (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\enter-dev.ps1')
    $accepted = $true
} catch { $reason = $_.Exception.Message }
Write-ProbeResult ([ordered]@{ accepted=$accepted; reason=$reason; before=$before; after=(Get-ProbeEnvironment) })
if (-not $accepted) { exit 41 }
'@

function Assert-RejectedActivation([string]$Name, [string]$Directory = $fixtureProject) {
    $beforeTree = Get-FixtureTreeSnapshot $Directory
    $beforeEscape = Get-FixtureTreeSnapshot $escapeDirectory
    $result = Invoke-EnvironmentProbe -Code $rejectedActivation -Directory $Directory
    Check ($result.ExitCode -eq 41 -and -not $result.Data.accepted -and $result.Data.reason) ($Name + ' rejects activation')
    Check (($result.Data.before | ConvertTo-Json -Compress) -ceq ($result.Data.after | ConvertTo-Json -Compress) -and
        (Get-FixtureTreeSnapshot $Directory) -ceq $beforeTree -and
        (Get-FixtureTreeSnapshot $escapeDirectory) -ceq $beforeEscape) ($Name + ' fails before environment or file writes')
}

$processEnvironmentBefore = Get-EnvironmentSnapshot Process
$userEnvironmentBefore = Get-EnvironmentSnapshot User
$machineEnvironmentBefore = Get-EnvironmentSnapshot Machine
$inheritedHomeValue = [Environment]::GetEnvironmentVariable('HOME', 'Process')
$locationBefore = (Get-Location).Path
try {
    [IO.Directory]::CreateDirectory($driveDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($escapeDirectory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $escapeDirectory 'untouched.txt'), 'outside target sentinel', $utf8)
    $hostileEnvironment = @{
        USERPROFILE = Join-Path $userDirectory 'profile'
        APPDATA = Join-Path $userDirectory 'Roaming'; LOCALAPPDATA = Join-Path $userDirectory 'Local'
        TEMP = Join-Path $userDirectory 'Temp'; TMP = Join-Path $userDirectory 'Tmp'; TMPDIR = Join-Path $userDirectory 'TmpDir'
        WORLDFORGE_HOME = Join-Path $userDirectory 'wrong project'
        NUGET_PACKAGES = Join-Path $userDirectory 'NuGet'; NUGET_HTTP_CACHE_PATH = Join-Path $userDirectory 'NuGetHttp'
        NUGET_PLUGINS_CACHE_PATH = Join-Path $userDirectory 'NuGetPlugins'; NUGET_SCRATCH = Join-Path $userDirectory 'NuGetScratch'
        DOTNET_CLI_HOME = Join-Path $userDirectory 'DotNetHome'; DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $userDirectory 'DotNetBundle'
        MSBuildUserExtensionsPath = Join-Path $userDirectory 'MSBuild'; DOTNET_ROOT = Join-Path $userDirectory 'dotnet'
        DOTNET_ROOT_X64 = Join-Path $userDirectory 'dotnet'; DOTNET_HOST_PATH = Join-Path $userDirectory 'dotnet\dotnet.exe'
        DOTNET_GENERATE_ASPNET_CERTIFICATE = '1'; DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '0'; DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'false'
        MSBUILDDISABLENODEREUSE = '0'; PSModuleAnalysisCachePath = Join-Path $userDirectory 'module-analysis-cache'
        PATH = (Join-Path $userDirectory 'dotnet') + ';' + $env:PATH
    }
    foreach ($name in @('USERPROFILE','APPDATA','LOCALAPPDATA','TEMP','TMP','TMPDIR','NUGET_PACKAGES',
        'NUGET_HTTP_CACHE_PATH','NUGET_PLUGINS_CACHE_PATH','NUGET_SCRATCH','DOTNET_CLI_HOME',
        'DOTNET_BUNDLE_EXTRACT_BASE_DIR','MSBuildUserExtensionsPath','DOTNET_ROOT')) {
        [IO.Directory]::CreateDirectory($hostileEnvironment[$name]) | Out-Null
        [IO.File]::WriteAllText((Join-Path $hostileEnvironment[$name] 'untouched.txt'), ('user sentinel ' + $name), $utf8)
    }
    $userConfig = Join-Path $hostileEnvironment.APPDATA 'NuGet\NuGet.Config'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $userConfig)) | Out-Null
    [IO.File]::WriteAllText($userConfig, '<configuration><!-- existing user configuration --></configuration>', $utf8)
    $userFilesBefore = Get-FixtureTreeSnapshot $userDirectory
    & $subst H: $driveDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the disposable H: mapping.' }
    $mapped = $true

    New-ProjectFixture
    $projectConfigBefore = Get-FileHash -LiteralPath (Join-Path $fixtureProject 'project.godot') -Algorithm SHA256
    $positive = Invoke-EnvironmentProbe -Code @'
Write-ProbeStage 'before entry-path Join-Path'
$entryScript = Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\enter-dev.ps1'
Write-ProbeStage 'after entry-path Join-Path'
Write-ProbeStage 'before first enter-dev'
. $entryScript
Write-ProbeStage 'after first enter-dev'
$first = Get-ProbeEnvironment
Write-ProbeStage 'before second enter-dev'
. $entryScript
Write-ProbeStage 'after second enter-dev'
Write-ProbeResult ([ordered]@{ first=$first; second=(Get-ProbeEnvironment) })
'@
    Assert-ConfinedEnvironment $positive.Data.first $fixtureProject
    Assert-ConfinedEnvironment $positive.Data.second $fixtureProject
    Check ($positive.ExitCode -eq 0 -and
        ($positive.Data.first | ConvertTo-Json -Compress) -ceq ($positive.Data.second | ConvertTo-Json -Compress)) 'actual child activation confines caches, NuGet profile, scratch and extraction, with idempotent PATH and no installed SDK'
    Check ((Get-FileHash -LiteralPath (Join-Path $fixtureProject 'project.godot') -Algorithm SHA256).Hash -ceq $projectConfigBefore.Hash) 'activation preserves existing project configuration bytes'

    $localConfig = Join-Path $fixtureProject 'Cache\AppData\Roaming\NuGet\NuGet.Config'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $localConfig)) | Out-Null
    [IO.File]::WriteAllText($localConfig, '<configuration><!-- existing project-local settings --></configuration>', $utf8)
    $localConfigBefore = Get-FixtureTreeSnapshot (Split-Path -Parent $localConfig)
    $preserve = Invoke-EnvironmentProbe -Code @'
. (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\enter-dev.ps1')
Write-ProbeResult (Get-ProbeEnvironment)
'@
    Check ($preserve.ExitCode -eq 0 -and (Get-FixtureTreeSnapshot (Split-Path -Parent $localConfig)) -ceq $localConfigBefore) 'existing portable NuGet configuration is not overwritten'

    $driveRoot = Invoke-EnvironmentProbe -Code @'
. (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\portable-paths.ps1')
$output = @(Initialize-WorldForgePortableEnvironment -Root 'H:\')
$missing = @(Assert-WorldForgePortablePath -Root 'H:\' -Path 'H:\not-created\nested\directory' -Kind Directory -AllowMissing)
Write-ProbeResult ([ordered]@{ output_count=$output.Count+$missing.Count; after=(Get-ProbeEnvironment) })
'@
    Assert-ConfinedEnvironment $driveRoot.Data.after 'H:\'
    Check ($driveRoot.ExitCode -eq 0 -and $driveRoot.Data.output_count -eq 0 -and
        -not (Test-Path -LiteralPath 'H:\not-created')) 'drive-root checkout and missing ancestors validate without pipeline output or probe writes'

    $offDrive = Join-Path $fixtureDirectory 'not on H'
    New-ProjectFixture $offDrive
    Assert-RejectedActivation 'non-H checkout' $offDrive
    foreach ($path in @('Temp', 'Cache\NuGet')) {
        New-ProjectFixture
        $blocker = Join-Path $fixtureProject $path
        [IO.Directory]::CreateDirectory((Split-Path -Parent $blocker)) | Out-Null
        [IO.File]::WriteAllText($blocker, 'existing file must not be replaced', $utf8)
        Assert-RejectedActivation ('directory occupied by a file: ' + $path)
    }
    foreach ($path in @('Tools\dotnet\dotnet.exe', 'Cache\AppData\Roaming\NuGet\NuGet.Config')) {
        New-ProjectFixture
        [IO.Directory]::CreateDirectory((Join-Path $fixtureProject $path)) | Out-Null
        Assert-RejectedActivation ('file occupied by a directory: ' + $path)
    }
    foreach ($path in @('Cache','Temp','Tools','artifacts','.godot','Cache\NuGet','Cache\DotNetHome',
        'Cache\MSBuild','Cache\AppData\Roaming\NuGet','Cache\AppData\Local\NuGet\Migrations',
        'Temp\NuGetScratch','Temp\DotNetBundle','Tools\dotnet','Tools\Godot')) {
        New-ProjectFixture
        New-FixtureJunction (Join-Path $fixtureProject $path)
        Assert-RejectedActivation ('junction at ' + $path)
    }

    New-ProjectFixture
    $rootTarget = Join-Path $escapeDirectory 'root target'
    New-ProjectFixture $rootTarget
    $linkedRoot = 'H:\linked checkout'
    New-FixtureJunction $linkedRoot $rootTarget
    $rootTargetBefore = Get-FixtureTreeSnapshot $rootTarget
    $rootRejection = Invoke-EnvironmentProbe -Code $rejectedActivation -Directory $linkedRoot
    Check ($rootRejection.ExitCode -eq 41 -and -not $rootRejection.Data.accepted -and
        (Get-FixtureTreeSnapshot $rootTarget) -ceq $rootTargetBefore) 'checkout root junction is rejected without writing its target'
    Remove-FixtureJunctions
    $ancestorTarget = Join-Path $escapeDirectory 'ancestor target'
    New-ProjectFixture (Join-Path $ancestorTarget 'project')
    New-FixtureJunction 'H:\linked parent' $ancestorTarget
    $ancestorBefore = Get-FixtureTreeSnapshot $ancestorTarget
    $ancestorRejection = Invoke-EnvironmentProbe -Code $rejectedActivation -Directory 'H:\linked parent\project'
    Check ($ancestorRejection.ExitCode -eq 41 -and -not $ancestorRejection.Data.accepted -and
        (Get-FixtureTreeSnapshot $ancestorTarget) -ceq $ancestorBefore) 'checkout ancestor junction is rejected before writes'

    New-ProjectFixture
    $containment = Invoke-EnvironmentProbe -Code @'
. (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\portable-paths.ps1')
$rejected = 0
foreach ($path in @(($env:WORLDFORGE_PORTABLE_TEST_ROOT+'-sibling\child'),
    (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT '..\outside-project\child'))) {
    try { Assert-WorldForgePortablePath -Root $env:WORLDFORGE_PORTABLE_TEST_ROOT -Path $path -Kind Directory -AllowMissing }
    catch { $rejected++ }
}
Write-ProbeResult ([ordered]@{ rejected=$rejected })
'@
    Check ($containment.ExitCode -eq 0 -and $containment.Data.rejected -eq 2) 'sibling prefix and dot-dot escape cannot masquerade as project paths'

    $nestedTools = Join-Path $fixtureProject 'Tools\dotnet\sdk\fixture-version\payload\nested'
    [IO.Directory]::CreateDirectory($nestedTools) | Out-Null
    [IO.File]::WriteAllText((Join-Path $nestedTools 'existing-tool.txt'), 'preserve existing nested tool content', $utf8)
    $treeProbe = @'
. (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\portable-paths.ps1')
$accepted = $false
$reason = ''
$outputCount = 0
try {
    $directory = Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'Tools'
    $output = @(Assert-WorldForgePortableTree -Root $env:WORLDFORGE_PORTABLE_TEST_ROOT -Directory $directory)
    $outputCount = $output.Count
    $accepted = $true
} catch { $reason = $_.Exception.Message }
Write-ProbeResult ([ordered]@{ accepted=$accepted; reason=$reason; output_count=$outputCount })
if (-not $accepted) { exit 41 }
'@
    $ordinaryTreeBefore = Get-FixtureTreeSnapshot $fixtureProject
    $ordinaryTree = Invoke-EnvironmentProbe -Code $treeProbe
    Check ($ordinaryTree.ExitCode -eq 0 -and $ordinaryTree.Data.accepted -and $ordinaryTree.Data.output_count -eq 0 -and
        (Get-FixtureTreeSnapshot $fixtureProject) -ceq $ordinaryTreeBefore) 'installer tree guard silently accepts ordinary nested tool files without writes'
    New-FixtureJunction (Join-Path $nestedTools 'arbitrary-archive-folder')
    $linkedTreeBefore = Get-FixtureTreeSnapshot $fixtureProject
    $linkedTargetBefore = Get-FixtureTreeSnapshot $escapeDirectory
    $linkedTree = Invoke-EnvironmentProbe -Code $treeProbe
    Check ($linkedTree.ExitCode -eq 41 -and -not $linkedTree.Data.accepted -and
        $linkedTree.Data.reason -match 'junction|symbolic link' -and
        (Get-FixtureTreeSnapshot $fixtureProject) -ceq $linkedTreeBefore -and
        (Get-FixtureTreeSnapshot $escapeDirectory) -ceq $linkedTargetBefore) 'installer tree guard rejects arbitrary nested junction before modifying tool tree or target'
    Remove-FixtureJunctions

    $editorDirectory = Join-Path $fixtureProject 'Tools\Godot\Godot_v4.7.2-stable_mono_win64'
    [IO.Directory]::CreateDirectory($editorDirectory) | Out-Null
    $editor = Join-Path $editorDirectory 'Godot_v4.7.2-stable_mono_win64.exe'
    $marker = Join-Path $editorDirectory '_sc_'
    [IO.File]::WriteAllText($editor, 'validation-only placeholder; never executed', $utf8)
    [IO.File]::WriteAllText($marker, '', $utf8)
    $godotProbe = @'
. (Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'scripts\portable-paths.ps1')
$accepted = $false
$reason = ''
$outputCount = 0
try {
    $executable = Join-Path $env:WORLDFORGE_PORTABLE_TEST_ROOT 'Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
    $output = @(Assert-WorldForgePortableGodot -Root $env:WORLDFORGE_PORTABLE_TEST_ROOT -Executable $executable)
    $outputCount = $output.Count
    $accepted = $true
} catch { $reason = $_.Exception.Message }
Write-ProbeResult ([ordered]@{ accepted=$accepted; reason=$reason; output_count=$outputCount })
if (-not $accepted) { exit 41 }
'@
    $validGodot = Invoke-EnvironmentProbe -Code $godotProbe
    Check ($validGodot.ExitCode -eq 0 -and $validGodot.Data.accepted -and $validGodot.Data.output_count -eq 0 -and
        -not (Test-Path -LiteralPath (Join-Path $editorDirectory 'editor_data'))) 'portable Godot accepts real executable/marker leaves without launching or creating editor data'
    foreach ($case in @('missing marker','marker is a directory','editor data junction','executable is a directory')) {
        Remove-FixtureJunctions
        if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker -Recurse -Force }
        if (Test-Path -LiteralPath $editor) { Remove-Item -LiteralPath $editor -Recurse -Force }
        [IO.File]::WriteAllText($editor, 'validation-only placeholder; never executed', $utf8)
        if ($case -eq 'marker is a directory') { [IO.Directory]::CreateDirectory($marker) | Out-Null }
        elseif ($case -ne 'missing marker') { [IO.File]::WriteAllText($marker, '', $utf8) }
        if ($case -eq 'editor data junction') { New-FixtureJunction (Join-Path $editorDirectory 'editor_data') }
        if ($case -eq 'executable is a directory') {
            Remove-Item -LiteralPath $editor
            [IO.Directory]::CreateDirectory($editor) | Out-Null
        }
        $beforeGuard = Get-FixtureTreeSnapshot $fixtureProject
        $beforeOutside = Get-FixtureTreeSnapshot $escapeDirectory
        $invalidGodot = Invoke-EnvironmentProbe -Code $godotProbe
        Check ($invalidGodot.ExitCode -eq 41 -and -not $invalidGodot.Data.accepted -and $invalidGodot.Data.reason -and
            (Get-FixtureTreeSnapshot $fixtureProject) -ceq $beforeGuard -and
            (Get-FixtureTreeSnapshot $escapeDirectory) -ceq $beforeOutside) ('portable Godot fails closed before any writes: ' + $case)
    }

    New-ProjectFixture
    Copy-Item -LiteralPath (Join-Path $root 'RUN-TEST.cmd') -Destination $fixtureProject
    $launcherStub = $probePrelude + @'

$bootstrap = Get-ProbeEnvironment
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
Write-ProbeResult ([ordered]@{ bootstrap=$bootstrap; after=(Get-ProbeEnvironment) })
exit 7
'@
    [IO.File]::WriteAllText((Join-Path $fixtureProject 'scripts\run-self-test.ps1'), $launcherStub, $utf8)
    $launched = Invoke-FixtureProcess -Executable $commandShell -Arguments '/d /c RUN-TEST.cmd <nul' `
        -Directory $fixtureProject -Environment $hostileEnvironment
    $launchData = Read-ProbeResult $launched
    Assert-ConfinedEnvironment $launchData.after $fixtureProject
    Check ($launched.ExitCode -eq 7 -and $launchData.bootstrap.PSModuleAnalysisCachePath -ceq 'nul' -and
        $launchData.bootstrap.TEMP.TrimEnd('\') -ieq $fixtureProject -and
        $launchData.bootstrap.TMP.TrimEnd('\') -ieq $fixtureProject -and
        $launchData.bootstrap.TMPDIR.TrimEnd('\') -ieq $fixtureProject) 'real CMD initializes safe PowerShell startup paths, handles spaces and preserves the child exit code'
    Copy-Item -LiteralPath (Join-Path $root 'RUN-TEST.cmd') -Destination $offDrive
    [IO.File]::WriteAllText((Join-Path $offDrive 'scripts\run-self-test.ps1'), $launcherStub, $utf8)
    $offDriveBefore = Get-FixtureTreeSnapshot $offDrive
    $refusedLauncher = Invoke-FixtureProcess -Executable $commandShell -Arguments '/d /c RUN-TEST.cmd <nul' `
        -Directory $offDrive -Environment $hostileEnvironment
    Check ($refusedLauncher.ExitCode -ne 0 -and -not $refusedLauncher.Output.Contains('PORTABLE_PROBE:') -and
        (Get-FixtureTreeSnapshot $offDrive) -ceq $offDriveBefore) 'non-H CMD entry rejects before launching its PowerShell script or writing project data'

    $userFilesAfter = Get-FixtureTreeSnapshot $userDirectory
    if ($userFilesAfter -cne $userFilesBefore) {
        foreach ($difference in @(Compare-Object ($userFilesBefore -split "`n") ($userFilesAfter -split "`n"))) {
            Write-Host ('Inherited user fixture change ' + $difference.SideIndicator + ': ' + $difference.InputObject)
        }
    }
    Check ($userFilesAfter -ceq $userFilesBefore) 'all subprocess probes preserve inherited user NuGet configuration, profiles and cache files'
    Check ((Get-EnvironmentSnapshot Process) -ceq $processEnvironmentBefore -and
        (Get-EnvironmentSnapshot User) -ceq $userEnvironmentBefore -and
        (Get-EnvironmentSnapshot Machine) -ceq $machineEnvironmentBefore -and
        (Get-Location).Path -ceq $locationBefore) 'parent process environment, persistent user/machine environment and working directory are unchanged'
    Write-Host 'PASS ALL Windows portable environment, fail-closed paths, marker validation, child isolation and CMD bootstrap tests'
} finally {
    # Unlink junctions before any recursive fixture cleanup, including on failed assertions.
    Remove-FixtureJunctions
    if ($mapped) {
        & $subst H: /D | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not remove the owned disposable H: mapping.' }
    }
    if (Test-Path -LiteralPath $fixtureDirectory) { Remove-Item -LiteralPath $fixtureDirectory -Recurse -Force }
}
