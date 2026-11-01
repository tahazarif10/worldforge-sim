# WorldForge Sim — engineering master roadmap

> Source of truth for **future milestone order and release gates**, reviewed 2026-11-01.
> This replaces the obsolete 2D/pixel-art-first roadmap. For what is **actually
> implemented**, see [PROJECT_STATE.md](PROJECT_STATE.md). Proposed numbers,
> features and budgets below are targets, not claims that the game has them.

## 0. Product contract

WorldForge Sim is an **original** 3D stylized god-sandbox and living-world simulation, inspired by the *genre* of WorldBox, not its proprietary code, art or game content. Target: unusually readable and attractive miniature worlds with mountains, coastlines, forests, creatures, autonomous villages/civilizations and user powers; comfortable on older Windows GPUs.

**Keep the currently approved 3D scene as the release baseline:**
- Scene: `Scenes/VisualLab3D.tscn`, authoritative gameplay `WorldState` currently **120×88**; default scene `Scenes/Main.tscn` and user-modified `project.godot` remain untouched.
- Original `VisualTerrain3D` renderer and `StrategyCameraRig`, opaque stylized ocean/coast shader, land color/relief, Kenney CC0 authored nature, F4 Eco, deterministic people and brushes.
- Known rejected path: `Scenes/RegionalWorldLab3D.tscn` from old V3.0. It **must not become the release scene**; #46 stays open until proven visual AND control parity on Windows. Technical 8192² addressability did not demonstrate a playable large world.
- Canonical hardware/OS: Godot **4.7.2 Mono/.NET 10**, AMD **Radeon R9 M395X**, OpenGL **Compatibility**, project `H:\WorldForge-Sim`. Never overwrite/reset dirty local `project.godot` or intentionally redirect project caches to C:.
- No Codex/Work by default; scoped source changes through GitHub branch/PR, pure .NET and Godot CI. Account credits cannot be inferred from tool names; check Usage independently.

## 1. Status legend — do not conflate implementation and acceptance

| Symbol | Meaning |
| --- | --- |
| ✅ CI-verified and merged | Source exists on `main` and named CI passed; **not** automatic real-machine acceptance |
| 🟨 Source merged, Windows gate missing | Needs exact local host screenshot / mouse behavior / FPS / data evidence |
| ▶ Next recommended slice | Planned first implementation, **not yet coded** |
| ◻ Planned | Dependencies and proposed acceptance defined, implementation not begun |
| ⛔ Withdrawn | Failed user-facing release; source may remain as engineer-only data experiment |
| ⛔ Blocked | Cannot progress until named evidence/gate passes |

Do not mark a feature Accepted merely because the Godot scene starts headlessly. Record evidence links, exact code SHA, host hardware, conditions, actual outcome and remaining deviations per slice.

## 2. Historical foundation, verified state

