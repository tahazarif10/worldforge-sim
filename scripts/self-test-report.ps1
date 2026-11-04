# PS 5.1. Dot-sourcing is side-effect free. All output stays in the caller's new run directory.
function Write-SelfTestText {
    param([string]$Path, [AllowEmptyString()][string]$Text)
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

function New-SelfTestReport {
    param([string]$Directory, [string]$Status, [string]$Failure, [double]$Seconds)
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('AUTOMATED DIAGNOSTIC REPORT')
    $lines.Add("Status: $Status")
    $lines.Add('Profile: short-diagnostic; physical/visual/performance acceptance: PENDING')
    $lines.Add("Elapsed seconds: $([Math]::Round($Seconds, 1))")
    $lines.Add("Evidence directory: $Directory")
    if ($Failure) { $lines.Add("Failure: $Failure") }
    $lines.Add('The game closes automatically. Manual closure or Esc means INCOMPLETE, not a game crash verdict.')
    $lines.Add('No files were uploaded. Paste this report into the active project conversation.')
    $portable = Join-Path $Directory 'portable-environment.json'
    if (Test-Path -LiteralPath $portable -PathType Leaf) {
        try {
            $environment = Get-Content -LiteralPath $portable -Raw -Encoding UTF8 | ConvertFrom-Json
            $lines.Add("Portable tool configuration: $($environment.root); scope: $($environment.scope)")
            $lines.Add("Temp: $($environment.bindings.TEMP); NuGet: $($environment.bindings.NUGET_PACKAGES)")
            $lines.Add("Tool app data: $($environment.bindings.APPDATA); .NET home: $($environment.bindings.DOTNET_CLI_HOME)")
            $lines.Add('Configuration evidence only, not a filesystem trace or proof of zero Windows/driver writes. Full bindings: portable-environment.json')
        } catch { $lines.Add("Portable configuration unavailable: $($_.Exception.Message)") }
    }
    $metadata = Join-Path $Directory 'capture\metadata.json'
    if (Test-Path -LiteralPath $metadata) {
        try {
            $m = Get-Content -LiteralPath $metadata -Raw -Encoding UTF8 | ConvertFrom-Json
            $lines.Add("Commit: $($m.candidate_commit_sha)")
            $lines.Add("Planner: $($m.streaming_planner); seed: $($m.requested_seed); quality: $($m.quality)")
            $lines.Add("GPU/driver: $($m.gpu_driver | ConvertTo-Json -Compress)")
        } catch { $lines.Add("Metadata unavailable: $($_.Exception.Message)") }
    }
    if (-not (Test-Path -LiteralPath $metadata)) {
        $early = Join-Path $Directory 'capture\worktree-before-import.json'
        if (Test-Path -LiteralPath $early) {
            try {
                $m = Get-Content -LiteralPath $early -Raw -Encoding UTF8 | ConvertFrom-Json
                $lines.Add("Commit (before preflight): $($m.commit_sha)")
            } catch { $lines.Add("Early identity unavailable: $($_.Exception.Message)") }
        }
    }
    $result = Join-Path $Directory 'capture\result.md'
    if (Test-Path -LiteralPath $result) {
        $lines.Add('')
        try { $lines.Add((Get-Content -LiteralPath $result -Raw -Encoding UTF8)) }
        catch { $lines.Add("Result file unavailable: $($_.Exception.Message)") }
    }
    $captureRoot = Join-Path $Directory 'capture'
    if (Test-Path -LiteralPath $captureRoot) {
        foreach ($file in @(Get-ChildItem -LiteralPath $captureRoot -Filter 'capture.json' -File -Recurse | Sort-Object FullName)) {
            try {
                $m = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                $lines.Add("Worker window [$($file.Directory.Name)]: $($m.worker_window | ConvertTo-Json -Compress)")
                if ($m.PSObject.Properties.Name -contains 'work_window') {
                    $lines.Add("Frame work [$($file.Directory.Name)]: $($m.work_window | ConvertTo-Json -Depth 5 -Compress)")
                }
            } catch { $lines.Add("Capture metadata error: $($file.Directory.Name): $($_.Exception.Message)") }
        }
    }
    $lines.Add('')
    $lines.Add('LOG TAILS (complete logs, captures and screenshots remain in the ZIP)')
    # Prefer stderr and newest logs on failure, while retaining a fixed-size clipboard report.
    $logs = @()
    try {
        $logs = @(Get-ChildItem -LiteralPath $Directory -Filter '*.log' -File -Recurse |
            Where-Object { $_.Length -gt 0 } | Sort-Object LastWriteTime -Descending | Select-Object -First 18)
    } catch { $lines.Add("Log enumeration incomplete: $($_.Exception.Message)") }
    foreach ($file in $logs) {
        $relative = $file.FullName.Substring($Directory.TrimEnd('\').Length + 1)
        $lines.Add("[$relative]")
        try {
            $tail = [string]::Join([Environment]::NewLine, [string[]]@(Get-Content -LiteralPath $file.FullName -Tail 20))
            if ($tail.Length -gt 2000) { $tail = $tail.Substring($tail.Length - 2000) }
            $lines.Add($tail)
        } catch { $lines.Add("UNAVAILABLE (secondary collection issue): $($_.Exception.Message)") }
    }
    $text = [string]::Join([Environment]::NewLine, $lines.ToArray())
    if ($text.Length -gt 40000) { $text = $text.Substring(0, 40000) + [Environment]::NewLine + '[Report trimmed; complete evidence is in the ZIP.]' }
    return $text
}

function Invoke-SelfTestBundle {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [switch]$NoClipboard
    )
    $ErrorActionPreference = 'Stop'
    $Directory = [IO.Path]::GetFullPath($Directory)
    if ((Test-Path -LiteralPath $Directory) -or (Test-Path -LiteralPath ($Directory + '.zip'))) {
        throw 'Refusing to overwrite an existing diagnostic run.'
    }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $status = 'FAIL_OR_INCOMPLETE'
    $failure = ''
    $archive = $null
    $copied = $false
    $collection = New-Object 'System.Collections.Generic.List[string]'
    try {
        & $Action $Directory *>&1 | Tee-Object -FilePath (Join-Path $Directory 'runner.log') |
            ForEach-Object { Write-Host $_ }
        $status = 'PASS'
    }
    catch { $failure = $_.Exception.Message }
    finally {
        $clock.Stop()
        # Write the plain report first, so ZIP/clipboard failures cannot hide the result.
        Write-SelfTestText -Path (Join-Path $Directory 'status.json') -Text (
            [ordered]@{ diagnostic_schema=1; status=$status; physical_acceptance='PENDING';
                failure=$failure; elapsed_seconds=$clock.Elapsed.TotalSeconds;
                finished_utc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json)
        try { $report = New-SelfTestReport -Directory $Directory -Status $status -Failure $failure -Seconds $clock.Elapsed.TotalSeconds }
        catch {
            $report = "AUTOMATED DIAGNOSTIC REPORT" + [Environment]::NewLine +
                "Status: $status" + [Environment]::NewLine + "Failure: $failure" +
                [Environment]::NewLine + "Report detail collection failed: $($_.Exception.Message)" +
                [Environment]::NewLine + "Evidence directory: $Directory"
        }
        Write-SelfTestText -Path (Join-Path $Directory 'REPORT.txt') -Text $report
        try {
            Write-AcceptanceHashes -Directory $Directory
        } catch { $collection.Add("Hash collection incomplete: $($_.Exception.Message)") }
        try {
            Compress-Archive -LiteralPath $Directory -DestinationPath ($Directory + '.zip') -CompressionLevel Optimal
            $archive = $Directory + '.zip'
        } catch { $collection.Add("ZIP unavailable; evidence folder preserved: $($_.Exception.Message)") }
        if ($collection.Count -gt 0) {
            # The original run status/failure remains authoritative. Additional failures are secondary.
            $notice = [string]::Join([Environment]::NewLine, $collection.ToArray())
            Write-SelfTestText -Path (Join-Path $Directory 'collection-warnings.txt') -Text $notice
            # Keep REPORT.txt immutable after hashing/archiving; append notices only to the clipboard/console.
            $report += [Environment]::NewLine + 'COLLECTION WARNINGS' + [Environment]::NewLine + $notice
            Write-Warning $notice
        }
        if (-not $NoClipboard) {
            for ($attempt=0; $attempt -lt 3 -and -not $copied; $attempt++) {
                try { Set-Clipboard -Value $report -ErrorAction Stop; $copied = $true }
                catch { if ($attempt -lt 2) { Start-Sleep -Milliseconds 100 } }
            }
        }
        Write-Host "RESULT: $status"
        Write-Host ("REPORT: " + (Join-Path $Directory 'REPORT.txt'))
        if ($archive) { Write-Host "ATTACHMENT: $archive" }
        if ($copied) { Write-Host 'Report copied. Paste it into the active project conversation.' }
        elseif (-not $NoClipboard) { Write-Warning 'Clipboard unavailable. Open REPORT.txt and copy its contents.' }
    }
    return [pscustomobject]@{ Status=$status; Directory=$Directory; Archive=$archive; Copied=$copied }
}
