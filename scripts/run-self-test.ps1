#requires -Version 5.1
# One-click short diagnostics for the opt-in worker candidate. No upload or Git mutation.
param([switch]$NoClipboard)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module "$PSHOME\Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1" -ErrorAction Stop
Import-Module "$PSHOME\Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1" -ErrorAction Stop
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($env:OS -ne 'Windows_NT' -or -not $root.StartsWith('H:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run RUN-TEST.cmd from the existing H:\WorldForge-Sim Windows checkout.'
}
Set-Location $root
. (Join-Path $PSScriptRoot 'enter-dev.ps1')
. (Join-Path $PSScriptRoot 'acceptance-bundle.ps1')
. (Join-Path $PSScriptRoot 'self-test-report.ps1')
$session = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$directory = Join-Path $root ('artifacts\acceptance\self-test\' + $session)
Assert-WorldForgePortablePath -Root $root -Path $directory -Kind Directory -AllowMissing
Write-Host 'AUTOMATED SHORT TEST: idle, pan, zoom, paint on direct and region-mirror.'
Write-Host 'Eight windows open in sequence and close themselves: 3s warm-up + 8s capture each.'
Write-Host 'Build/import and worker settling add time. Do not click, resize, close or leave a test window.'
Write-Host 'Esc cancels. Logs and a report are preserved even when a test fails or is canceled.'
$result = Invoke-SelfTestBundle -Directory $directory -NoClipboard:$NoClipboard -Action {
    param($output)
    $bindings = [ordered]@{}
    foreach ($name in @('WORLDFORGE_HOME','TEMP','TMP','TMPDIR','NUGET_PACKAGES',
        'NUGET_HTTP_CACHE_PATH','NUGET_PLUGINS_CACHE_PATH','NUGET_SCRATCH','DOTNET_CLI_HOME',
        'DOTNET_BUNDLE_EXTRACT_BASE_DIR','APPDATA','LOCALAPPDATA','MSBuildUserExtensionsPath',
        'DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_HOST_PATH','PSModuleAnalysisCachePath',
        'DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH',
        'DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE','MSBUILDDISABLENODEREUSE')) {
        $bindings[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    Write-SelfTestText -Path (Join-Path $output 'portable-environment.json') -Text (
        [ordered]@{ schema=1; root=$root; scope='process-and-children'; bindings=$bindings;
            filesystem_trace=$false; os_driver_writes='not controlled by the project' } | ConvertTo-Json -Depth 4)
    & (Join-Path $PSScriptRoot 'run-v3-3-acceptance.ps1') -Quick -MeshWorkers -Scenario all -Repeats 1 -EvidenceDirectory (Join-Path $output 'capture')
}
if ($result.Status -ne 'PASS') { exit 1 }
exit 0
