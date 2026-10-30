param([string]$GodotPath = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$out = Join-Path $root 'artifacts\acceptance'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$report = Join-Path $out 'result.txt'
"WorldForge acceptance - $(Get-Date -Format o)" | Set-Content -LiteralPath $report

# Windows PowerShell 5.1 turns redirected native stderr into RemoteException
# with ErrorActionPreference=Stop. Start-Process captures separate files.
# IMPORTANT: Do not use Start-Process -Wait: it can wait for descendants
# (including build/compiler servers) after the target process has exited.
# Process.WaitForExit waits only for the intended process, with a timeout.
function Invoke-LoggedStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    $stdout = Join-Path $out "$Name.stdout.txt"
    $stderr = Join-Path $out "$Name.stderr.txt"
    if (Test-Path -LiteralPath $stdout) { Remove-Item -LiteralPath $stdout -Force }
    if (Test-Path -LiteralPath $stderr) { Remove-Item -LiteralPath $stderr -Force }

    Write-Host "RUN: $Name"
    "===== START: $Name =====" | Add-Content -LiteralPath $report
    "Executable: $Exe" | Add-Content -LiteralPath $report
    "Arguments: $($Arguments -join ' ')" | Add-Content -LiteralPath $report
    $process = Start-Process -FilePath $Exe -ArgumentList $Arguments `
        -WorkingDirectory $root -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $timeoutMs = 600000
    if (-not $process.WaitForExit($timeoutMs)) {
        try { $process.Kill() } catch {}
        throw "Step '$Name' timed out. See $stdout and $stderr"
    }
    $exitCode = $process.ExitCode
    $process.Dispose()
    "Exit code: $exitCode" | Add-Content -LiteralPath $report
    if (Test-Path -LiteralPath $stdout) {
        $message = Get-Content -LiteralPath $stdout -Raw
        if ($message) { "[stdout]`n$message" | Add-Content -LiteralPath $report }
    }
    if (Test-Path -LiteralPath $stderr) {
        $message = Get-Content -LiteralPath $stderr -Raw
        if ($message) { "[stderr]`n$message" | Add-Content -LiteralPath $report }
    }
    "===== END: $Name =====" | Add-Content -LiteralPath $report
    if ($exitCode -ne 0) {
        throw "Step '$Name' failed with exit code $exitCode. Check $Name.stderr.txt and $Name.stdout.txt under $out"
    }
    Write-Host "PASS: $Name"
}

Push-Location $root
try {
    $sdkExe = Join-Path $root 'Tools\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $sdkExe)) {
        throw "H:-based .NET SDK missing at $sdkExe. Do not use the C: installation."
    }
    $sdk = (Get-Command dotnet -ErrorAction Stop).Source
    if (-not $sdk.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Blocked: dotnet on non-H: drive: $sdk. Dot-source scripts/enter-dev.ps1 first."
    }

    Invoke-LoggedStep -Name 'dotnet-info' -Exe $sdkExe -Arguments @('--info')
    Invoke-LoggedStep -Name 'core-build' -Exe $sdkExe -Arguments @('build', '.\src\WorldSim\WorldSim.csproj', '-c', 'Release')
    Invoke-LoggedStep -Name 'core-smoke' -Exe $sdkExe -Arguments @('run', '--project', '.\tests\WorldSim.Smoke\WorldSim.Smoke.csproj', '-c', 'Release')
    Invoke-LoggedStep -Name 'godot-csharp-build' -Exe $sdkExe -Arguments @('build', '.\WorldForgeSim.csproj', '-c', 'Debug')

    if ($GodotPath) {
        $godotExe = (Resolve-Path -LiteralPath $GodotPath).Path
        if (-not $godotExe.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Blocked: Godot executable outside H: $godotExe"
        }
        if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $godotExe -Parent) '_sc_'))) {
            throw 'Blocked: self-contained Godot _sc_ marker missing'
        }
        Invoke-LoggedStep -Name 'godot-headless-import' -Exe $godotExe -Arguments @('--headless', '--editor', '--path', $root, '--quit')
    } else {
        'SKIP: Godot headless import (no GodotPath supplied)' | Add-Content -LiteralPath $report
    }

    'PASS: CLI build + deterministic simulation smoke tests. Interactive Windows visual test still required.' |
        Add-Content -LiteralPath $report
    Write-Host "PASS: acceptance completed. Report: $report"
} catch {
    $details = ($_ | Out-String).Trim()
    "FAIL: $details" | Add-Content -LiteralPath $report
    Write-Host "FAIL: $details" -ForegroundColor Red
    Write-Host "See: $report" -ForegroundColor Yellow
    exit 1
} finally {
    Pop-Location
}
