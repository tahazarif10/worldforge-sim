# WorldForge Sim — V3 acceptance / physical benchmark matrix

> This is the operational definition of **PASS**, not a record that every test
> has passed. Always identify the exact command, code SHA, evidence path and
> result. Use status **PENDING**, **FAIL**, **BLOCKED**, **PASS** — never silently
> convert an unperformed Windows test into PASS because headless CI is green.

## A. Evidence ledger template (one entry per candidate / scenario)

```yaml
milestone: V3.3.0
date: YYYY-MM-DD
candidate_pr: null
candidate_commit_sha: null
branch: main
git_status_clean: null     # false if user project.godot is intentionally modified
baseline_commit_sha: null
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
scenario: pan-zoom-coast-60s
warmup_seconds: 10
duration_seconds: 60
sample_count: null
fps_mean: null
frame_ms_p50: null
frame_ms_p95: null
frame_ms_p99: null
frame_ms_max: null
gc_alloc_bytes: null
cpu_mesh_build_ms_p95: null
main_thread_mesh_upload_ms_p95: null
visible_uncovered_cells_max: null
camera_drag_latency_ms_p95: null
ram_peak_mb: null
vram_peak_mb: null
resident_regions_peak: null
resident_render_chunks_peak: null
screenshot_before: null
screenshot_after: null
frame_csv: null
godot_log: null
ci_url: null
result: PENDING
operator_approved: null
deviations: []
```

All fields `null` mean **not measured**, not zero. If automated script cannot sample GPU/VRAM, report "unavailable" explicitly rather than guessing. If `git status` shows user-modified `project.godot`, record it but DO NOT reset it.

## B. Exact user interaction scenarios

| ID | Reproducible action | What can fail? | Required evidence |
| --- | --- | --- | --- |
| B01 | Same seed `1315511732`, 1280×800, Eco, mountain at same pivot and zoom, screenshot | Flat cap returns, rock blobs float, missing cliff facets | F8 PNG before/after, seed, camera and operator art verdict |
| B02 | Same seed/pose, Balance vs Eco toggled with F4 | Broken water, unreadable forest, missing near terrain, massive shadow cost | 2 screenshots + 2 F9 CSV + HUD quality |
| B03 | Pan right-drag 60s at measured constant screen displacement; return reverse drag | Mouse lag, camera drift, unloaded holes | F9 CSV, position/pointer trace, visible cells cover 100% |
| B04 | Wheel zoom 30s repeatedly in/out anchored to coastal mouse position | Zoom loses surface point, jumps, missing foliage/terrain | Pick distance error in cells and screenshots at ends |
| B05 | WASD/arrow camera 30s near world edge and island center | Bounds clipping, unexpected speed variation | Camera pivot/zoom log; p95 input delay |
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
3. Warm up exactly 10 seconds after first visible terrain; run 30s idle, 60s right-drag, 30s zoom and 30s repeated paint. Separate CSV by scenario and quality. If F9 CSV is cumulative since startup, either add a reset marker in the future V3.3 runner or document sample time filters; do not silently treat it as isolated 60s data.
4. Compare original V2 and candidate with identical seed, resolution, focus, zoom and scripted movement. Use 3 passes per scenario if feasible to estimate variance; always report run count.
5. Derive `p50,p95,p99,max` from frame intervals; show dropped/stall count (>50, >100 ms), startup time and native mesh upload cost when instrumented. Distinguish Godot `delta` from CPU/GPU profiler timings.
6. Measure input latency using explicit input timestamp to camera transform update where instrumented; use F10 logs for active/pending visible chunk counts. A stationary screenshot with 60 FPS does not prove smooth camera navigation.
7. Reproduce a failure with the smallest scene and one hypothesis. Keep a previous approved build/branch; revert new feature flag before changing gameplay graphics to hide cost.
8. Rate screen coverage: missing pixel/tile between visible land and intended sea/land surface => **FAIL**. Lower-detail rendered ground is acceptable if approved; completely empty missing regions are not.
9. **Provisional** Eco target: approximately 30 FPS sustained at 1280×800. 30 FPS is 33.3 ms/frame; 60 FPS is 16.7 ms/frame. Seek p95 <=33.3 ms and no repetitive >100ms stutters during stress, but do not claim target met until physical measured evidence.
10. Golden host is AMD R9 M395X; future GPUs may justify optional effects but cannot replace the low-end acceptance gate.

## D. Source-level and CI gates

| Code gate | Condition |
| --- | --- |
| CI01 deterministic core | `WorldSim` and all existing `tests/*.Smoke` projects (including mountain & region mirror) pass with exact CI run reference |
| CI02 authored assets | 10 bundled Kenney GLB files import under Godot Mono 4.7.2; no 404/missing texture/material |
| CI03 scene | Same `VisualLab3D.tscn` boots in direct and opt-in RegionMirror; rejection scene remains opt-in only |
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
