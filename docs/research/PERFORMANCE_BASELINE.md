# PERFORMANCE_BASELINE — evidence, missing measurements and reproducible experiments

**Status: baseline not yet measured on the user's Windows machine.** This is a measurement protocol and source-level complexity audit, not a fabricated benchmark table.

## Available real evidence as of 2026-10-30

| What is known | Evidence | What it does NOT prove |
|---|---|---|
| User's portable Godot 4.7.2 Mono and .NET 10 SDK run from H: | Prior Windows PowerShell build and standalone Godot logs shared in conversation | Does not prove the V2 3D demo can run 60 FPS at 2048² |
| GPU advertises AMD Radeon R9 M395X, Godot Compatibility/OpenGL 3.3 | User standalone launch console | Does not establish actual VRAM capacity, Vulkan feature support or shader performance |
| Simulation smoke and C# Godot project compile succeeded on merged PR #18 | GitHub Actions history; `.github/workflows/core-ci.yml` | Does not run a 3D scene or test shader import, input, FPS |
| User says new V2 3D preview looks somewhat better but is inadequate and motion feels very bad | Current user feedback | Does not quantify frame-time latency, draw calls or where input breaks |
| Previously the **Godot EDITOR**, not game, hung | User clarification, issue #12 | Does not mean editor issue resolved nor that game is CPU-bound |

Current `VisualLab3D.cs:178–188` HUD FPS = `frame count / accumulated delta` and "worst frame" = maximum `_Process(delta)` within a one-second interval. These are useful indicators, but neither reports GPU time, p95/p99 input latency, render submissions or worst frozen intervals where the frame never returns. Do not call the HUD "profiler".

## Source-derived cost hypotheses (not measured)

| Path | Algorithmic / memory risk | Where |
|---|---|---|
| Start new world | WorldSim O(W×H) generation, every terrain chunk built and whole vegetation scan | `WorldState.Generate`, `VisualTerrain3D.BindWorld` |
| Brush changes terrain | Dirty affected 16-cell chunks rebuilt; additionally full vegetation O(W×H) | `VisualTerrain3D.MarkChanged/FlushChanges/RebuildVegetation` |
| User drags brush | Each mouse-move can perform ~1,910 candidate ray steps, then `UpdateActors` allocates a fresh MultiMesh | `VisualLab3D.ApplyAtScreen/UpdateActors` |
| Simulation | `WorldState.Advance` scans all citizens every logical tick; V2 creates new Mesh/MultiMesh each tick | `WorldState.Advance`, `VisualLab3D._Process/UpdateActors` |
| Rendering large map | All 16×16 tile chunks created; no stream/LOD; water one world-scale plane | `VisualTerrain3D.BindWorld` |

## Required baseline matrix

For each run record `git SHA`, engine build, project rendering method, GPU driver string, display resolution, world seed, map size, time speed, zoom/camera pose, shadows ON/OFF, use of Quaternius GLB or placeholders, run type (standalone or Editor), duration, exact user actions and whether profiler was active.

| Scenario | Map | People | Actions and sample window | Evidence required |
|---|---|---|---|---|
| BASE-01 | 120×88 | 0 | Start + idle 60 sec | first-boot time, warmed FPS/p95/p99, main CPU/RAM, GPU metrics if available |
| BASE-02 | 120×88 | 0 | 30 sec continuous camera right-drag | input latency, frame spikes, cursor-to-terrain tracking |
| BASE-03 | 120×88 | 0 | 30 sec rapid wheel zoom / keyboard pan | p95/p99, camera drift/pixels |
| BASE-04 | 120×88 | 0 | 20 sec painting Ocean/Forest/Mountain | CPU chunk build time, tree rebuild count, frame spikes |
| BASE-05 | 120×88 | 100 | 60 sec movement at 1× and 4× | per-tick allocations, actor update and FPS |
| BASE-06 | 120×88 | 450 | near V2 actor cap / pause vs running | worst frame + build count + memory |
| BASE-07 | 120×88 | same as above | shadows F2 ON/OFF A/B | GPU shading influence |
| BASE-08 | 512×512 | 0 | **after** region system exists, flythrough | resident chunks, load time, memory and p99 |
| BASE-09 | 2048² → 8192² | varied | **future** sparse streaming stress | active set bounded / correctness / save load |

Do not edit `WorldSim` dimensions to 2048² directly: current ctor has 1024 max and the architecture is full-memory. BASE-08/09 require implementation first; mark "not runnable", not "failed" or "passed".

## Metrics contract

