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

All fields `null` mean **unknown or not measured**, not zero. This is the target schema; the standalone F9 diagnostic does not produce the full host bundle; the [scenario runner](V3_3_ACCEPTANCE_RUNNER.md) supplies nested fixture/statistics metadata and raw capture files. Candidate and baseline captures must have matching scenario identity. A truncated or mismatched capture has `capture_valid: false` and cannot produce a performance PASS. If automated script cannot sample GPU/VRAM, report "unavailable" explicitly rather than guessing. If `git status` shows user-modified `project.godot`, record it but DO NOT reset it.

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
3. Warm up 10 seconds after first visible terrain; run separate 30s idle, 60s right-drag, 30s zoom and 30s paint scenarios. Legacy F9 CSVs have no timestamps/reset and cannot be time-filtered retrospectively. New F9 is a mixed rolling monotonic diagnostic; use the implemented scenario runner for reset/stop and complete-window validation. Do not treat a retained 7,200-sample tail as the entire session.
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

**Current implementation:** the mirror wrapper accepts `-WorldSeed 1315511732`. The [scenario runner](V3_3_ACCEPTANCE_RUNNER.md) supplies camera/phase parity, monotonic capture and validated bundles. Its paused scripted tests do not replace manual input/live-simulation/visual gates.

**Implemented command:** `scripts/run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1` for an initial matched bundle. The report distinguishes VALID capture integrity from PENDING physical/product acceptance; failed/partial outputs are retained.

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

## G. Target-host short diagnostic — 2026-11-03

