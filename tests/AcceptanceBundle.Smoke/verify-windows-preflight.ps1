#requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$Sdk,
    [switch]$PortableChild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:OS -ne 'Windows_NT') {
    throw 'This hosted Windows integration fixture must run only in GitHub Actions.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-bundle.ps1')

# The hosted checkout is exposed through a disposable H: alias so actual tools
# exercise the production environment initializer. This proves configured path
# behavior, not a physical user H: drive or the absence of OS/driver C: writes.
if (-not $PortableChild) {
    if ($null -ne (Get-PSDrive -Name H -ErrorAction SilentlyContinue) -or
        [Environment]::GetLogicalDrives() -contains 'H:\') {
        throw 'Hosted portable preflight requires an unused H: drive.'
    }
    $checkoutPath = [IO.Path]::GetFullPath($root).TrimEnd('\')
    $driveTarget = [IO.Directory]::GetParent($checkoutPath)
    if ($null -eq $driveTarget) { throw 'Hosted preflight needs a named checkout directory.' }
    # Match the real H:\WorldForge-Sim layout instead of making the project a
    # drive root. The drive-root fixture returned empty Godot resource scans.
    $portableCheckout = [IO.Path]::Combine('H:\', [IO.Path]::GetFileName($checkoutPath))
    $prefix = $checkoutPath + '\'
    function ConvertTo-HostedHPath([string]$Path) {
        $full = [IO.Path]::GetFullPath($Path)
        if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.File]::Exists($full)) {
            throw "Hosted preflight tool must be a file under the checkout: $Path"
        }
        # H: deliberately does not exist until subst below; Join-Path resolves
        # PowerShell drives even for composition, so use the filesystem API.
        return [IO.Path]::Combine($portableCheckout, $full.Substring($prefix.Length))
    }
    $childSdk = ConvertTo-HostedHPath $Sdk
    $childGodot = ConvertTo-HostedHPath $Executable
    $profileAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    if (-not $profileAppData) { throw 'Cannot identify the original Windows user NuGet configuration path.' }
    $profileConfig = Join-Path $profileAppData 'NuGet\NuGet.Config'
    $profileExisted = [IO.File]::Exists($profileConfig)
    [byte[]]$profileBytes = @()
    if ($profileExisted) { $profileBytes = [IO.File]::ReadAllBytes($profileConfig) }
    $bootstrap = [ordered]@{
        TEMP=$portableCheckout; TMP=$portableCheckout; TMPDIR=$portableCheckout; PSModuleAnalysisCachePath='nul'
    }
    $savedEnvironment = @{}
    foreach ($name in $bootstrap.Keys) {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    $mapped = $false
    $primaryFailure = $null
    $cleanupFailures = @()
    $childOutput = $null
    try {
        & (Join-Path $env:SystemRoot 'System32\subst.exe') H: $driveTarget.FullName
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the disposable hosted H: drive.' }
        $mapped = $true
        # These values must precede fresh PowerShell startup. enter-dev will
        # replace the existing H: checkout temp with project subdirectories later.
        foreach ($entry in $bootstrap.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
        $childOutput = Join-Path (Join-Path $portableCheckout 'artifacts\acceptance') ('portable-preflight-parent-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $childOutput -Force | Out-Null
        Invoke-AcceptanceProcess -Executable (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Arguments @(
            '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',
            (Join-Path $portableCheckout 'tests\AcceptanceBundle.Smoke\verify-windows-preflight.ps1'),
            '-Executable',$childGodot,'-Sdk',$childSdk,'-PortableChild') `
            -WorkingDirectory $portableCheckout -OutputDirectory $childOutput -TimeoutSeconds 720
    }
    catch { $primaryFailure = $_ }
    finally {
        if ($childOutput) {
            foreach ($name in @('stdout.log','stderr.log')) {
                try {
                    $log = Join-Path $childOutput $name
                    if (Test-Path -LiteralPath $log) {
                        # Keep initial environment/result headers as well as the
                        # ending native diagnostics; a tail alone can hide cause.
                        $preview = @(Get-Content -LiteralPath $log -TotalCount 201)
                        Write-Host "Child $name (bounded head/tail):"
                        if ($preview.Count -le 200) { $preview }
                        else {
                            $preview | Select-Object -First 80
                            Write-Host '[Middle omitted; complete child log is retained in the failure artifact.]'
                            Get-Content -LiteralPath $log -Tail 120
                        }
                    }
                } catch { Write-Warning "Could not display child $name; original evidence remains in the checkout." }
            }
        }
        # The child may return a generic bundle exception after a long report.
        # Read its separately persisted primary failure before unmapping H:.
        try {
            $artifactRoot = Join-Path $root 'artifacts\acceptance'
            if (Test-Path -LiteralPath $artifactRoot -PathType Container) {
                $bundles = @(Get-ChildItem -LiteralPath $artifactRoot -Directory -Filter 'windows-preflight-*' |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 3)
                foreach ($bundle in $bundles) {
                    $statusPath = Join-Path $bundle.FullName 'status.json'
                    if (Test-Path -LiteralPath $statusPath -PathType Leaf) {
                        $saved = Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json
                        Write-Host ("Saved child bundle {0}: {1}" -f $bundle.Name, $saved.status)
                        if ($saved.failure) { Write-Host ("Primary bundle failure: {0}" -f $saved.failure) }
                    }
                    $warningsPath = Join-Path $bundle.FullName 'collection-warnings.txt'
                    if (Test-Path -LiteralPath $warningsPath -PathType Leaf) {
                        Get-Content -LiteralPath $warningsPath -TotalCount 20
                    }
                }
            }
        } catch { Write-Warning ("Could not display saved child status: {0}" -f $_.Exception.Message) }
        foreach ($name in $bootstrap.Keys) {
            try { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
            catch { $cleanupFailures += "Could not restore parent environment variable: $name" }
        }
        try {
            if ([IO.File]::Exists($profileConfig) -ne $profileExisted -or
                ($profileExisted -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($profileConfig)) -cne
                    [Convert]::ToBase64String($profileBytes))) {
                throw 'Native work changed the original Windows user NuGet.Config presence or bytes.'
            }
        } catch { $cleanupFailures += $_.Exception.Message }
        if ($mapped) {
            try {
                & (Join-Path $env:SystemRoot 'System32\subst.exe') H: /D | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Could not remove the disposable hosted H: drive.' }
            } catch { $cleanupFailures += $_.Exception.Message }
        }
    }
    if ($null -ne $primaryFailure) {
        foreach ($problem in $cleanupFailures) { Write-Warning $problem }
        throw $primaryFailure
    }
    if ($cleanupFailures.Count -ne 0) { throw ($cleanupFailures -join '; ') }
    Write-Host 'PASS fresh Windows PowerShell portable tool execution; original user NuGet.Config presence/bytes preserved, parent environment restored and H: unmapped'
    return
}

if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase) -or
    $root.TrimEnd('\').Equals('H:', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Portable child must execute from a named directory on the disposable H: drive.'
}
foreach ($name in @('TEMP','TMP','TMPDIR')) {
    if ([Environment]::GetEnvironmentVariable($name, 'Process') -ine $root) {
        throw "Fresh PowerShell did not receive the H: checkout bootstrap: $name"
    }
}
if ($env:PSModuleAnalysisCachePath -ine 'nul') { throw 'PowerShell module-cache bootstrap was not applied before child launch.' }
. (Join-Path $root 'scripts/enter-dev.ps1')
Assert-WorldForgePortablePath -Root $root -Path $Sdk -Kind File
Assert-WorldForgePortableGodot -Root $root -Executable $Executable
$expectedSdk = Join-Path $root 'Tools\dotnet\dotnet.exe'
$godotPrefix = (Join-Path $root 'Tools\Godot').TrimEnd('\') + '\'
$selectedSdk = (Get-Command dotnet.exe -CommandType Application | Select-Object -First 1).Source
if (-not [IO.Path]::GetFullPath($Sdk).Equals($expectedSdk, [StringComparison]::OrdinalIgnoreCase) -or
    -not $selectedSdk.Equals($expectedSdk, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFullPath($Executable).StartsWith($godotPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Actual SDK/Godot selection did not stay in the portable checkout tool directories.'
}

# Keep this extra native probe outside the original bundle: its 12 import/test/
# build/boot stage identities and closed-log checks below remain unchanged.
$environmentOutput = Join-Path $root ('artifacts/acceptance/portable-nuget-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $environmentOutput -Force | Out-Null
Invoke-AcceptanceProcess -Executable $Sdk -Arguments @('nuget','locals','all','--list','--force-english-output') `
    -WorkingDirectory $root -OutputDirectory $environmentOutput -TimeoutSeconds 60
$expectedCaches = [ordered]@{
    'http-cache' = $env:NUGET_HTTP_CACHE_PATH
    'global-packages' = $env:NUGET_PACKAGES
    'temp' = $env:NUGET_SCRATCH
    'plugins-cache' = $env:NUGET_PLUGINS_CACHE_PATH
}
$observedCaches = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $environmentOutput 'stdout.log')) {
    if ($line -match '^(http-cache|global-packages|temp|plugins-cache):\s*(.+)$') {
        $name = $Matches[1]
        $path = $Matches[2].Trim()
        if ($observedCaches.ContainsKey($name)) { throw "Duplicate NuGet cache location: $name" }
        Assert-WorldForgePortablePath -Root $root -Path $path -Kind Directory -AllowMissing
        if (-not [IO.Path]::GetFullPath($path).TrimEnd('\').Equals(
            [IO.Path]::GetFullPath($expectedCaches[$name]).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
            throw "Actual NuGet $name path differs from the initialized portable environment: $path"
        }
        $observedCaches[$name] = $path
        Write-Host "PASS actual NuGet $name location: $path"
    }
}
if ($observedCaches.Count -ne 4) { throw 'dotnet nuget locals did not report all four portable cache locations.' }

. (Join-Path $root 'scripts/acceptance-preflight.ps1')
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
. (Join-Path $root 'scripts/self-test-report.ps1')
$before = Get-AcceptanceWorktreeState -Root $root
$directory = Join-Path $root ('artifacts/acceptance/windows-preflight-' + [Guid]::NewGuid().ToString('N'))
$result = Invoke-SelfTestBundle -Directory $directory -NoClipboard -Action {
    param($output)
    Invoke-AcceptancePreflight -Root $root -Executable $Executable -Sdk $Sdk -Directory (Join-Path $output 'preflight')
    Initialize-AcceptanceRuntime -Executable $Executable -Root $root -Directory (Join-Path $output 'preparation') -WorldSeed 1315511732 -FeatureArguments @('--mesh-workers')
    $after = Get-AcceptanceWorktreeState -Root $root
    Assert-AcceptanceWorktreeStable -Baseline $before -Current $after -AllowSidecarRefresh
    Assert-AcceptanceResourcesReady -State $after
    $processes = @(Get-ChildItem -LiteralPath $output -Filter 'process.json' -File -Recurse)
    if ($processes.Count -ne 12) { throw "Expected 9 preflight + 3 preparation processes; got $($processes.Count)." }
    foreach ($file in $processes) {
        $m = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ($m.exit_code -ne 0 -or $m.streams_complete -ne $true -or $m.outcome -cne 'exited') {
            throw "Native stage did not finish and drain: $($file.Directory.Name)"
        }
        foreach ($name in @('stdout.log','stderr.log')) {
            $stream = [IO.File]::Open((Join-Path $file.Directory.FullName $name), [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $stream.Dispose()
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $root '.godot/mono/temp/bin/Debug/WorldForgeSim.dll'))) {
        throw 'Real game assembly was not built.'
    }
    Write-Host 'PASS official Windows Godot import, seven art tests, actual game build and both worker boots; 12 completed native stages with closed logs'
}
if ($result.Status -ne 'PASS' -or -not $result.Archive) {
    Get-Content -LiteralPath (Join-Path $directory 'REPORT.txt')
    throw "Real Windows preflight failed. Evidence: $directory"
}
Write-Host 'PASS real Windows preflight report, SHA256 manifest and ZIP are immediately readable; GPU acceptance remains pending'
