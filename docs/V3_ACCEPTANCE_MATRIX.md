# WorldForge Sim — V3 acceptance / physical benchmark matrix

> This is the operational definition of **PASS**, not a record that every test
> has passed. Always identify the exact command, code SHA, evidence path and
> result. Use status **PENDING**, **FAIL**, **BLOCKED**, **PASS** — never silently
> convert an unperformed Windows test into PASS because headless CI is green.

## A. Evidence ledger template (one entry per candidate / scenario)

```yaml
evidence_schema: 2
session_id: null
scenario_id: null
milestone: V3.3.0
date: YYYY-MM-DD
candidate_pr: null
candidate_commit_sha: null
branch: main
git_status_clean: null     # false if user project.godot is intentionally modified
baseline_commit_sha: null
local_config_diff_sha256: null
backend: direct                 # compare with region-mirror in the same scene
world_dimensions: 120x88
generator_version: null
build_configuration: null
vsync_mode: null
fps_limit: null
engine_time_scale: null
delta_smoothing: null
engine: Godot 4.7.2 Mono
dotnet_sdk_path: H:\WorldForge-Sim\Tools\dotnet\dotnet.exe
windows_version: null
gpu: AMD Radeon R9 M395X
gpu_driver: null
renderer: gl_compatibility
viewport: 1280x800
fullscreen: false
seed: 1315511732
camera_pose: { pivot_x: null, pivot_z: null, yaw: null, pitch: null, ortho_size: null }
quality: Eco
slope_mode: null
simulation_tick: null
simulation_speed: null
render_phase: null
input_trace_sha256: null
scenario: pan-coast-60s
warmup_seconds: 10
requested_duration_seconds: 60
actual_duration_seconds: null
clock_source: null             # monotonic ticks or Stopwatch; not engine delta
capture_start_us: null
capture_end_us: null
sample_count: null
dropped_samples: null
buffer_overflow: null
percentile_method: nearest-rank
stall_over_50ms_count: null
stall_over_100ms_count: null
capture_valid: null
fps_mean: null
frame_ms_p50: null
frame_ms_p95: null
frame_ms_p99: null
frame_ms_max: null
gc_alloc_bytes: null
cpu_mesh_build_ms_p95: null
main_thread_mesh_upload_ms_p95: null
visible_uncovered_cells_max: null
input_handler_to_transform_ms_p95: null
physical_input_to_display_ms_p95: null # unavailable without an appropriate method
ram_peak_mb: null
vram_peak_mb: null
resident_regions_peak: null
resident_render_chunks_peak: null
screenshot_before: null
screenshot_after: null
frame_csv: null
godot_log: null
core_ci_url: null
core_ci_head_sha: null
engine_ci_url: null
engine_ci_head_sha: null
artifact_sha256_file: null
result: PENDING
operator_approved: null
deviations: []
```

All fields `null` mean **unknown or not measured**, not zero. This is the target schema; the current F9 exporter does not produce it. Candidate and baseline captures must have matching scenario identity. A truncated or mismatched capture has `capture_valid: false` and cannot produce a performance PASS. If automated script cannot sample GPU/VRAM, report "unavailable" explicitly rather than guessing. If `git status` shows user-modified `project.godot`, record it but DO NOT reset it.

## B. Exact user interaction scenarios

