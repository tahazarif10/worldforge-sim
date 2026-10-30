# Capture bounded Godot process CPU and memory samples. Logs only to H:.
# Example: .\scripts\capture-host-performance.ps1 -Seconds 40 -IntervalSeconds 2
param(
    [ValidateRange(6, 300)][int]$Seconds = 40,
    [ValidateRange(1, 10)][int]$IntervalSeconds = 2
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing diagnostics outside H: ($root)"
}
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$out = Join-Path $root 'artifacts\performance'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$dest = Join-Path $out ('godot-host-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.csv')
$previous = @{}
$coreCount = [Math]::Max(1, [Environment]::ProcessorCount)
$sampleCount = [int][Math]::Ceiling($Seconds / $IntervalSeconds) + 1
Write-Host "Sampling Godot process CPU/memory to H: $dest"
Write-Host "Keep game running during sampling; try idle first, then brush actions."
for ($i = 0; $i -lt $sampleCount; $i++) {
    $now = Get-Date
    $processes = @(Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        [PSCustomObject]@{
            Timestamp = $now.ToString('o'); PID = -1; Title = '(no Godot process)';
            CpuPercentOfMachine = ''; CpuSeconds = ''; WorkingSetMB = ''; PrivateMB = ''
        } | Export-Csv -Path $dest -NoTypeInformation -Append -Encoding UTF8
    }
    foreach ($proc in $processes) {
        try {
            $totalSeconds = [double]$proc.CPU
            $cpuPercent = ''
            if ($previous.ContainsKey($proc.Id)) {
                $old = $previous[$proc.Id]
                $elapsed = ($now - $old.Time).TotalSeconds
                if ($elapsed -gt 0) {
                    $cpuPercent = [Math]::Round(
                        100.0 * ($totalSeconds - $old.CpuSeconds) / ($elapsed * $coreCount), 1
                    )
                }
            }
            $previous[$proc.Id] = @{ CpuSeconds = $totalSeconds; Time = $now }
            [PSCustomObject]@{
                Timestamp = $now.ToString('o')
                PID = $proc.Id
                Title = $proc.MainWindowTitle
                CpuPercentOfMachine = $cpuPercent
                CpuSeconds = [Math]::Round($totalSeconds, 2)
                WorkingSetMB = [Math]::Round($proc.WorkingSet64 / 1MB, 1)
                PrivateMB = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
            } | Export-Csv -Path $dest -NoTypeInformation -Append -Encoding UTF8
        }
        catch {
            Write-Warning "Could not sample process $($proc.Id): $_"
        }
    }
    if ($i -lt $sampleCount - 1) { Start-Sleep -Seconds $IntervalSeconds }
}
Write-Host "Completed. H:-based performance log: $dest"
Get-Item -LiteralPath $dest | Select-Object FullName, Length
