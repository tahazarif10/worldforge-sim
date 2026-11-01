# V3.3 measurement and parity runner

## Implemented scope

The first V3.3.1 implementation supplies monotonic frame capture, explicit completed windows, bounded buffers, reference-seed forwarding, a fixed camera/scene fixture, four scripted scenarios and a Windows H: bundle runner. It implements [ADR 0007](decisions/0007-measurement-evidence-and-scenario-identity.md)'s measurement foundation.

**Physical acceptance remains PENDING.** A valid data bundle is not a user art verdict, a GPU execution timer, physical input latency or proof of large-world streaming. The fixture deliberately pauses simulation at tick zero to compare the renderer/data backend under controlled conditions. Live simulation, real mouse/keyboard dispatch, save/load and final camera feel are separate [acceptance scenarios](V3_ACCEPTANCE_MATRIX.md).

## Run on the user's Windows machine

Use the existing portable Godot 4.7.2 Mono/.NET tools in `H:\WorldForge-Sim`. Inspect Git status and safely obtain the merged implementation before launching; the runner does not fetch/switch/pull/reset/stash.

```powershell
Set-Location 'H:\WorldForge-Sim'
git rev-parse HEAD
git status --short

# First useful capture: both backends, one idle pass (30s + 10s warm-up each).
.\scripts\run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1

# Full Eco matrix: all four scenarios, three passes, alternating backend order.
.\scripts\run-v3-3-acceptance.ps1

# A separate matched Balanced bundle:
.\scripts\run-v3-3-acceptance.ps1 -Quality Balanced
```

Default seed: `1315511732`. Default FPS cap: 60; change with `-MaxFps` and preserve the same value for comparisons. `-WorldSeed` and `-Scenario idle|pan|zoom|paint|all` are supported. The full three-pass matrix contains 24 launches, approximately 19 minutes of warm-up/capture plus import/build/startup time; the short idle command is the recommended first check.

The runner rebuilds using existing H: tooling and runs timing/parity correctness tests. Missing tools fail with their path; nothing is downloaded automatically. It preserves local `project.godot` and records its hash. Other working-tree changes require reconciliation before a reproducible capture; it does not erase them.

Keep each test window focused and leave controls alone. Esc cancels. Interaction, focus loss, resizing, clock anomalies, overflow, missing outputs, mismatched identity or incomplete workload prevents valid comparison. An error creates a FAIL report for the bundle where possible and exits unsuccessfully.

## Fixture and scenarios

Fixture ID: `v3.3-reference-1`. Same approved `VisualLab3D` scene, 120×88 world, center `(60,44)`, orthographic size 79, 1280×800 viewport, specified quality, smooth terrain and fixed initial simulation state.

| Scenario | Duration | Scripted work |
| --- | --- | --- |
| idle | 30 seconds | Fixed camera; simulation paused |
| pan | 60 seconds | Existing BeginDrag/DragTo algorithm, continuous sinusoidal screen trace |
| zoom | 30 seconds | Existing cursor-anchored ZoomAt with a continuous target-size trace |
| paint | 30 seconds | Forest/ocean brushes at four scheduled actions/second through existing screen picking; crosses x=64 |

Traces are continuous functions of monotonic elapsed time sampled at actual callbacks, not identical frame counts across machines. The camera CSV makes those sampling differences inspectable. Paint catches up at most four actions per callback; backlog or missing scheduled actions invalidates evidence instead of silently comparing different workloads.

The shader's `review_time` defaults to -1, preserving ordinary engine TIME animation. Only acceptance mode supplies scenario time. Before/after screenshots reset camera and water phase to zero; paint edits remain in the after image. Screenshots and formatting/export occur outside the measured window. Headless CI skips PNG capture and labels itself headless.

## Output and identity

All host output is under:

```text
H:\WorldForge-Sim\artifacts\acceptance\v3-3\<session>\
  metadata.json
  result.md
  SHA256SUMS.txt
  preflight.log
  FrameTime.Smoke.log
  TerrainBackendParity.Smoke.log
  <scenario>-<pass>-<backend>\
    capture.json
    frames.csv
    camera.csv
    before.png
    after.png
    godot.log
    stdout.log
    stderr.log
```

The outer metadata records candidate SHA, Git status, project configuration hash, rebuilt assembly hash, SDK/PowerShell and available GPU driver information. Each capture records actual fixture, backend, engine/runtime/render settings, window/viewport, FPS cap, VSync, clock, completeness and raw statistics. Unknown hardware fields remain null. The runner verifies source/configuration/assembly identity between launches.

`frames.csv` columns: `session_id, scenario_id, sequence, elapsed_us, interval_us, engine_delta_ms`. A callback interval comes from `Time.GetTicksUsec`. Engine delta is separately retained and may differ. First callback establishes the origin; Stop freezes the final full interval.

Capture capacity is 32,768 intervals. Overflow remains bounded and explicitly invalidates a complete-window claim. Interactive F9 uses a separate 7,200-sample rolling diagnostic capture with a JSON sidecar; it does not claim isolated-scenario acceptance. Old F9 files from before this implementation contained engine delta and cannot be upgraded retrospectively.

p50/p95/p99 use nearest rank. Mean FPS is count divided by summed elapsed seconds. Counts of intervals strictly above 50 and 100ms are included. The validator recomputes statistics from CSV, checks continuous sequence/timestamps, aligns camera rows and compares both the fixture hash and actual fixture fields.

## Valid capture vs accepted product

`capture_valid: true` means the instrumented window and declared fixture passed integrity checks. `physical_acceptance: PENDING` remains in every automatic report. No FPS threshold or aesthetic acceptance is auto-approved.

The automation exercises existing camera/brush methods, not physical input-device latency. This paused baseline does not characterize moving-agent/tick cost. Add those separate measurements after the initial host bundle is reviewed; never rename the current figures to imply they do.

## Verification and failure behavior

- Pure C# tests: first callback, a 500ms stall despite 133.333ms engine delta, reset, stop/export exclusion, ring overflow, bad clocks/deltas, invariant formatting, option bounds and deterministic trace points.
- Windows PowerShell 5.1 CI: actual parser, positive and deliberately broken bundles, mismatched fixtures, missing/reordered data, altered statistics, headless masquerading as host data, native argument quoting, child failure and timeout.
- Official Godot CI: direct/mirror boot plus actual short idle/pan/zoom/paint captures on both backends, validated by the shared bundle checker. These are integration checks, not GPU benchmarks.
- H: host screenshots, physical interaction and full-duration performance: PENDING until actually run.

Related: [state](PROJECT_STATE.md), [roadmap](ROADMAP.md), [#53](https://github.com/tahazarif10/worldforge-sim/issues/53), [#54](https://github.com/tahazarif10/worldforge-sim/issues/54).

## Rollback

Ordinary launch never enables `--acceptance-*` mode. Revert the implementation commit if source rollback is needed. Keep already captured evidence and user-local files; do not reset project.godot or delete artifacts to obtain a clean-looking report.