| ID | Reproducible action | What can fail? | Required evidence |
| --- | --- | --- | --- |
| B01 | Same seed `1315511732`, 1280×800, Eco, mountain at same pivot and zoom, screenshot | Flat cap returns, rock blobs float, missing cliff facets | F8 PNG before/after, seed, camera and operator art verdict |
| B02 | Same seed/pose, Balanced vs Eco toggled with F4 | Broken water, unreadable forest, missing near terrain, massive shadow cost | 2 screenshots + 2 F9 CSV + HUD quality |
| B03 | Pan right-drag 60s at measured constant screen displacement; return reverse drag | Mouse lag, camera drift, unloaded holes | F9 CSV, position/pointer trace, visible cells cover 100% |
| B04 | Wheel zoom 30s repeatedly in/out anchored to coastal mouse position | Zoom loses surface point, jumps, missing foliage/terrain | Pick distance error in cells and screenshots at ends |
| B05 | WASD/arrow camera 30s near world edge and island center | Bounds clipping, unexpected speed variation | Camera pivot/zoom log; clearly labeled application timing |
| B06 | Paint Forest/Ocean/Mountain across `x=63/64` 64×64 region boundary and `x=15/16` 16×16 render chunk boundary | Old coast texture, seam, ghost prop, mismatched picker | screenshots, cell diffs, chunk revisions, hit-tests |
| B07 | Use F7 sculpt up/down then F11 save / F12 reload; pan away/revisit | Wrong vertex height, corrupt region, lost edit | checksums, height equality and file paths |
| B08 | Spawn people, run 1x/2x/4x, pause, meteor, New World and return to new Seed | Citizens missing/dislocated, stale job from prior world, RNG diverges | before/after populations/seed/ticks, no Godot errors |
| B09 | Startup, pan quickly through distant map, zoom to wide overview | V3-style blank holes, heavy burst upload, large CPU GC | unloaded-visible-cell count=0 (V3.4), p99 and chunks per frame |
| B10 | Corrupt a **copy** of saved region, wrong seed, truncated file and simulated interrupted save | Partial state applied, data loss, crash | explicit load refusal and prior save still readable |
| B11 | Close during active camera motion/streaming and restart | background thread leak, event handler leak, stale callbacks | exit code, active worker=0 after shutdown, re-open clean |
| B12 | Direct V2 vs RegionMirror opt-in, identical 120×88 world seed, brush script and camera | subtle biome/color/wave differences; reduced responsiveness | exact CPU parity tests + F8 manual appearance + F9 matched FPS |
| B13 | Lower GPU tier and 1920×1080 optional compare | GPU fragment bound causes frame-time spikes | rendering settings + CSV + screenshot |
| B14 | Very high cell coordinate and rapid teleport (when V3.4+ ready) | float precision, blank view, one-frame explosions | region key, focus and epsilon exact pick; coverage log |

The currently merged V2 scene only supports portions of this matrix. **Do not mark B09/B14 as passed until corresponding streaming functionality is implemented**. In B10 do not damage the sole original user save.

## C. Performance protocol

1. Close the editor if it hangs; use **standalone** `run-visual-lab.ps1` or `verify-v2-art.ps1`, not an embedded Godot editor session.
2. Collect `git rev-parse HEAD`, `git status --short`, Godot console version/render driver and selected seed before each run.
3. Warm up 10 seconds after first visible terrain; run separate 30s idle, 60s right-drag, 30s zoom and 30s paint scenarios. The current F9 rolling CSV has no timestamps/reset and cannot be reliably time-filtered retrospectively. Implement explicit scenario boundaries/reset and monotonic samples in #54 before numerical acceptance. Do not treat a retained 7,200-sample tail as the entire session.
4. Match seed, dimensions, backend except the tested variable, camera pose/actions, viewport, quality/slope mode, tick/speed, render phase, build configuration, VSync/FPS cap and engine/driver. Aim for 3 passes per scenario and report every pass/run count; deviations remain explicit. Images need matching visual phase, not just the same seed.
5. Derive p50/p95/p99/max from monotonic callback intervals using the documented nearest-rank convention; report >50/>100ms counts, sample loss and actual duration. Mean FPS = sample count divided by summed valid interval seconds, not average instantaneous FPS. Keep engine delta and managed/native stage timings separately named; none alone is a GPU execution timer.
6. An input-handler timestamp to camera-transform update measures an application interval; it excludes hardware/OS/presentation delay. Record physical input-to-display latency only with an appropriate measurement method. Use coverage/queue counters for missing terrain; a stationary 60-FPS screenshot proves neither responsive motion nor continuous coverage.
7. Reproduce a failure with the smallest scene and one hypothesis. Keep a previous approved build/branch; revert new feature flag before changing gameplay graphics to hide cost.
8. Rate screen coverage: missing pixel/tile between visible land and intended sea/land surface => **FAIL**. Lower-detail rendered ground is acceptable if approved; completely empty missing regions are not.
9. **Provisional** Eco target: approximately 30 FPS sustained at 1280×800. 30 FPS is 33.3 ms/frame; 60 FPS is 16.7 ms/frame. Seek p95 <=33.3 ms and no repetitive >100ms stutters during stress, but do not claim target met until physical measured evidence.
10. Golden host is AMD R9 M395X; future GPUs cannot replace this gate. Keep F8 screenshot/export operations outside timed windows or explicitly mark and report capture overhead. Do not silently drop inconvenient slow frames; record background-load/power conditions and any invalidated run with its reason.