**Reported result: PASS; capture integrity VALID. Full visual/input/performance acceptance remains PENDING.** This is the first complete target-host short worker diagnostic supplied in the active project conversation after the preflight/log repair in [PR #72](https://github.com/tahazarif10/worldforge-sim/pull/72).

### Provenance and scope

| Field | Supplied evidence |
| --- | --- |
| Candidate commit | `849dc704fd596a654d39741df1d9dea1d9ea1ef9` |
| Runner / profile | RUN-TEST.cmd; short-diagnostic; one pass per scenario/backend |
| Planner / seed / quality | viewport-v3-workers / 1315511732 / Eco |
| Engine / renderer | Godot 4.7.2 stable Mono / Compatibility OpenGL |
| GPU / driver | AMD RADEON R9 M395X / 27.20.14540.15002 |
| Whole diagnostic elapsed | 168.9 seconds, including preparation and collection |
| Per-window request | 3 seconds warm-up + 8 seconds capture; eight windows |
| Capture session | 20261103-135153-944451ca |
| Local evidence directory | `H:\WorldForge-Sim\artifacts\acceptance\self-test\20261103-135153-e384afbb` |
| Expected local archive | `H:\WorldForge-Sim\artifacts\acceptance\self-test\20261103-135153-e384afbb.zip` |
| Received source | User-uploaded Pasted text(20261103-135435).txt, 17,690 bytes |
| Received file SHA256 | `d740862f773bde1f8e29e2944f600f1b65657518bde0f4bea4a5b7448424c426` |

The uploaded text includes eight result rows, eight worker-window summaries, and VALID engine log tails for all eight captures. It is a **user-supplied report**, not independent execution by the reviewer. The ZIP, original capture JSON/CSVs, screenshots, local configuration/sidecar manifests and archive hashes were **not supplied or independently inspected**. The archive path above follows the runner's documented naming; archive existence/content is not established merely by this text. Source report times/session labels are preserved without inferring the Windows timezone. Reported elapsed time is not the sum of timed frame windows.

### Reported callback intervals (milliseconds)

| Scenario | Backend | Samples | p50 | p95 | p99 | Maximum |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| idle | direct | 479 | 16.680 | 18.340 | 18.530 | 35.315 |
| idle | region-mirror | 480 | 16.675 | 18.209 | 18.561 | 20.080 |
| pan | direct | 478 | 16.682 | 18.224 | 18.573 | 31.238 |
| pan | region-mirror | 480 | 16.671 | 16.884 | 18.443 | 19.457 |
| zoom | direct | 478 | 16.679 | 18.367 | 18.646 | 31.783 |
| zoom | region-mirror | 443 | 16.723 | 31.707 | 31.886 | 31.988 |
| paint | direct | 457 | 16.691 | 30.490 | 31.791 | 36.456 |
| paint | region-mirror | 447 | 16.694 | 30.817 | 38.555 | 45.336 |

There are **3,742 reported samples** in total. Medians are about 16.7 ms; that corresponds to roughly a 60 Hz median interval, not a measured mean FPS or GPU execution time. All eight reported p95 values are below the provisional 33.3 ms Eco target, and all eight reported maxima are below 50 ms **within these short windows only**. Neither statement certifies sustained performance.

The larger tails are mirror zoom and both paint captures. A single ordered pass cannot establish a backend regression, an improvement over the previous renderer, or its cause. Both backends used the worker planner; this is not a workers-ON versus workers-OFF comparison.

### Reported paint worker window

| Counter | direct | region-mirror |
| --- | ---: | ---: |
| Max retained entries | 2 | 2 |
| Max reserved array bytes | 8,388,608 | 8,388,608 |
| Max ready payload bytes | 298,016 | 329,392 |
| Max admitted upload bytes | 298,016 | 329,392 |
| Max snapshot time, ms | 1.2105 | 7.5639 |
| Max publication time, ms | 7.0668 | 7.2867 |
| Max pump time, ms | 7.0952 | 7.5839 |
| Soft time-budget overruns | 11 | 23 |
| Max failed chunks / coast failures | 0 / 0 | 0 / 0 |

All eight summaries report zero failed chunks and zero coast failures. The 8 MiB value is a managed-array reservation counter, **not total RAM/VRAM usage**. These are independently sampled maxima; they must not be added together or assumed to describe the same frame. The existing 4 ms admission target is soft because one snapshot/native operation cannot be preempted; observed overruns require profiling, not silently increasing that target.

Idle/pan/zoom summaries report zero retained/reserved/ready/upload work and zero snapshot/publication cost during sampled windows. All summaries also report max_active=0, including paint. Those snapshots do not prove no worker ever ran between observations, and do not validate sustained streaming through an expanded world. In particular, the mirror zoom tail cannot be assigned to worker publication from this report.

### Decision and next evidence

- **Complete for this run:** the corrected host runner reaches all eight captures, returns PASS and produces a readable copied report. The earlier preflight/locked-log stop did not recur.
- **Partial physical evidence:** exact host commit/driver and short callback distributions are now known. Keep full acceptance #53 and streaming #55 open; keep `windows_verified_commit` and `user_accepted_visual_commit` unset.
- **Next engineering priority:** triage the measured paint snapshot/publication costs and mirror zoom tail before resident coast/LOD expansion. Start with the existing ZIP; correlate frames/poses/settings and inspect before/after images. Do not request another identical short run merely to reproduce already available summaries.
- **Falsifiable investigation:** test whether paint long intervals coincide with owner-thread edit/snapshot/publication work. If correlation is absent or the missing stage timings cannot resolve it, instrument only the missing phase in a separate opt-in PR. Treat zoom independently; no cache/GPU/worker diagnosis is established here.
- **Still required:** complete raw-bundle integrity/identity review, full-duration repeated matched captures, actual input/live simulation/save/load and visible coverage checks, image review and the user's art/camera verdict. No feature is promoted to default and no visual quality is reduced.

The immediate user action is to attach the **existing ZIP** above in the active project conversation; the same diagnostic need not be rerun first. After raw-bundle review, the already implemented full worker command is `scripts\run-v3-3-acceptance.ps1 -MeshWorkers` (10-second warm-up, 30/60-second windows, three repeats by default). Ordinary-planner comparison omits the switch and must retain all other fixture settings.

## H. Staged publication candidate (V3.4.4 slice 2)

The user approved the preceding short diagnostic milestone and authorized continuation. This does not fill missing full-duration or exact-candidate image/input evidence. [#74](https://github.com/tahazarif10/worldforge-sim/issues/74) / [ADR 0010](decisions/0010-staged-publication-and-frame-work-evidence.md) adds two native admissions per detail and schema 3 work.csv, with correct preceding-callback alignment and correlated slow intervals. Read [the candidate protocol](V3_4_STAGED_PUBLICATION.md) before comparing it to the schema 2 host report above. Publication policy changes fixture identity. Recollect the short RUN-TEST.cmd diagnostic on this candidate; raw old/new ZIP review and repeated longer scenarios are separate acceptance steps. Status: host phase evidence, speed improvement, GPU seams and mirror-zoom attribution **PENDING**.

## I. Received staged-publication host report — 2026-11-04

**Reported result: PASS; all eight short captures VALID. Full visual/input/performance acceptance remains PENDING.** This report supplies the phase summaries requested in section H for the merged staged-publication candidate. It supersedes that outstanding short-report request; the earlier approved report in section G remains historical evidence.

### Provenance and integrity limits

| Field | Supplied evidence |
| --- | --- |
| Candidate commit | `a0fd5131fab345792f6b957b5c98c316d3d52a52` |
| Runner / profile | RUN-TEST.cmd; short-diagnostic; one pass per scenario/backend |
| Planner / seed / quality | viewport-v3-workers / 1315511732 / Eco |
| Engine / renderer | Godot 4.7.2 stable Mono / Compatibility OpenGL 3.3 |
| GPU / driver | AMD RADEON R9 M395X / 27.20.14540.15002 |
| Whole diagnostic elapsed | 210 seconds, including preparation and collection |
| Per-window request | 3 seconds warm-up + 8 seconds capture; paused simulation and scripted actions |
| Capture session | 20261104-123330-745205dc |
| Local evidence directory | `H:\WorldForge-Sim\artifacts\acceptance\self-test\20261104-123329-7429f296` |
| Expected local archive | `H:\WorldForge-Sim\artifacts\acceptance\self-test\20261104-123329-7429f296.zip` |
| Received source | User-uploaded Pasted text(20261104-123607).txt, 26,930 bytes |
| Received file SHA256 | `2e26e2aeac1972ec439f9aa3bb7124a127b1815c2773034a3f1c4922d1592ad1` |

The text contains eight result rows, eight worker summaries, eight frame-work summaries and VALID log records for all eight distinct captures. All frame-work summaries report schema 1, `complete=true`, `invalid_rows=0` and `dropped_rows=0`. Their counts match the result-table samples and worker-window samples, totaling **3,750**. The printed slowest interval in each summary matches its table maximum, and the printed callback plus outside-callback durations reconstruct each supplied slow interval within numerical precision. These are consistency checks of the received summary, not independent validation of the raw traces.

No ZIP, original frame/work CSVs, capture JSON, screenshots or configuration/sidecar manifests were supplied. Full raw-row joins, fixture policy identity, archive integrity and image quality have not been independently reviewed. The expected archive path follows the runner's naming; the text does not prove its existence or contents. Preserve session labels without inferring the Windows timezone. The 210-second elapsed value includes work outside timed capture and is not a frame-time statistic.

### Reported callback intervals (milliseconds)

| Scenario | Backend | Samples | p50 | p95 | p99 | Maximum |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| idle | direct | 481 | 16.677 | 18.369 | 18.553 | 19.340 |
| idle | region-mirror | 481 | 16.679 | 18.409 | 18.557 | 18.645 |
| pan | direct | 479 | 16.673 | 18.406 | 18.530 | 31.783 |
| pan | region-mirror | 479 | 16.677 | 18.443 | 18.577 | 35.086 |
| zoom | direct | 480 | 16.677 | 18.452 | 18.636 | 30.871 |
| zoom | region-mirror | 431 | 16.713 | 31.858 | 31.964 | 32.428 |
| paint | direct | 461 | 16.689 | 18.538 | 31.791 | 35.523 |
| paint | region-mirror | 458 | 16.688 | 21.647 | 35.231 | 40.132 |

All eight reported p95 values remain below the provisional 33.3 ms Eco target and all reported maxima are below 50 ms in these short windows. This does not close the sustained-performance gate or establish physical input latency. Every worker summary reports zero failed chunks and zero coast failures.

### Reported paint work and retained resources

| Counter / sampled maximum | direct | region-mirror |
| --- | ---: | ---: |
| Retained entries / staged chunks | 3 / 1 | 3 / 1 |
| Reserved array bytes | 12,582,912 | 12,582,912 |
| Ready payload bytes | 453,088 | 453,088 |
| Admitted upload bytes | 204,960 | 204,960 |
| Snapshot time, ms | 1.1271 | 7.3728 |
| Publication time, ms | 4.2958 | 4.1488 |
| Terrain phase, ms | 2.7838 | 3.0929 |
| Foliage phase, ms | 1.6147 | 1.9351 |
| Coast phase, ms | 0.5717 | 0.5583 |
| Pump time, ms | 4.3245 | 7.3906 |
| Soft time-budget overruns | 1 | 12 |
| Callback time, ms | 25.937 | 36.196 |
| Action time, ms | 4.755 | 8.242 |
| Visibility time, ms | 22.701 | 30.090 |
| Flush time, ms | 1.311 | 0.778 |

These maxima can come from different callbacks. Visibility contains pump, and pump contains snapshot/publication work; do not sum nested timings or separate maxima. The 12 MiB reservation describes managed numeric arrays, not total native/GPU memory. Sampled `max_active=0` does not mean no workers ran between observations. Idle, pan and zoom summaries report zero retained/reserved/upload work and zero snapshot/native-publication time in their windows.

### Correlated slow rows: paint visibility and mirror zoom

The following six paint rows are the three slowest intervals printed for each backend. All six have **terrain, foliage and coast publication times equal to zero**, while snapshots are included in pump. Values below are rounded to three decimals; `visibility minus pump` is derived from the timings in the same row.

| Backend / sequence | Interval ms | Callback ms | Visibility ms | Pump ms | Visibility minus pump ms | Outside callback ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| direct / 390 | 35.523 | 25.937 | 22.701 | 1.020 | 21.682 | 9.586 |
| direct / 45 | 31.883 | 12.915 | 11.182 | 1.077 | 10.105 | 18.968 |
| direct / 133 | 31.841 | 15.328 | 13.702 | 1.168 | 12.534 | 16.513 |
| region-mirror / 31 | 40.132 | 31.076 | 25.601 | 7.271 | 18.330 | 9.056 |
| region-mirror / 387 | 39.926 | 36.196 | 30.090 | 2.837 | 27.253 | 3.730 |
| region-mirror / 158 | 39.725 | 30.290 | 25.342 | 7.319 | 18.023 | 9.435 |

**Supported inference:** these printed paint stalls contain substantial owner-callback visibility work outside the worker pump, between 10.105 and 27.253 ms per listed row. Native publication is not the measured work in those six callbacks, although publication occurs elsewhere in the capture. Source inspection and finer attribution of the visibility path are the next profiling priority; this summary does not identify the particular visibility operation or explain every part of each interval.

| Mirror zoom sequence | Interval ms | Callback ms | Action ms | Visibility ms | Outside callback ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 7 | 32.428 | 5.765 | 5.193 | 0.519 | 26.663 |
| 72 | 32.027 | 6.172 | 5.594 | 0.519 | 25.855 |
| 199 | 32.007 | 6.121 | 5.520 | 0.526 | 25.886 |

All three mirror-zoom rows report zero snapshot/terrain/foliage/coast time. Their 25.855–26.663 ms outside the measured callback remains **unclassified**. This quantity is not a GPU timer and cannot establish a GPU, cache, VSync, scheduling or backend cause. Treat this tail separately from paint visibility work.

### Comparison and decision

Compared with the single 2026-11-03 report at `849dc704fd596a654d39741df1d9dea1d9ea1ef9`, paint p95 is lower in this run: **30.490 → 18.538 ms direct** and **30.817 → 21.647 ms mirror**. Direct paint p99 remains **31.791 ms**; mirror paint p99 changes **38.555 → 35.231 ms**. Reported publication maxima change **7.0668 → 4.2958 ms direct** and **7.2867 → 4.1488 ms mirror**, with soft-budget overrun counts **11 → 1** and **23 → 12**. Retained maxima also change from **2 to 3 entries**, and array reservations from **8 to 12 MiB** per backend. Mirror zoom retains its longer tail: p95 **31.707 → 31.858 ms**, p99 **31.886 → 31.964 ms**.

These are descriptive differences between two single ordered short runs with changed publication policy and instrumentation. They do not prove a repeatable speed improvement, a backend regression, or causation by staging. Both runs used mesh workers; neither is a workers-ON/OFF comparison. The increased whole-session duration, 168.9 → 210 seconds, includes preparation/collection and does not establish slower gameplay.

- **Received checkpoint complete:** the staged candidate reaches all eight captures and supplies complete reported work summaries. No repeat of this unchanged short diagnostic is needed merely to restate these metrics.
- **Next investigation:** review the existing ZIP's raw frames/work/metadata/images and audit the visibility work outside pump; keep mirror zoom attribution separate. A targeted correction must follow that evidence, while resident coast/LOD expansion remains later work.
- **Still pending:** full raw-bundle verification, repeated full-duration matched scenarios, image/coverage review, actual input, live simulation, save/load and explicit user art/camera acceptance. Keep #53/#55 open and the full-acceptance commit fields unset. No default promotion or visual-quality reduction follows from this report.