- FPS: frames/second, rolling 1 sec.
- Frame timing: monotonic stopwatch elapsed per frame; capture **every frame** into a bounded ring buffer, calculate mean/median/p95/p99 and max on an offline copy. Record an apparent full freeze separately because `_Process` resumes after stall.
- CPU: total process usage normalized by logical CPU count, process working set/private bytes. Host sampler includes editor and game processes if both running: **label PID**.
- GPU: Godot `Performance.GetMonitor` draw calls, 3D objects, primitives, memory where provided; labels must be sourced from actual engine version. Not all monitors work on Compatibility or release builds; render thread/GPU timers may be unavailable and must be recorded `N/A`.
- Geometry: number of active chunks by LOD, mesh surfaces/vertices, shadow casters, batch count, instance count; distinguish triangles submitted from visible and occluded.
- Latency: high-resolution input event timestamp, event-to-render target screen change; tools need explicit instrumentation before quantifying.
- Edit: CPU time `GenerateChunk`, `RebuildVegetation`, `ApplyBrush`, GPU upload count/bytes, deferred queue wait.
- Streaming: queue depth, stale task cancellation count, bytes per upload, time-to-visible after teleport, cache hit rate.
- Simulation: active regions/entities vs background aggregations, ticks per second and replay divergence.

Use a reproducible **fixed seed 20261030**, a fixed camera pose and fixed zoom/resolution for A/B tests. Run warmup before recording; repeat >=3 passes and report dispersion.

## Windows H:-only starter commands

Run in Windows PowerShell. Existing user changes to `project.godot` must be preserved; never force switch, clean or hard-reset. Perform research benchmark on a branch only if local working tree is safe.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git log -1 --format='%h %s'
. .\scripts\enter-dev.ps1

# Current V2 standalone; no editor debug overhead:
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-visual-lab.ps1
```

In a **second** PowerShell console (same H: directory), while the game is open, start the existing bounded host sampler:

```powershell
Set-Location 'H:\WorldForge-Sim'
. .\scripts\enter-dev.ps1
.\scripts\capture-host-performance.ps1 -Seconds 60 -IntervalSeconds 2
# Inspect generated CSV files without moving them to C:
Get-ChildItem '.\artifacts\performance\godot-host-*.csv' | Sort-Object LastWriteTime -Descending | Select-Object -First 3
```

The CSV includes process CPU and working-set/private memory but **does not calculate game frame timing**. The in-game FPS/worst-frame HUD should be screenshotted with identical camera location. If the game fails, inspect `H:\WorldForge-Sim\artifacts\visual-lab\godot-3d-console.log` and report exact error. Avoid starting Godot editor interactive mode unless testing editor issue #12 separately.

To measure p95/p99, add a separate **instrumentation-only** PR (not completed yet): lightweight C# `Stopwatch.GetTimestamp()` per frame into a preallocated ring buffer, exported to `artifacts\performance\V2-frame-times-*.csv` on controlled user stop/hotkey. Include measured time of `RenderChunk`, `UpdateActors`, `RebuildVegetation`; avoid writing to disk every frame. Include app version, GPU, seed, projection and controls header. A Windows performance capture and timestamps are necessary to validate.

## Budget-setting method

Do **not** promise 60 FPS from compile result. The provisional targets to investigate are:
- **Quality compatibility tier:** near 60 FPS if supported by R9 M395X; if measured hardware cannot, consider stable 30 FPS with attractive art as a legitimate lower tier. Test at 1280×800 first.
- Candidate 60 FPS budget: 16.67 ms frame; aim p95 <=16.7 ms, p99 <=33 ms for rare spikes **after** profiling. These are aspirational thresholds, not evidence.
- Candidate 30 FPS budget: 33.33 ms frame; aim p95 <=33.3 ms, p99 <=50 ms. High input latency must still be addressed.
- Reserve CPU budgets for input/simulation/streaming and main-thread mesh uploads; allocate from measured actual frame breakdown, not arbitrary percentages.
- Set a **streaming latency** acceptance target after a 512² MVP measures queue/mesh upload. Avoid accidental "fast" claims from warm caches.

## Interpretation decision tree

- Camera feels wrong at stable FPS and no p99 spikes -> **input geometry bug**, not optimization.
- Frame spikes correlated with painting -> profile full RebuildVegetation/mesh commit.
- Main thread high CPU with stable GPU -> optimize ray/picking, allocations, C# scheduling.
- GPU saturated when shadows ON -> lower shadow distance/caster count, prefer hierarchical LOD, not flat no-art fallback.
- FPS stable in standalone but Editor hangs -> track issue #12 separately; don't optimize game render pipeline to conceal an editor bug.
- Some graphics settings unavailable -> Compatibility feature mismatch, not evidence of code crash without logs.

## Missing evidence that only the user host can provide

Actual game FPS/frametimes, AMD driver version, Vulkan/D3D support, GPU VRAM capacity, editor crash logs, actual GLB content/format/license, all 1280×800 screenshots, visual/interaction feedback, high-zoom camera precision and 512²+ performance after implementation. The research environment did **not** execute Godot against the user's GPU.

**Sources:** [Godot 3D optimization](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html), [Godot Performance monitors](https://docs.godotengine.org/en/latest/classes/class_performance.html), [Godot profiler](https://docs.godotengine.org/en/latest/tutorials/scripting/debug/the_profiler.html).
