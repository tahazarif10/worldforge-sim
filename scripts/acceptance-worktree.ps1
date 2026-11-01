# PS 5.1-compatible worktree identity. Dot-sourcing does not write or launch Godot.
# Sidecars are configuration/identity evidence, not disposable cache files.
function Invoke-AcceptanceGit {
    param([string]$Root, [string[]]$Arguments)
    $output = @(& git -C $Root @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Git inspection failed: $($Arguments -join ' ')"
    }
    return [string]::Join([Environment]::NewLine, [string[]]$output)
}

function Get-AcceptanceWorktreeState {
    param([Parameter(Mandatory = $true)][string]$Root)
    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version Latest
    $commit = (Invoke-AcceptanceGit -Root $Root -Arguments @('rev-parse','HEAD')).Trim()
    if ($commit -notmatch '^[0-9a-f]{40}$') { throw 'Unable to identify Git commit.' }
    # NUL separators preserve spaces without parsing Git's quoted display paths.
    $trackedText = Invoke-AcceptanceGit -Root $Root -Arguments @('ls-files','--cached','-z')
    $tracked = $trackedText.Split([char[]]@([char]0), [StringSplitOptions]::RemoveEmptyEntries)
    if ($tracked -cnotcontains 'project.godot') { throw 'Expected tracked project.godot at repository root.' }
    $expected = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    foreach ($path in $tracked) {
        if ($path -cmatch '\.(cs|gdshader)$') { $expected.Add($path + '.uid', $path) }
        elseif ($path -cmatch '\.glb$') { $expected.Add($path + '.import', $path) }
    }
    $statusText = Invoke-AcceptanceGit -Root $Root -Arguments @('status','--porcelain=v1','--untracked-files=all','-z')
    [string[]]$status = $statusText.Split([char[]]@([char]0), [StringSplitOptions]::RemoveEmptyEntries)
    [Array]::Sort($status, [StringComparer]::Ordinal)
    $trackedChanges = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $status) {
        if ($entry.Length -lt 4) { throw 'Malformed Git status record.' }
        $code = $entry.Substring(0,2)
        $path = $entry.Substring(3)
        if ($path -ceq 'project.godot' -and @(' M','M ','MM') -ccontains $code) {
            $trackedChanges.Add($entry)
            continue
        }
        if ($code -ceq '??' -and $expected.ContainsKey($path)) { continue }
        throw "Unreviewed working-tree change: $entry. Preserve/reconcile it before acceptance."
    }
    $sidecars = New-Object 'System.Collections.Generic.List[object]'
    $missing = New-Object 'System.Collections.Generic.List[string]'
    [string[]]$paths = @($expected.Keys)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    # Inspect every expected companion, including those hidden by a local Git exclude.
    foreach ($path in $paths) {
        $fullPath = Join-Path $Root $path
        if (-not (Test-Path -LiteralPath $fullPath)) { $missing.Add($path); continue }
        $item = Get-Item -LiteralPath $fullPath -Force
        $source = Get-Item -LiteralPath (Join-Path $Root $expected[$path]) -Force
        foreach ($file in @($item, $source)) {
            if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Expected a regular tracked resource and sidecar: $path"
            }
        }
        $sidecars.Add([pscustomobject][ordered]@{
            path = $path; source_path = $expected[$path]; bytes = $item.Length
            sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        })
    }
    return [pscustomobject][ordered]@{
        worktree_schema = 1; commit_sha = $commit
        git_status = @($status); tracked_status = @($trackedChanges.ToArray())
        project_godot_sha256 = (Get-FileHash -LiteralPath (Join-Path $Root 'project.godot') -Algorithm SHA256).Hash
        godot_sidecars = @($sidecars.ToArray())
        missing_godot_sidecars = @($missing.ToArray())
    }
}

function Assert-AcceptanceResourcesReady {
    param([Parameter(Mandatory = $true)]$State)
    if (@($State.missing_godot_sidecars).Count -gt 0) {
        throw ('Runtime preparation incomplete; missing companions: ' +
            ($State.missing_godot_sidecars -join ', '))
    }
}

function Get-AcceptanceSidecarDifferences {
    param([Parameter(Mandatory = $true)]$Baseline, [Parameter(Mandatory = $true)]$Current)
    $old = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    foreach ($item in $Baseline.godot_sidecars) { $old.Add($item.path, $item) }
    foreach ($item in $Current.godot_sidecars) {
        if (-not $old.ContainsKey($item.path)) {
            "Added companion: $($item.path)"
            continue
        }
        $previous = $old[$item.path]
        if ($previous.sha256 -cne $item.sha256 -or $previous.bytes -ne $item.bytes -or
            $previous.source_path -cne $item.source_path) {
            "Changed companion: $($item.path)"
        }
        [void]$old.Remove($item.path)
    }
    foreach ($path in $old.Keys) { "Removed companion: $path" }
}

function Assert-AcceptanceWorktreeStable {
    param(
        [Parameter(Mandatory = $true)]$Baseline,
        [Parameter(Mandatory = $true)]$Current,
        [switch]$AllowSidecarRefresh
    )
    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version Latest
    if ($Baseline.worktree_schema -ne 1 -or $Current.worktree_schema -ne 1 -or
        $Baseline.commit_sha -cne $Current.commit_sha -or
        $Baseline.project_godot_sha256 -cne $Current.project_godot_sha256 -or
        ($Baseline.tracked_status | ConvertTo-Json -Compress) -cne
        ($Current.tracked_status | ConvertTo-Json -Compress)) {
        throw 'Source/project configuration changed during acceptance preparation or capture.'
    }
    # Only the import/build preparation boundary may establish fresh sidecars.
    if (-not $AllowSidecarRefresh -and (
        ($Baseline.git_status | ConvertTo-Json -Compress) -cne
        ($Current.git_status | ConvertTo-Json -Compress) -or
        (ConvertTo-Json -InputObject @($Baseline.godot_sidecars) -Depth 5 -Compress) -cne
        (ConvertTo-Json -InputObject @($Current.godot_sidecars) -Depth 5 -Compress))) {
        $details = @(Get-AcceptanceSidecarDifferences -Baseline $Baseline -Current $Current)
        if ($details.Count -eq 0) { $details = @('Git status changed; compare the saved before/after manifests.') }
        throw ('Godot sidecar/configuration identity changed during capture. ' + ($details -join '; '))
    }
}

function Export-AcceptanceSidecars {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)]$State,
        [Parameter(Mandatory = $true)][string]$Directory
    )
    $ErrorActionPreference = 'Stop'
    foreach ($sidecar in $State.godot_sidecars) {
        $relative = [string]$sidecar.path
        if ([IO.Path]::IsPathRooted($relative) -or
            $relative -match '(^|/)\.\.?(/|$)|\\|[\x00-\x1f]') {
            throw 'Unsafe sidecar snapshot path.'
        }
        $destination = Join-Path $Directory $relative
        if (Test-Path -LiteralPath $destination) { throw "Snapshot already exists: $relative" }
        [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $Root $relative) -Destination $destination
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -cne $sidecar.sha256) {
            throw "Sidecar changed while preserving evidence: $relative"
        }
    }
}
