# Repository audit — 2026-11-01

## Scope and evidence boundary

Audited repository head: [bf70b825528dda2f2bf8fee16e9f7245ec016c15](https://github.com/tahazarif10/worldforge-sim/commit/bf70b825528dda2f2bf8fee16e9f7245ec016c15), merged documentation PR #58. The last runtime-code merge before this audit was PR #51, `9dc03c8aa608220971b1da3516074f8fd68508a8`. Those are source references, not an exact SHA certified by a new user-host screenshot.

Work performed: source/docs/workflow review, existing GitHub CI evidence review, and official technical research. **No Windows game run, GPU benchmark, editor-hang reproduction or new user visual acceptance was performed in this audit.** Research recommendations are not implemented runtime features.

## Existing evidence

| Evidence | Exact reference | What it proves | What it does not prove |
| --- | --- | --- | --- |
| Core CI on audited main | [Run 41109317518](https://github.com/tahazarif10/worldforge-sim/actions/runs/41109317518), head `bf70b825528dda2f2bf8fee16e9f7245ec016c15` | Documentation guard, deterministic smoke suite and C# compilation succeeded | GPU speed, screenshots or camera feel |
| Most recent pre-audit engine run | [Run 41105302717](https://github.com/tahazarif10/worldforge-sim/actions/runs/41105302717), head `41eaaba01e9a6678583c376f7f841751ae4506e6` | Official Godot imports/boots at that PR #51 head | A new engine run at audited main or physical Windows acceptance |
| Recorded reference world | Seed `1315511732`; earlier user screenshot described in [state](PROJECT_STATE.md) | Relevant visual reference and mountain composition | Sustained FPS distribution or corrected-summit approval |
| Existing documentation | [PR #58](https://github.com/tahazarif10/worldforge-sim/pull/58) | Roadmap, workflow, architecture, research, acceptance and ADR 0001 already existed | That all planned features are implemented |

The repository already had substantial documentation. This revision fills evidence gaps and fixes ordering/CI coverage; it does not claim the documents were absent.

## Findings and response

Priorities here indicate execution order: **P0** invalidates a decision or acceptance claim; **P1** blocks safe scale-up; **P2** is a later capability/design requirement.

### A01 — P0: current frame samples are engine delta, not a validated wall-clock baseline

**Source:** [VisualLab3D](../Scripts/VisualLab/VisualLab3D.cs), [FrameTimeProbe](../Scripts/Instrumentation/FrameTimeProbe.cs), [FrameTime smoke](../tests/FrameTime.Smoke/Program.cs).

`_Process` passes `delta * 1000.0` into the probe. The 7,200-sample ring exports ordinal indices and engine intervals, without timestamps, reset/scenario identity or explicit overflow metadata. At a hypothetical constant 60 samples/second, capacity covers 120 seconds; a longer capture retains only its tail. This is a capacity calculation, not a measured host rate.

Godot documents caps/smoothing/fixed-FPS effects on delta. Percentile arithmetic tests cannot validate the underlying clock. **Response:** ADR 0007 and #54 require monotonic timestamps, clear capture boundaries and complete-window validation before #53 numerical acceptance. Current F9 remains diagnostic. Runtime probe change is **pending**.

### A02 — P0: direct/mirror comparison needs the same complete scenario

**Source:** [direct verifier](../scripts/verify-v2-art.ps1), [mirror verifier](../scripts/verify-v2-region-parity.ps1), [parity tests](../tests/TerrainBackendParity.Smoke/Program.cs), [camera](../Scripts/Camera3D/StrategyCameraRig.cs).

The direct verifier supports `-WorldSeed`. At the audited head the mirror wrapper accepts only `-NoLaunch` and does not forward a seed. The source parity test covered `20261030, 99, -123456` but not the user mountain seed. Engine CI booted the reference seed and mirror mode separately, not together. Camera pose/trace and visual phase are not exported as a complete reproducible fixture.

**Response in this revision:** extend the existing parity test to seed `1315511732` and add the combined seed+mirror headless case. **Pending #54:** launcher seed forwarding, camera/state/phase capture and replay. Exact screenshots and physical performance remain pending even after the new CI cases pass.

### A03 — P1: a chunk count cap does not bound frame cost

**Source:** [VisualTerrain3D](../Scripts/VisualLab/VisualTerrain3D.cs), [ChunkLoadBudget](../Scripts/Streaming/ChunkLoadBudget.cs).

Visibility is refreshed at roughly 0.05-second intervals; Eco/Balanced use count limits of 2/4 new chunks, and initial population can use 64. Native mesh construction and foliage work occur on the main thread. `FlushChanges` processes dirty resident chunks and refreshes the whole coast mask; the streaming count limit does not itself bound this edit path.

**Risk, not a measured diagnosis:** large edited areas or uploads may cause stalls despite a small chunk limit. **V3.4 response:** measure CPU generation/upload/edit timings separately, coalesce dirty revisions, bound queued arrays/bytes and main-thread upload time, preserve existing geometry until its replacement is ready. A fast continuous pan must not starve still-relevant jobs by invalidating them solely for a changed camera epoch.

### A04 — P1: coast scaling requires exact halos and global coordinates

**Source:** [WaterCoastMath](../Scripts/VisualLab/WaterCoastMath.cs).

`BuildMask` allocates one byte per cell for distance, one for type and four for output RGBA. At hypothetical 8192², array payload is `8192 × 8192 × 6 = 402,653,184 bytes = 384 MiB`, including **256 MiB** output RGBA. Image/native/GPU copies and object overhead are additional. **The current 120×88 scene does not incur this 8192² allocation.**

Current foam has nonzero response at Manhattan land distances 1, 2 and 3. A one/two-cell halo cannot in general reproduce that output at a region edge. Future patches need at least the three-cell input halo for this kernel, exact outer-world boundary behavior and absolute-coordinate noise. Texture filtering may need an additional sampling border; terrain color/elevation kernels need their own derived halos.

**V3.4/V3.5 acceptance:** byte-compare patched/global masks on bounded reference maps, including coast at chunk/region edges and edits. Preserve approved shader appearance.

### A05 — P1: a mirror cache is not independent region authority

**Source:** [WorldState](../src/WorldSim/WorldState.cs), [terrain sources](../src/WorldSim/TerrainSources.cs), [VisualTerrain3D](../Scripts/VisualLab/VisualTerrain3D.cs).

WorldState owns a full terrain array and constrains dimensions to the existing small-world range; the live V2 scene is 120×88. RegionMirror reads it on cache misses. Some terrain corner/color paths still call `_world.Tile` directly.

The current base terrain formula is coordinate/hash based and includes world dimensions through island falloff. Extract it with explicit `seed + bounds + generator version` before inventing a different generator. Sharing only a seed does not preserve art when map bounds or algorithms change.

**V3.5 acceptance:** one region authority for tiles/height/tools/save, no hidden direct-WorldState terrain read, preserved reference-world results, schema-versioned changes for larger-world composition.

### A06 — P1: resident LRU is not a whole-world memory limit

**Source:** [RegionStore](../src/WorldSim/Regions/RegionStore.cs).

The bounded resident map is separate from retained edit/revision/dirty bookkeeping. Edits across many regions can grow metadata/overrides even while resident region count stays under its limit.

**V3.5 response:** account for resident snapshots, overrides, revisions, dirty queues, worker results and GPU/upload memory separately. Define dirty write-back/backpressure before eviction, preserve unsaved edits on I/O failure, and test many-region edit/revisit/save cycles. Never evict dirty state merely to make a counter look bounded.

### A07 — P1: region-file replacement does not commit a whole world

**Source:** [ElevationRegionFiles](../src/WorldSim/Regions/ElevationRegionFiles.cs), existing region codec tests.

Temporary-file replacement and validation help protect individual files. They do not make biome, elevation, entities and world clock one atomic save generation. A partially completed group may mix revisions.

**V3.5 response:** evaluate versioned immutable region files plus an atomic root manifest against SQLite transactions before ADR 0005 is accepted. Both need explicit durability assumptions, bounded I/O and corruption/interruption tests. Preserve the last valid generation and validate the complete candidate before changing live state. A checksum or successful rename is not a universal power-loss guarantee.

### A08 — P2: simulation time, RNG and movement need a regional contract

**Source:** [VisualLab3D](../Scripts/VisualLab/VisualLab3D.cs), [WorldState](../src/WorldSim/WorldState.cs).

Current simulation tick is 0.18 seconds; the frame loop limits catch-up steps and can drop excess backlog. Person rendering updates with simulation steps. WorldState uses seeded `System.Random` and sequential iteration. These facts do not establish cross-version replay or camera-independent regional advancement.

**V3.6 response:** choose stable update/event order, explicit catch-up policy, versioned RNG algorithm/state and saved tick. Separate interpolated drawing from authoritative discrete simulation, with teleport/new-world reset. Define inactive-region semantics; freeze/coarse/event advancement are product decisions, not consequences of visibility. Bound path requests and invalidate paths on terrain revisions. No claim that a checkbox for Godot physics interpolation solves this custom tick loop.

### A09 — P1: engine workflow filters omit runtime surfaces

**Source:** [engine workflow](../.github/workflows/visual-lab-ci.yml), [core workflow](../.github/workflows/core-ci.yml).

At audited head, engine paths include visual-lab scripts but omit camera, streaming, instrumentation, UI, launch scripts and project/SDK configuration.

**Response in this revision:** cover `Scripts/**`, `src/**`, `Scenes/**`, `Assets/**`, `scripts/**`, project/SDK files and the workflow itself; set `permissions: contents: read` in both workflows. Expand the reference-seed mirror regression case. This improves test triggering and least-privilege execution; headless tests still do not measure GPU performance.

### A10 — P1: evidence, decisions and delivery order need clearer ownership

**Source:** existing README, state, roadmap, acceptance docs and issue trackers #52–#57.

The old order could be read as numerical baseline first, reliable scenario harness second. State mentioned planning under review after PR #58 had already merged. Documentation and CI SHAs were easy to confuse with a visually accepted build.

**Response in this revision:** separate audited source, runtime reference, core/engine runs and unknown physical SHA; create a document index, Persian guide, this audit and ADR 0007; refine templates and static documentation checks. Small 512²–1024² living-world/gameplay validation is an explicit route before optional 2048²–8192² expansion.

## What this revision changes

- Current documentation and linked research/acceptance contracts.
- Contributor/issue/PR entry points and project-scoped handling rules.
- Engine trigger coverage, workflow token permissions and reference-seed regression coverage.
- Documentation integrity checks for the new canonical documents and evidence schema.

It does not implement the new measurement runner, regional renderer, transactional world saves, population simulation or new artwork. Those remain explicit deliverables with dependencies in the roadmap. CI results for this revision belong to its PR and exact head; the pre-audit runs above are historical evidence, not reused as a substitute.

## Next implementation slice

Complete #53's fixture specification, then #54's clock/scenario/harness. Verify the known long-stall and scenario-overflow cases, preserve the approved renderer, and obtain the real H:-Windows comparison. Only then promote V3.4 work through the physical gate.

Official technical sources and alternatives are catalogued in [research](V3_TECHNICAL_RESEARCH_2026.md); operational pass/fail conditions are in [acceptance](V3_ACCEPTANCE_MATRIX.md).
