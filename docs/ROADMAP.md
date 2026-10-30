# WorldForge Sim — engineering master roadmap

> Source of truth for **future milestone order and release gates**, 2026-10-30.
> This replaces the obsolete 2D/pixel-art-first roadmap. For what is **actually
> implemented**, see [PROJECT_STATE.md](PROJECT_STATE.md). Proposed numbers,
> features and budgets below are targets, not claims that the game has them.

## 0. Product contract

WorldForge Sim is an **original** 3D stylized god-sandbox and living-world simulation, inspired by the *genre* of WorldBox, not its proprietary code, art or game content. Target: unusually readable and attractive miniature worlds with mountains, coastlines, forests, creatures, autonomous villages/civilizations and user powers; comfortable on older Windows GPUs.

**Keep the currently approved 3D scene as the release baseline:**
- Scene: \`Scenes/VisualLab3D.tscn\`, authoritative gameplay \`WorldState\` currently **120×88**; default scene \`Scenes/Main.tscn\` and user-modified \`project.godot\` remain untouched.
- Original \`VisualTerrain3D\` renderer and \`StrategyCameraRig\`, opaque stylized ocean/coast shader, land color/relief, Kenney CC0 authored nature, F4 Eco, deterministic people and brushes.
- Known rejected path: \`Scenes/RegionalWorldLab3D.tscn\` from old V3.0. It **must not become the release scene**; #46 stays open until proven visual AND control parity on Windows. Technical 8192² addressability did not demonstrate a playable large world.
- Canonical hardware/OS: Godot **4.7.2 Mono/.NET 10**, AMD **Radeon R9 M395X**, OpenGL **Compatibility**, project \`H:\WorldForge-Sim\`. Never overwrite/reset dirty local \`project.godot\` or intentionally redirect project caches to C:.
- No Codex/Work by default; scoped source changes through GitHub branch/PR, pure .NET and Godot CI. Account credits cannot be inferred from tool names; check Usage independently.

## 1. Status legend — do not conflate implementation and acceptance

| Symbol | Meaning |
| --- | --- |
| ✅ CI-verified and merged | Source exists on \`main\` and named CI passed; **not** automatic real-machine acceptance |
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
| Recovery | V3 launcher defaults back to approved V2; optional \`WorldState\`-backed \`RegionStore\` cache inside V2 renderer | [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47), [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) CI-verified; physical V2 vs mirror FPS/drag comparison open |
| V3.2 art correction | Removed oversized flat mountain slab decorations; 66 -> 9 mountain accents for the user's \`1315511732\` seed; optional exact seed replay | [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50), [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) CI green; actual Windows summit screenshot approval still **pending** |

## 3. Dependency-ordered milestone plan

**No milestone skips.** An engineering slice can merge behind an OFF-by-default flag after CI; user-facing promotion requires the appropriate Windows visual/input/performance gate. Each numbered sub-slice should have one issue, branch, focused tests, evidence bundle, reviewer-ready PR and state update.

### ▶ V3.3 — Freeze / measure the approved 3D baseline (NEXT)

**Purpose:** create a real *measured* golden baseline before touching streaming or global world dimensions. Do not implement more world-size or camera changes first.

**V3.3.0 — Lock the reference scene and art contract**
- Fix representative world seeds: \`1315511732\` (user's mountain ridge) and \`20261030\` (default), plus one new randomized seed recorded verbatim. State that \`20261030\` may contain **zero mountains**.
- Lock 1280×800 window screenshot/camera pose, orthographic size, Eco/Balanced mode and fixed camera interaction script; H:-only screenshot and frame exports.
- Capture official user's *actual* V2 approved scene and new mountain fix. F8 screenshots and F9 CSV; do not claim automated pixel matching until reference images and matching camera poses exist.
- Characterize baseline: CPU frame time, wall-clock frame intervals, p50/p95/p99/max, RAM/VRAM where available, terrain/foliage rebuild and GC, click->camera response.
- Fix reference record: Windows build, Godot/.NET version, branch SHA, GPU driver, viewport, zoom, seed, quality flag, elapsed warm-up, exact test commands.

**V3.3.1 — Instrumentation / parity harness**
- Add **one-command PowerShell 5.1-compatible, H:-only** acceptance runner (future file \`scripts/run-v3-3-acceptance.ps1\`, **not yet present**); avoid modifying \`project.godot\`.
- Compare direct \`WorldState\` and opt-in \`RegionMirrorTerrainSource\` in the SAME V2 renderer/camera at the SAME seed; F8 image and F9 frame samples. No separate V3 scene.
- Record input-latency samples (right-drag, mouse-anchored wheel, brush screen-to-ground), missing geometry counts, artifact hashes and an explicit operator **accept/reject** field.
- Verify F2–F12 shortcuts, painting, citizen spawn and meteor, mid-frame camera edits, repeated new-world and failure recovery.

**V3.3.2 — Triage any measured regression**
- Only if measurements indicate a bottleneck, isolate CPU mesh generation, main-thread Godot mesh upload, shader/fill cost, dynamic shadow cost, GLB instancing, image mask rebuild or C# GC.
- One falsifiable hypothesis per diagnostic PR. Profile first; then make the smallest reversible change, and repeat matched scenarios.
- Submit before/after frame distribution and screenshots; **never claim performance gains merely from fewer rocks or theoretical allocations**.

**V3.3 exit gate:** complete real Windows AMD baseline, readable images, no unacceptable camera drift, exact controls working, no lost terrain brush edits, image/FPS evidence recorded; user explicitly approves visual baseline. #46 remains open if V3-like large map visual/UX parity still unproven.

### ◻ V3.4 — Seamless visual streaming *without visible holes*

**Dependency:** V3.3 physical baseline and camera/art acceptance.

**V3.4.1 — Pure viewport planner and residency contract**
- Extract exact shared visible-footprint planner from current \`VisualTerrain3D.UpdateVisibility\` and \`StrategyCameraRig\`. Camera can move or zoom; border margin and elevation bounds conservatively cover screen.
- Define \`TileKey\`, \`ChunkCoord\` (16×16 display), \`RegionKey\` (64×64 data), \`ChunkGeneration\`, explicit \`Unloaded/Queued/Generating/Ready/Stale/Disposed\`.
- Bounded nearest-first / latest-camera-wins queue. A superseded request cannot publish after zoom/seed change. No per-frame unbounded work. Record count of **uncovered visible pixels/cells**.

**V3.4.2 — Guaranteed distant fallback**
- *Before* removing previous geometry, present low-detail terrain+opaque ocean proxy covering every in-view region. Detailed chunk replaces matching proxy only when ready. No arbitrary 48-chunk cap without fallback.
- Keep V2 terrain color palette/coast silhouette at low LOD; reduce far decoration, **not near-camera approved art**.
- Verify rapid pans, zoom-in/out, window resize and teleport at map limits never show the blank background where ground/water should be.

**V3.4.3 — Immutable worker mesh-data snapshots**
- Off-main-thread compute only **pure C# arrays** from immutable versioned region snapshots: vertices, indices, colors, normals, foliage placements. NO Godot \`SceneTree\`, \`MeshInstance3D\`, \`SurfaceTool\` or GPU texture operations in workers.
- Main thread owns Godot mesh/material/nodes and uploads a bounded number/bytes or measured milliseconds per frame. Cancel/discard stale generations.
- Buffer capacity, world-seed generation identity, bounded memory accounting, teardown/join and background worker failure handling must be tested.

**V3.4.4 — LOD, seams and coast parity**
- Start with LOD0 (approved near 16×16), LOD1 (moderate distant geometry), LOD2 (low-detail fallback). **Prototype and benchmark thresholds**, do not hardcode arbitrary GPU assumptions.
- Ensure coincident shared edge heights, LOD seam stitching/skirting, exact pointer pick of visible near geometry and coast mask computed from bounded region halos, not an 8192² RGBA texture.
- Keep opaque V2 shader style, water motion/foam colors and Kenney per-chunk MultiMesh. No old flat-shader V3 fallback.

**V3.4 exit:** never a blank visible tile at normal gameplay zoom; bounded queue/residency and uploads under worst-case camera movement; no material visual degradation; Windows p95/p99 and subjective drag/zoom gate within approved V3.3 baseline tolerance.

### ◻ V3.5 — Make terrain regions authoritative, then expand map size

**Dependency:** V3.4 seamless streaming and physical acceptance.

**V3.5.1 — Generator art parity and immutable region contract**
- Build a new **deterministic V2-compatible regional generator** from shared seed/absolute coordinates, independent of region arrival/eviction/edit order. Do not point the live game at the unrelated V3.0 \`RegionGenerator\`.
- \`RegionStore\` becomes terrain authority only after generated tiles, coastline, shared-corner elevation and scatter are identical to the approved baseline on the reference worlds, or have an explicitly approved art migration.
- Define regional revision, world-generation ID, immutable read snapshot, negative/global coords and halo ownership. No direct mutable \`WorldState\` reference in production regional baseline.

**V3.5.2 — Height and tool integration**
- Integrate existing sparse elevation offsets and canonical absolute shared vertices across 64-cell borders. LOD and picking must reflect exact edited surface.
- Port every tool (land, ocean, sand, forest, mountain, sculpt, meteor) through versioned command transactions and region invalidation; unchanged user-facing brush radius/behavior.
- Test boundary cells, negative coords where allowed, repeated edits, undo/redo semantics (if introduced), unload/reload and water transitions.

**V3.5.3 — Crash-resilient world saves**
- Explicit versioned manifest: \`worldId\`, \`seed\`, schema, checksum, region revisions, entity revisions and sequence. Ordered atomic commit/rollback across regions; per-region file rename alone is **not** a multi-file transaction.
- Validate corruption/truncation, wrong seed/world ID, interrupted multi-region save, version upgrade and backup recovery without destroying last accepted save. Define path sandbox H: for development.

**V3.5.4 — Evidence-driven scale ladder**
- 120×88 → 256² → 512² → 1024² → 2048² → 4096² → 8192² *logical and actively playable* only when streaming, visual fidelity, save/load and actual AMD performance gates pass each scale.
- Never allocate full terrain height/biome/water arrays for 8192². Measure peak resident regions, chunks, physical RAM/VRAM, startup time, active agent tick and disk footprint at each step.
- If a scale fails, STOP and profile; do not mask the failure by shrinking the near field, replacing sea art or limiting draw distance into visible holes.

**V3.5 exit:** region source becomes the actual source of truth for terrain/tools/save; user has approved camera/coast/forest/mountain parity at progressively larger sizes. No claim of full living 8192² simulation yet.

### ◻ V3.6 — Region-scoped living world

**Dependency:** V3.5 authoritative regions and durable saves.

**V3.6.1:** stable long-lived citizen/entity IDs, position in region/global coords, per-region ownership and cross-border transfer (exactly once), spatial indexes and render batches; no per-citizen \`Node3D\` by default.

**V3.6.2:** deterministic bounded active-area simulation at fixed tick, time budget/backpressure, deferred inactive-region advancement policy, reproducible RNG stream per entity/region. A player editing a remote region cannot accidentally advance the entire world at the wrong rate.

**V3.6.3:** save citizen/animal AI, needs and movement across unload/reload; verify no duplicate/lost entities at borders, storms/meteor events apply once, and cross-region events replay deterministically.

**V3.6 exit:** existing V2 person spawn/meteor/time controls behave the same on a tested 512²–1024² region world; bounded tick cost, no entity loss, Windows measurements. [Existing entity issue #45](https://github.com/tahazarif10/worldforge-sim/issues/45) remains blocked until V3.4–V3.5 gates.

### ◻ V3.7 — Art & UX uplift without abandoning Compatibility

**Dependency:** measured V3.3 baseline; can prototype isolated assets while V3.4 work continues.

**Slices:** artist-authored premium forest/rocks (test Quaternius Standard free CC0 pack as *candidate*, never blindly import); procedural variation by biome; better summit silhouette with reference screenshots; directional animations for people/animals, selection and feedback; scalable HUD and accessibility; ambient VFX/audio with limits. Keep 10 Kenney CC0 model fallback. 
**Exit:** user approves side-by-side art, CC0 provenance and real AMD frame time. No VFX or shading feature requiring Forward+ in the Compatibility baseline.

### ◻ V4 — Autonomous ecology, settlements and kingdoms

**Dependency:** reliable V3.6 entity/terrain simulation and versioned persistent world.

| Slice | User-visible world behavior | Minimum deterministic proof |
| --- | --- | --- |
| V4.1 Ecology | Animals, food, trees/resources, weather/biome effects, needs/aging | Bounded population, resource conservation and repeatable ticks |
| V4.2 Settlement | Gathering/building jobs, homes, village founding, roads/ownership | No double-spend, bounded job scheduler, cross-region transfer |
| V4.3 Kingdoms | Borders, leaders, diplomacy, conflict and alliances | Reproducible state transitions, pathfinding at region borders |
| V4.4 Powers | Distinct original god powers, disaster aftermath, ecology reactions | Single-application events, visual and simulation parity |
| V4.5 Presentation | Distinct architectural assets, animated interactions, notifications | Camera/selection and visual readability at scale |

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
| G0 — no destructive default | \`Main.tscn\`, \`project.godot\`, V2 shader/assets unchanged unless explicitly approved | Do not merge |
| G1 — deterministic correctness | .NET tests incl. boundary/seed/edit/cache/state and exact failure-case checks | Fix before PR merge |
| G2 — engine boot | Official Godot **4.7.2 Mono** imported GLBs, C# compiled, same V2 scene boots headlessly | Fix before PR merge |
| G3 — visual art parity | Exact-seed matched F8 screenshots (shoreline/forest/summit), user approval | Keep new mode OFF |
| G4 — camera/control | Right-drag/zoom-at-cursor, painting, picking, citizens, no blank visible ground under stress | Keep new mode OFF |
| G5 — low-end performance | Windows **R9 M395X** frame p50/p95/p99, 30s/60s repeated scenarios, VRAM/RAM if measurable | Profile; no FPS claim |
| G6 — persistence | Crash/corrupt/wrong-seed save recovery across region boundaries | Do not call save production-ready |
| G7 — provenance/release | Original/CC0 license, no external proprietary assets, release SHA and docs | Block public release |

**Provisional performance objectives (not achieved claims):** Eco mode aims for sustained ≳30 FPS at 1280×800 on the R9 M395X. For perspective, 33.33 ms/frame corresponds to 30 FPS and 16.67 ms to 60 FPS. A stronger p95 objective is <=33.3 ms with no recurring >100 ms stalls during pan/zoom; thresholds must be compared with physical V2 baseline and adjusted only by recorded decision. FPS HUD 1-second averages **cannot substitute for p95/p99 CSV**. A lower detail/LOD tier must not produce missing geometry or destroy near-field art.

## 5. Repeatable local commands and required evidence

Preflight **before modifying source**:

\`\`\`powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin
\`\`\`

Do not run \`git reset --hard\`, \`git clean\` or overwrite \`project.godot\`. If \`git switch main\` is blocked, stop and reconcile the local change first. To run the accepted V2 screenshot world **after safely updating main**:

\`\`\`powershell
git switch main
git pull --ff-only origin main
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732
\`\`\`

In the running game: **F8** H:-based PNG; **F9** frame-time CSV; **F10** chunk diagnostics; **F4** Eco/Balanced; **F6** slope comparison. The new master acceptance runner named in V3.3 is a **planned deliverable**, not a command users can run today.

Each milestone evidence bundle shall record: Git branch/SHA, seed, camera angle/size, 1280×800 (or actual recorded viewport), Eco/Balanced, Windows/GPU/driver, Godot/.NET, 30–60s matched action trace, p50/p95/p99/max, RAM/VRAM if observable, counts of active/pending chunks, screenshot paths, CI run IDs, result FAIL/PASS/PENDING for **each** gate, known limitations and the user's art/UX verdict.

## 6. Process

Follow [Engineering Workflow](ENGINEERING_WORKFLOW.md), [Architecture](V3_ARCHITECTURE.md), [Research](V3_TECHNICAL_RESEARCH_2026.md), [Acceptance](V3_ACCEPTANCE_MATRIX.md) and [ADRs](decisions/README.md). Every scope change that affects GPU pipeline, data authority, threading, save format, queue/residency semantics or milestone order requires an ADR. Keep issue #46 open until direct/mirror parity and new streaming behavior are accepted on real Windows.