| Track | Implemented capability | Evidence / remaining gate |
| --- | --- | --- |
| M0/M1 | .NET deterministic simulation, original Godot prototype, HUD, painting, people, meteor, tick speed; standalone Windows launcher | Initial functional evidence; 2D pixel-art direction rejected |
| V2.0–V2.6 | Real 3D orthographic camera, chunked editable geometry, sculpted shoreline shader, 10 bundled Kenney CC0 models, Eco/Balanced quality and instrumentation | Multiple merged PRs; individual screenshot/perf claims vary |
| V2.7/V2.8 | Center relief and shared-corner continuous fan terrain; matched camera ray/brush picking | [PR #38](https://github.com/tahazarif10/worldforge-sim/pull/38), [PR #40](https://github.com/tahazarif10/worldforge-sim/pull/40); Windows perf still needed |
| V2.9 | Sparse 64×64 signed quantized elevation offsets, explicit H: save/load, opt-in F7 sculpt | [PR #42](https://github.com/tahazarif10/worldforge-sim/pull/42); not a full-world height/simulation save |
| V3.0 | Independent 8192² logical region prototype | [PR #44](https://github.com/tahazarif10/worldforge-sim/pull/44) **WITHDRAWN FOR GAMEPLAY**: bad art/camera, no citizens, missing visible chunks; [regression #46](https://github.com/tahazarif10/worldforge-sim/issues/46) |
| Recovery | V3 launcher defaults back to approved V2; optional `WorldState`-backed `RegionStore` cache inside V2 renderer | [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47), [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) CI-verified; physical V2 vs mirror FPS/drag comparison open |
| V3.2 art correction | Removed oversized flat mountain slab decorations; 66 -> 9 mountain accents for the user's `1315511732` seed; optional exact seed replay | [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50), [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) CI green; actual Windows summit screenshot approval still **pending** |

## 3. Dependency-ordered milestone plan

**Respect dependencies and evidence gates.** An engineering slice can merge behind an OFF-by-default flag after applicable CI; user-facing promotion requires the relevant Windows visual/input/performance gate. Each sub-slice has one issue, focused tests, a reviewable PR, evidence and a state update. Optional large-map tiers are not prerequisites for a complete small-world gameplay loop.

The [2026-11-01 audit](REPOSITORY_AUDIT_2026-11-01.md) explains current code gaps. [ADR 0007](decisions/0007-measurement-evidence-and-scenario-identity.md) resolves measurement ordering:

| Order | Deliverable | Completion evidence |
| --- | --- | --- |
| 1 — #53 fixture | Fix seeds, camera start/actions, viewport/quality; capture reference art | Reproducible fixture and images; numerical performance may remain pending |
| 2 — #54 instrumentation | Monotonic clock, scenario boundaries, seed/pose parity, H: runner | Long-stall/reset/overflow tests, applicable engine CI, complete raw capture |
| 3 — #53 host acceptance | Matched direct/mirror runs on R9 M395X | Valid isolated distributions, screenshots and user camera/art verdict |
| 4 — #52 closure | Record baseline, deviations and accepted tolerances | All V3.3 physical gates satisfied; no inference from headless CI |

Do not require a completed numerical baseline to begin the harness needed to measure it. Screenshot review can proceed immediately.

### ▶ V3.3 — Freeze / measure the approved 3D baseline (NEXT)

**GitHub tracker:** [#52](https://github.com/tahazarif10/worldforge-sim/issues/52). Initial slices: [#53 V3.3.0](https://github.com/tahazarif10/worldforge-sim/issues/53) and [#54 V3.3.1](https://github.com/tahazarif10/worldforge-sim/issues/54).

**Purpose:** create a real *measured* golden baseline before touching streaming or global world dimensions. Do not implement more world-size or camera changes first.

**V3.3.0 — Lock the reference scene and art contract**
- Fix representative world seeds: `1315511732` (user's mountain ridge) and `20261030` (default), plus one new randomized seed recorded verbatim. State that `20261030` may contain **zero mountains**.
- Lock 1280×800 window screenshot/camera pose, orthographic size, Eco/Balanced mode and fixed camera interaction script; H:-only screenshot and frame exports.
- Capture the user's actual V2 reference scene and corrected summit. Screenshots can establish the art fixture now. Finish quantitative acceptance only after V3.3.1 supplies valid clock/scenario samples; legacy F9 was a rolling engine-delta diagnostic, not that final evidence.
- After instrumentation: measure monotonic callback intervals, p50/p95/p99/max, count of stalls, mesh/upload/edit cost and GC; RAM/VRAM only where observable. Label handler-to-camera timing separately from physical input-to-display latency.
- Fix reference record: Windows build, Godot/.NET version, branch SHA, GPU driver, viewport, zoom, seed, quality flag, elapsed warm-up, exact test commands.

**V3.3.1 — Instrumentation / parity harness**

**Implementation update:** the first capture/fixture/runner foundation is now present; [guide and limitations](V3_3_ACCEPTANCE_RUNNER.md). The requirements below remain the full acceptance contract; real input latency, live simulation and user-host verification are still pending.
- Add **one-command PowerShell 5.1-compatible, H:-only** acceptance runner (implemented file `scripts/run-v3-3-acceptance.ps1`; see [runner guide](V3_3_ACCEPTANCE_RUNNER.md)); avoid modifying `project.godot`.
- Compare direct `WorldState` and opt-in `RegionMirrorTerrainSource` in the SAME V2 renderer/camera at the SAME seed; F8 image and F9 frame samples. No separate V3 scene.
- Replace acceptance use of `_Process delta` with monotonic intervals (`Time.GetTicksUsec` or consistently used `Stopwatch`); keep engine delta separately. Add session/scenario/elapsed timestamps, warm-up/start/end/reset, explicit sample overflow and p50/p95/p99 conventions.
- Forward the same optional seed through the mirror launcher; capture/replay camera pivot/size/actions, simulation tick/speed and animated render phase. Record build configuration, VSync/FPS cap, slope mode, renderer/driver and local configuration differences.
- Record application input-handler-to-camera intervals, missing geometry counts, artifact hashes and an explicit operator **accept/reject** field. Do not call application timing physical mouse-to-photon latency.
- Validate with a fake-clock long stall, scenario reset, ring wrap/overflow, mismatched metadata and missing output cases. Capture screenshots/export outside timed intervals or mark their overhead. Keep sample memory bounded and reject incomplete windows.
- Verify F2–F12 shortcuts, painting, citizen spawn and meteor, mid-frame camera edits, repeated new-world and failure recovery.

**V3.3.2 — Triage any measured regression**
- Only if measurements indicate a bottleneck, isolate CPU mesh generation, main-thread Godot mesh upload, shader/fill cost, dynamic shadow cost, GLB instancing, image mask rebuild or C# GC.
- One falsifiable hypothesis per diagnostic PR. Profile first; then make the smallest reversible change, and repeat matched scenarios.
- Submit before/after frame distribution and screenshots; **never claim performance gains merely from fewer rocks or theoretical allocations**.

**V3.3 exit gate:** complete real Windows AMD baseline, readable images, no unacceptable camera drift, exact controls working, no lost terrain brush edits, image/FPS evidence recorded; user explicitly approves visual baseline. #46 remains open if V3-like large map visual/UX parity still unproven.

### ◻ V3.4 — Seamless visual streaming *without visible holes*

**Tracker:** [#55](https://github.com/tahazarif10/worldforge-sim/issues/55).

**Dependency:** V3.3 physical baseline and camera/art acceptance.

**V3.4.1 — Pure viewport planner and residency contract**
- Extract exact shared visible-footprint planner from current `VisualTerrain3D.UpdateVisibility` and `StrategyCameraRig`. Camera can move or zoom; border margin and elevation bounds conservatively cover screen.
- Define `TileKey`, `ChunkCoord` (16×16 display), `RegionKey` (64×64 data), `ChunkGeneration`, explicit `Unloaded/Queued/Generating/Ready/Stale/Disposed`.
- Bound the queue by desired footprint plus margin and prioritize current camera demand. Hard-reject world/data/LOD-format mismatches; a camera move reprioritizes or cancels irrelevant work but must not discard a still-wanted valid chunk solely because CameraEpoch advanced. Test continuous pan without starvation. Record **uncovered visible pixels/cells**.

**V3.4.2 — Guaranteed distant fallback**
- *Before* removing previous geometry, present low-detail terrain+opaque ocean proxy covering every in-view region. Detailed chunk replaces matching proxy only when ready. No arbitrary 48-chunk cap without fallback.
- Keep V2 terrain color palette/coast silhouette at low LOD; reduce far decoration, **not near-camera approved art**.
- Verify rapid pans, zoom-in/out, window resize and teleport at map limits never show the blank background where ground/water should be.

**V3.4.3 — Immutable worker mesh-data snapshots**
- Off-main-thread compute only **pure C# arrays** from immutable versioned region snapshots: vertices, indices, colors, normals, foliage placements. NO Godot `SceneTree`, `MeshInstance3D`, `SurfaceTool` or GPU texture operations in workers.
- Main thread owns Godot mesh/material/nodes and controls uploads with count, bytes and measured time budgets. Dirty paint/sculpt rebuilds and coast updates enter the same bounded pipeline; the current count-only visibility budget does not cover all edit work. Cancel/discard invalid world/data generations.
- Buffer capacity, world-seed generation identity, bounded memory accounting, teardown/join and background worker failure handling must be tested.

**V3.4.4 — LOD, seams and coast parity**
- Start with LOD0 (approved near 16×16), LOD1 (moderate distant geometry), LOD2 (low-detail fallback). **Prototype and benchmark thresholds**, do not hardcode arbitrary GPU assumptions.
- Ensure shared edge heights, LOD seam stitching/skirting and exact near-geometry picking. Current foam requires at least a three-cell input halo, absolute-coordinate noise and separate consideration of texture filtering borders. Compare patch/global masks at boundaries. A whole 8192² BuildMask would use 384 MiB of array payload including 256 MiB output RGBA, before native/GPU copies.
- Keep opaque V2 shader style, water motion/foam colors and Kenney per-chunk MultiMesh. No old flat-shader V3 fallback.

**V3.4 exit:** never a blank visible tile at normal gameplay zoom; bounded queue/residency and uploads under worst-case camera movement; no material visual degradation; Windows p95/p99 and subjective drag/zoom gate within approved V3.3 baseline tolerance.

### ◻ V3.5 — Make terrain regions authoritative, then expand map size

**Tracker:** [#56](https://github.com/tahazarif10/worldforge-sim/issues/56).

**Dependency:** V3.4 seamless streaming and physical acceptance.

**V3.5.1 — Generator art parity and immutable region contract**
- Extract a **deterministic V2-compatible regional generator** from seed, absolute coordinates, explicit world bounds and generator version. The current island falloff uses width/height; preserve it at reference dimensions before approving a new large-world composition. Region visitation/eviction order must not affect values. Do not substitute the unrelated V3.0 generator.
- `RegionStore` becomes terrain authority only after generated tiles, coastline, shared-corner elevation and scatter are identical to the approved baseline on the reference worlds, or have an explicitly approved art migration.
- Define revisions, world identity, immutable snapshots, supported coordinate bounds and halo ownership. Remove direct WorldState tile/color reads from the renderer when switching authority. Bound resident snapshots, edited overrides, revision metadata, dirty queues and pending arrays; LRU resident count alone is insufficient. Dirty eviction requires durable write-back or explicit backpressure.

**V3.5.2 — Height and tool integration**
- Integrate existing sparse elevation offsets and canonical absolute shared vertices across 64-cell borders. LOD and picking must reflect exact edited surface.
- Port every tool (land, ocean, sand, forest, mountain, sculpt, meteor) through versioned command transactions and region invalidation; unchanged user-facing brush radius/behavior.
- Test boundary cells, negative coords where allowed, repeated edits, undo/redo semantics (if introduced), unload/reload and water transitions.

**V3.5.3 — Crash-resilient world saves**
- Accept ADR 0005 after comparing versioned immutable files + root manifest with SQLite transactions. Record dependency/I/O/backup tradeoffs and Windows filesystem durability assumptions. Required save metadata includes world ID, seed, bounds, generator/schema/RNG versions, region/entity revisions and simulation clock. Per-region rename alone is not a multi-region transaction.
- Validate corruption/truncation, wrong seed/world ID, interrupted multi-region save, version upgrade and backup recovery without destroying last accepted save. Define path sandbox H: for development.

**V3.5.4 — Evidence-driven scale ladder**
- Initial acceptance ladder: 120×88 → 256² → 512², then 1024² when the host passes. V3.6 and a V4.0 gameplay loop may proceed on an accepted 512²–1024² world. Treat 2048² → 4096² → 8192² as optional later scale experiments, each requiring visual, save/load, simulation and AMD evidence; they are not prerequisites for villages or a useful game.
- Never allocate full terrain height/biome/water arrays for 8192². Measure peak resident regions, chunks, physical RAM/VRAM, startup time, active agent tick and disk footprint at each step.
- If a scale fails, STOP and profile; do not mask the failure by shrinking the near field, replacing sea art or limiting draw distance into visible holes.

**V3.5 exit:** regions own terrain/tools/save, complete-generation recovery works and a 512²–1024² tier has actual camera/coast/forest/mountain acceptance. Record the largest accepted size separately from logical addressability. Do not wait for 8192² to start V3.6.

### ◻ V3.6 — Region-scoped living world

**Tracker:** [#57](https://github.com/tahazarif10/worldforge-sim/issues/57), continuation of existing [#45](https://github.com/tahazarif10/worldforge-sim/issues/45).

**Dependency:** V3.5 authoritative regions and durable saves.

**V3.6.1:** stable long-lived citizen/entity IDs, position in region/global coords, per-region ownership and cross-border transfer (exactly once), spatial indexes and render batches; no per-citizen `Node3D` by default.

**V3.6.2:** deterministic fixed-tick simulation with stable update/event ordering, bounded work and explicit catch-up/backpressure. Specify offscreen advancement; save tick, RNG algorithm/version/state and event sequence. System.Random seed alone is not a cross-runtime replay contract. Separate render interpolation from custom simulation ticks; reset interpolation on spawn, teleport and New World. A camera move must not silently redefine simulation time.

**V3.6.3:** save citizen/animal AI, needs and movement across unload/reload; verify no duplicate/lost entities at borders, storms/meteor events apply once, and cross-region events replay deterministically.

**V3.6.4 — Navigation and jobs:** begin with a bounded logical tile/region graph using terrain snapshots. Stagger path requests, cache by terrain revision, handle unreachable targets and cross-region transfers, and avoid rebuilding navigation from detailed visual meshes on every edit. Compare hierarchical routing only when measured path cost requires it. Keep Godot navigation as an evaluated adapter option, not a mandatory core dependency.

**V3.6 exit:** existing V2 person spawn/meteor/time controls behave the same on a tested 512²–1024² region world; bounded tick cost, no entity loss, Windows measurements. [Existing entity issue #45](https://github.com/tahazarif10/worldforge-sim/issues/45) remains blocked until V3.4–V3.5 gates.

### ◻ V3.7 — Art & UX uplift without abandoning Compatibility

**Dependency:** measured V3.3 baseline; can prototype isolated assets while V3.4 work continues.

**Slices:** artist-authored premium forest/rocks (test Quaternius Standard free CC0 pack as *candidate*, never blindly import); procedural variation by biome; better summit silhouette with reference screenshots; directional animations for people/animals, selection and feedback; scalable HUD and accessibility; ambient VFX/audio with limits. Keep 10 Kenney CC0 model fallback. 
**Exit:** user approves side-by-side art, CC0 provenance and real AMD frame time. No VFX or shading feature requiring Forward+ in the Compatibility baseline.

### ◻ V4 — Autonomous ecology, settlements and kingdoms

**Dependency:** reliable V3.6 entity/terrain simulation and versioned persistent world.

| Slice | User-visible world behavior | Minimum deterministic proof |
| --- | --- | --- |
| V4.0 First complete loop | Spawn a small population, gather one resource/food, build shelter, survive, respond to one power, save/reload and continue | Repeatable small-world scenario; no lost jobs/resources/entities; readable feedback and user fun/clarity review |
| V4.1 Ecology | Animals, food, trees/resources, weather/biome effects, needs/aging | Bounded population, resource conservation and repeatable ticks |
| V4.2 Settlement | Gathering/building jobs, homes, village founding, roads/ownership | No double-spend, bounded job scheduler, cross-region transfer |
| V4.3 Kingdoms | Borders, leaders, diplomacy, conflict and alliances | Reproducible state transitions, pathfinding at region borders |
| V4.4 Powers | Distinct original god powers, disaster aftermath, ecology reactions | Single-application events, visual and simulation parity |
| V4.5 Presentation | Distinct architectural assets, animated interactions, notifications | Camera/selection and visual readability at scale |

V4.0 runs on the accepted small-world tier. Defer multiplayer, complex diplomacy/economy, huge population claims and new rendering backends until the core loop is coherent. Expand one interacting system at a time; save migrations and performance budgets accompany each addition.

No proprietary WorldBox art, music, code or game data.

### ◻ V5 — Production quality and early access readiness

**Dependency:** stable V4 core fun loop, user-approved visuals and performance.

- V5.1 user-facing settings, save/version migration, accessibility and localization.
- V5.2 multi-hour soak, crash recovery, memory leaks, idle/offscreen cost, deterministic replay and save backups.
- V5.3 Windows packaging, GPU tiers and install/uninstall; signed installers only if actual signing is available, no fabricated certifications.
- V5.4 tutorial/onboarding, original sound/art licensing ledger and content checklist.
- V5.5 reproducible release candidate, rollback and stable channel.

## 4. Non-negotiable gate matrix

| Gate | Required evidence | Failure action |
| --- | --- | --- |
| G0 — no destructive default | `Main.tscn`, `project.godot`, V2 shader/assets unchanged unless explicitly approved | Do not merge |
| G1 — deterministic correctness | .NET tests incl. boundary/seed/edit/cache/state and exact failure-case checks | Fix before PR merge |
| G2 — engine boot | Official Godot **4.7.2 Mono** imported GLBs, C# compiled, same V2 scene boots headlessly | Fix before PR merge |
| G3 — visual art parity | Exact-seed matched F8 screenshots (shoreline/forest/summit), user approval | Keep new mode OFF |
| G4 — camera/control | Right-drag/zoom-at-cursor, painting, picking, citizens, no blank visible ground under stress | Keep new mode OFF |
| G5 — low-end performance | Windows **R9 M395X** frame p50/p95/p99, 30s/60s repeated scenarios, VRAM/RAM if measurable | Profile; no FPS claim |
| G6 — persistence | Crash/corrupt/wrong-seed save recovery across region boundaries | Do not call save production-ready |
| G7 — provenance/release | Original/CC0 license, no external proprietary assets, release SHA and docs | Block public release |

**Provisional performance objectives (not achieved claims):** Eco mode aims for sustained ≳30 FPS at 1280×800 on the R9 M395X. For perspective, 33.33 ms/frame corresponds to 30 FPS and 16.67 ms to 60 FPS. A stronger p95 objective is <=33.3 ms with no recurring >100 ms stalls during pan/zoom; thresholds must be compared with physical V2 baseline and adjusted only by recorded decision. FPS HUD averages and an unlabeled legacy engine-delta CSV **cannot substitute for complete monotonic scenario captures**. A lower detail/LOD tier must not produce missing geometry or destroy near-field art.

## 5. Repeatable local commands and required evidence

Preflight **before modifying source**:

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin
```

Do not run `git reset --hard`, `git clean` or overwrite `project.godot`. If `git switch main` is blocked, stop and reconcile the local change first. To run the accepted V2 screenshot world **after safely updating main**:

```powershell
git switch main
git pull --ff-only origin main
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732
```

In the running game: **F8** H:-based PNG; **F9** frame-time CSV; **F10** chunk diagnostics; **F4** Eco/Balanced; **F6** slope comparison. The [V3.3 acceptance runner](V3_3_ACCEPTANCE_RUNNER.md) is now implemented. Run `scripts/run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1` for the first host bundle; physical approval remains pending.

Each milestone evidence bundle shall record: Git branch/SHA, seed, camera angle/size, 1280×800 (or actual recorded viewport), Eco/Balanced, Windows/GPU/driver, Godot/.NET, 30–60s matched action trace, p50/p95/p99/max, RAM/VRAM if observable, counts of active/pending chunks, screenshot paths, CI run IDs, result FAIL/PASS/PENDING for **each** gate, known limitations and the user's art/UX verdict.

## 6. Process

Follow [Engineering Workflow](ENGINEERING_WORKFLOW.md), [Architecture](V3_ARCHITECTURE.md), [Research](V3_TECHNICAL_RESEARCH_2026.md), [Acceptance](V3_ACCEPTANCE_MATRIX.md) and [ADRs](decisions/README.md). Every scope change that affects GPU pipeline, data authority, threading, save format, queue/residency semantics or milestone order requires an ADR. Keep issue #46 open until direct/mirror parity and new streaming behavior are accepted on real Windows.


## 7. Delivery control

| Planning horizon | Ready work | Exit before taking the next item |
| --- | --- | --- |
| Immediate | #53 fixture specification + #54 monotonic/scenario core | Meaningful timing/identity tests and documented capture format |
| Next focused PR | #54 launcher/pose parity and H: evidence runner | Valid complete outputs, failure handling, applicable CI; physical gates still explicit |
| Host checkpoint | #53 matched direct/mirror runs | Real art/control verdict and valid repeated measurements; #52 can then close |
| Following architecture | #55 planner/fallback, then bounded jobs/LOD | Coverage and performance acceptance before #56 authority changes |
| Product checkpoint | #56/#57 at 512²–1024², then V4.0 | A durable, understandable living-world loop before optional maximum scale |

Assign an implementer and evidence reviewer in each issue; do not invent assignees or delivery dates. Estimate the next small slice after its source audit, then revise from observed throughput. Scope changes must record their impact on dependencies and acceptance. The active stage is not complete while its physical gate is pending.
