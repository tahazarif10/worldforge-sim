#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'scripts/acceptance-worktree.ps1')
. (Join-Path $root 'scripts/acceptance-bundle.ps1')
. (Join-Path $root 'scripts/acceptance-runtime.ps1')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('acceptance-worktree-' + [Guid]::NewGuid().ToString('N'))
$fixtureRoot = Join-Path $tempRoot 'repo with spaces'
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
function Write-FixtureFile([string]$Path, [string]$Text) {
    $destination = Join-Path $fixtureRoot $Path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::WriteAllText($destination, $Text, $utf8)
}
function Check($condition, [string]$name) {
    if (-not $condition) { throw "FAIL $name" }
    Write-Host "PASS $name"
}
function Reject([scriptblock]$action, [string]$name) {
    $rejected = $false
    try { & $action | Out-Null } catch { $rejected = $true }
    Check $rejected $name
}
function Invoke-FixtureGit([string[]]$Arguments) {
    [void](Invoke-AcceptanceGit -Root $fixtureRoot -Arguments $Arguments)
}
try {
    Invoke-FixtureGit -Arguments @('init','--quiet')
    Invoke-FixtureGit -Arguments @('config','core.autocrlf','false')
    Write-FixtureFile '.gitignore' ('artifacts/' + [Environment]::NewLine)
    Write-FixtureFile 'project.godot' '[application]'
    Write-FixtureFile 'Scripts/Camera.cs' '// camera'
    Write-FixtureFile 'Scripts/New.cs' '// imported later'
    Write-FixtureFile 'Scripts/Tracked.cs' '// tracked UID'
    Write-FixtureFile 'Scripts/Tracked.cs.uid' 'uid://tracked'
    Write-FixtureFile 'Assets/water.gdshader' 'shader_type spatial;'
    Write-FixtureFile 'Assets/tree model.glb' 'controlled test asset'
    Invoke-FixtureGit -Arguments @('add','.')
    Invoke-FixtureGit -Arguments @('-c','user.name=Acceptance Smoke','-c','user.email=acceptance@example.invalid',
        '-c','commit.gpgsign=false','commit','--quiet','-m','Fixture')
    $clean = Get-AcceptanceWorktreeState -Root $fixtureRoot
    Check ($clean.git_status.Count -eq 0 -and $clean.godot_sidecars.Count -eq 1) 'clean Git state and tracked sidecar captured'

    Write-FixtureFile 'project.godot' '[application] local preserved'
    Write-FixtureFile 'Scripts/Camera.cs.uid' 'uid://cameraa'
    Write-FixtureFile 'Assets/water.gdshader.uid' 'uid://water'
    Write-FixtureFile 'Assets/tree model.glb.import' '[remap] before import'
    $before = Get-AcceptanceWorktreeState -Root $fixtureRoot
    Reject { Assert-AcceptanceResourcesReady -State $before } 'missing UID blocks sealing the prepared baseline'
    Check ($before.godot_sidecars.Count -eq 4 -and $before.tracked_status.Count -eq 1) 'actual dirty project plus untracked GLB/CS/shader companions allowed'
    Check (@($before.godot_sidecars | Where-Object path -CEQ 'Assets/tree model.glb.import').Count -eq 1) 'NUL Git records preserve paths with spaces'
    $beforeDir = Join-Path $fixtureRoot 'artifacts/sidecars/before-import'
    Export-AcceptanceSidecars -Root $fixtureRoot -State $before -Directory $beforeDir

    Write-FixtureFile 'Scripts/New.cs.uid' 'uid://new'
    Write-FixtureFile 'Assets/tree model.glb.import' '[remap] after import'
    $prepared = Get-AcceptanceWorktreeState -Root $fixtureRoot
    Assert-AcceptanceWorktreeStable -Baseline $before -Current $prepared -AllowSidecarRefresh
    Assert-AcceptanceResourcesReady -State $prepared
    Check ($prepared.godot_sidecars.Count -eq 5) 'new and refreshed sidecars accepted only at preparation boundary'
    Reject { Assert-AcceptanceWorktreeStable -Baseline $before -Current $prepared } 'capture rejects preparation-only identity refresh'
    $captureDir = Join-Path $fixtureRoot 'artifacts/sidecars/capture'
    Export-AcceptanceSidecars -Root $fixtureRoot -State $prepared -Directory $captureDir
    Check ([IO.File]::ReadAllText((Join-Path $beforeDir 'Assets/tree model.glb.import')) -ceq '[remap] before import') 'original import settings preserved before preparation'
    Check ([IO.File]::ReadAllText((Join-Path $captureDir 'Assets/tree model.glb.import')) -ceq '[remap] after import') 'actual prepared settings preserved for reproduction'
    $roundtrip = $prepared | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    Assert-AcceptanceWorktreeStable -Baseline $roundtrip -Current (Get-AcceptanceWorktreeState -Root $fixtureRoot)
    Write-Host 'PASS serialized manifest can be revalidated against actual Git/filesystem'

    # A real native child reproduces a late UID and an exit-zero without capture.json.
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'Scripts/New.cs.uid')
    $beforeChild = Get-AcceptanceWorktreeState -Root $fixtureRoot
    $childPath = Join-Path $fixtureRoot 'artifacts/child.ps1'
    Write-FixtureFile 'artifacts/child.ps1' 'param([string]$Root); [IO.File]::WriteAllText((Join-Path $Root "Scripts/New.cs.uid"),"uid://new"); exit 0'
    $childOutput = Join-Path $fixtureRoot 'artifacts/child-output'
    [IO.Directory]::CreateDirectory($childOutput) | Out-Null
    $processArgs = @{
        Executable = (Join-Path $PSHOME 'powershell.exe')
        Arguments = @('-NoProfile','-NonInteractive','-File',$childPath,'-Root',$fixtureRoot)
        WorkingDirectory = $fixtureRoot; OutputDirectory = $childOutput; TimeoutSeconds = 15
    }
    $afterChild = Invoke-AcceptanceObservedProcess @processArgs
    Check (Test-Path -LiteralPath (Join-Path $childOutput 'worktree-after.json')) 'post-run manifest saved for an incomplete native child'
    $details = @(Get-AcceptanceSidecarDifferences -Baseline $beforeChild -Current $afterChild)
    Check ($details -ccontains 'Added companion: Scripts/New.cs.uid') 'diagnostic names the exact late-generated UID'
    $message = ''
    try {
        Read-AcceptanceCapture -Directory $childOutput -ExpectedSession test -ExpectedScenario idle -ExpectedBackend direct -ExpectedSeed 1 -AllowHeadless | Out-Null
    } catch { $message = $_.Exception.Message }
    Check ($message.Contains('capture.json') -and $message.Contains('automatic completion')) 'exit zero without capture output is explicitly incomplete'
    Assert-AcceptanceWorktreeStable -Baseline $prepared -Current $afterChild
    Write-FixtureFile 'artifacts/child.ps1' 'exit 7'
    $processArgs.OutputDirectory = Join-Path $fixtureRoot 'artifacts/failed-child'
    [IO.Directory]::CreateDirectory($processArgs.OutputDirectory) | Out-Null
    $message = ''
    try { Invoke-AcceptanceObservedProcess @processArgs | Out-Null } catch { $message = $_.Exception.Message }
    Check ($message.Contains('exited 7')) 'snapshot handling preserves the native failure reason'
    Check (Test-Path -LiteralPath (Join-Path $processArgs.OutputDirectory 'worktree-after.json')) 'post-run manifest retained after native failure'

    Write-FixtureFile 'Scripts/Camera.cs.uid' 'uid://camerab'
    $changed = Get-AcceptanceWorktreeState -Root $fixtureRoot
    Check (($prepared.git_status -join '|') -ceq ($changed.git_status -join '|')) 'content mutation keeps the same Git status'
    Reject { Assert-AcceptanceWorktreeStable -Baseline $prepared -Current $changed } 'same-length sidecar mutation caught by hash'
    Reject { Export-AcceptanceSidecars -Root $fixtureRoot -State $prepared -Directory (Join-Path $fixtureRoot 'artifacts/stale') } 'copy refuses stale manifest content'
    Write-FixtureFile 'Scripts/Camera.cs.uid' 'uid://cameraa'
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'Scripts/New.cs.uid')
    Reject { Assert-AcceptanceWorktreeStable -Baseline $prepared -Current (Get-AcceptanceWorktreeState -Root $fixtureRoot) } 'sidecar removal invalidates capture'
    Write-FixtureFile 'Scripts/New.cs.uid' 'uid://new'

    Write-FixtureFile '.git/info/exclude' ('Scripts/New.cs.uid' + [Environment]::NewLine)
    $ignored = Get-AcceptanceWorktreeState -Root $fixtureRoot
    Check ($ignored.godot_sidecars.Count -eq 5) 'locally ignored companions remain in the evidence manifest'
    Write-FixtureFile 'Scripts/New.cs.uid' 'uid://changed'
    Reject { Assert-AcceptanceWorktreeStable -Baseline $ignored -Current (Get-AcceptanceWorktreeState -Root $fixtureRoot) } 'ignored companion content drift detected'
    Write-FixtureFile 'Scripts/New.cs.uid' 'uid://new'
    Write-FixtureFile '.git/info/exclude' ''

    foreach ($path in @('Scripts/Unknown.cs','Scripts/Unknown.cs.uid','Assets/orphan.glb.import','notes.uid')) {
        Write-FixtureFile $path 'unexpected'
        Reject { Get-AcceptanceWorktreeState -Root $fixtureRoot } "unreviewed/orphan file refused: $path"
        Remove-Item -LiteralPath (Join-Path $fixtureRoot $path)
    }
    Write-FixtureFile 'Scripts/Camera.cs' '// source changed'
    Reject { Get-AcceptanceWorktreeState -Root $fixtureRoot } 'tracked source edits remain blocked'
    Write-FixtureFile 'Scripts/Camera.cs' '// camera'
    Write-FixtureFile 'Scripts/Tracked.cs.uid' 'uid://replacement'
    Reject { Get-AcceptanceWorktreeState -Root $fixtureRoot } 'modified tracked sidecar requires explicit reconciliation'
    Write-FixtureFile 'Scripts/Tracked.cs.uid' 'uid://tracked'
    Write-FixtureFile 'project.godot' '[application] changed after baseline'
    Reject { Assert-AcceptanceWorktreeStable -Baseline $prepared -Current (Get-AcceptanceWorktreeState -Root $fixtureRoot) -AllowSidecarRefresh } 'preparation never permits project configuration drift'
    Write-FixtureFile 'project.godot' '[application] local preserved'
    Assert-AcceptanceWorktreeStable -Baseline $prepared -Current (Get-AcceptanceWorktreeState -Root $fixtureRoot)
    Check ([IO.File]::ReadAllText((Join-Path $fixtureRoot 'project.godot')) -ceq '[application] local preserved') 'existing local project configuration preserved'
    Write-Host 'PASS ALL real Git worktree, import-boundary and sidecar evidence tests'
}
finally {
    # Only the unique test directory created by this invocation is removed.
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}
