param([string]$GodotPath = "")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
$out = Join-Path $root 'artifacts\acceptance'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$report = Join-Path $out 'result.txt'
"WorldForge acceptance - NOT YET RUN" | Set-Content $report
Push-Location $root
try {
    $sdk = Get-Command dotnet -ErrorAction Stop
    if (-not $sdk.Source.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Blocked: dotnet executable is on a non-H: drive: $($sdk.Source). Configure H: SDK first."
    }
    & dotnet --info *>> $report
    & dotnet build '.\src\WorldSim\WorldSim.csproj' -c Release *>> $report
    if ($LASTEXITCODE -ne 0) { throw 'Core build failed' }
    & dotnet run --project '.\tests\WorldSim.Smoke\WorldSim.Smoke.csproj' -c Release *>> $report
    if ($LASTEXITCODE -ne 0) { throw 'Core smoke tests failed' }
    & dotnet build '.\WorldForgeSim.csproj' -c Debug *>> $report
    if ($LASTEXITCODE -ne 0) { throw 'Godot C# project build failed' }
    if ($GodotPath) {
        $godotExe = (Resolve-Path $GodotPath).Path
        if (-not $godotExe.StartsWith('H:\', [System.StringComparison]::OrdinalIgnoreCase)) { throw "Blocked: Godot is not on H: $godotExe" }
        if (-not (Test-Path (Join-Path (Split-Path $godotExe -Parent) '_sc_'))) { throw 'Blocked: Godot portable _sc_ marker missing' }
        & $godotExe --headless --editor --path $root --quit *>> $report
        if ($LASTEXITCODE -ne 0) { throw 'Godot headless project import failed' }
    }
    "PASS: CLI build + simulation smoke checks. Windows visual run still required." | Add-Content $report
    Write-Host "PASS - report: $report"
} catch {
    "FAIL: $_" | Add-Content $report
    Write-Error "FAIL - see $report"
    exit 1
} finally {
    Pop-Location
}