**Measurement gate:** fake-clock long-stall/first-sample/reset/ring-wrap/overflow checks precede host acceptance. Validate scenario identity and artifact completeness. See [ADR 0007](decisions/0007-measurement-evidence-and-scenario-identity.md). Existing FrameTime tests cover arithmetic/buffer behavior, not monotonic capture.

## D. Source-level and CI gates

| Code gate | Condition |
| --- | --- |
| CI01 deterministic core | `WorldSim` and all existing `tests/*.Smoke` projects (including mountain & region mirror) pass with exact CI run reference |
| CI02 authored assets | 10 bundled Kenney GLB files import under Godot Mono 4.7.2; no 404/missing texture/material |
| CI03 scene | Same VisualLab3D boots in direct and RegionMirror, including seed 1315511732 with both; rejection scene remains opt-in only |
| CI04 geometry | Shared-corner seam within tested floating tolerance, exact triangle picker on sloped land and edited coast |
| CI05 region lifecycle | Evicted/edited regions restore deterministic values, stale streaming result cannot publish, cache stays within budget |
| CI06 coast | Region halo mask matches approved V2 reference on bounded world and seams |
| CI07 save | Snapshot checksums, version mismatch/corruption refusal; future manifest atomicity fault-injection |
| CI08 performance | Measured p95/p99 at matching viewport on Windows, not Linux headless startup |
| CI09 visual approval | Screenshot comparison and explicit user approval required for any aesthetic/UX-changing release |
| CI10 regression protection | `Scenes/Main.tscn`, `project.godot`, V2 approved shader/camera unchanged unless change explicitly approved |

Use official CI links from PR, not "CI passed" without a run ID. Separate **automated source-pass** from **human Windows art-pass** in PR summary.

## E. Existing Windows commands (already implemented)

```powershell
Set-Location 'H:\WorldForge-Sim'
git rev-parse HEAD
git status --short
. .\scripts\enter-dev.ps1

# Approved, with exact mountain screenshot seed:
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732

# Opt-in exact WorldState / 64x64 region-mirror backend (same scene):
.\scripts\verify-v2-region-parity.ps1
```

**Existing hotkeys:** F4 quality, F5 facet relief, F6 smooth/legacy slope, F7 sculpt mode, F8 screenshot PNG, F9 frame CSV, F10 diagnostic counters, F11/F12 elevation save/load, right mouse drag and wheel zoom. Actual available features depend on selected scene.

**Current mirror-wrapper limitation:** it does not yet accept `-WorldSeed`; running the two commands above does not by itself guarantee a matched reference-seed comparison. Seed/pose forwarding belongs to #54. Reference-seed CPU parity and combined headless boot coverage are included in this repository revision.

**Planned command, not yet implemented:** `scripts/run-v3-3-acceptance.ps1`; its goal is one reproducible test command with SHA/state and CSV/PNG evidence bundle, Windows PowerShell 5.1 syntax validation, PASS/FAIL summary and cleanup.

## F. Release summary template

```markdown
Milestone: V3.3.0
PR: #... / HEAD ...
Code CI: PASS (run link) | FAIL | PENDING
Godot boot: PASS (run link) | FAIL | PENDING
Windows screenshot / mountain: PASS (file evidence + user verdict) | FAIL | PENDING
Windows camera: PASS (drag/zoom evidence) | FAIL | PENDING
Windows frame p95/p99: PASS (CSV+numbers) | FAIL | PENDING
Region/saves: PASS (tests and run links) | FAIL | NOT IN SCOPE
Regression vs previous approved: ...
Known deviations / untested items: ...
Decision: MERGE INTERNAL ONLY | PROMOTE USER-FACING | BLOCK
Next exact action / command: ...
```
