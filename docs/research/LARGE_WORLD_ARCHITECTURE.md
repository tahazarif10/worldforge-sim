# LARGE_WORLD_ARCHITECTURE — spatial world model, streaming and scalable render

**Objective:** grow from current 120×88 V2 demonstration to maps of 512² / 2048² / 4096² / 8192² cells and eventually larger, without a second foundational rewrite. A giant world is **not** all loaded, fully detailed and fully simulated at once.

## Why the current architecture fails before huge maps

- \`WorldState.cs:23–34\` explicitly rejects dimensions greater than 1024 and allocates \`Terrain[]\` for **all** cells.
- \`WorldState.cs:107–126\` generates all terrain eagerly; \`:69–90\` walks the full citizen list every tick.
- \`VisualLab3D.cs:101–113\` instantiates one small 120×88 world; no region data abstraction.
- \`VisualTerrain3D.cs:71–75\` builds every 16×16 visual chunk during world creation, then \`:203–286\` recreates vegetation over the full grid whenever a cell region changes.
- No persistence for player-modified terrain, no disk format, no level-of-detail, no view-driven chunk manager, no background scheduling.
- \`MultiMeshInstance3D\` reduces draw calls but Godot does not cull instances within the same MultiMesh individually. Segment by **spatial region and LOD**. [Godot official](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html).

## World-size cost: raw data only, not actual application footprint

Assume **1 byte** biome per cell and **2 bytes** quantized height per cell, not counting rivers, roads, entities, object graphs, world edits, terrain mesh, textures, GPU resources, streaming caches, compression or runtime overhead.

| World cells | Cell count | 1-byte biome | 2-byte height | Number 64×64 regions | Meaning |
|---|---:|---:|---:|---:|---|
| 120×88 | 10,560 | ~0.010 MiB | ~0.020 MiB | 4 (with edge regions) | Present V2 demo |
| 512×512 | 262,144 | 0.25 MiB | 0.50 MiB | 64 | Functional region MVP |
| 2048×2048 | 4,194,304 | 4 MiB | 8 MiB | 1,024 | Large |
| 4096×4096 | 16,777,216 | 16 MiB | 32 MiB | 4,096 | Very large |
| 8192×8192 | 67,108,864 | 64 MiB | 128 MiB | 16,384 | Streaming stress |

The simplistic memory numbers are small compared with *rendering all geometry*: one top quad per cell requires two triangles, so 8192² fully detailed already means ~134 million top-face triangles, **before** cliffs/vegetation/shadows. Never create that whole mesh or one Godot node per cell.

Current 16×16 mesh chunks would produce 262,144 chunk objects at 8192², before LOD. Scale requires region-level logical storage, demand-driven visual chunk creation and far-field representations.

## Recommended separation: authoritative model, view, and cache

\`\`\`mermaid
flowchart TB
  USER[Brush and camera input] --> CMD[WorldCommand / tick engine]
  CMD --> SIM[Simulation service + deterministic region scheduler]
  SIM --> DATA[IWorldStore: 64x64 canonical region snapshots + edit journal]
  DATA <--> DISK[H:-based world saves: region files + manifest]
  SIM --> EVENTS[RegionChanged / EntityChanged versioned events]
  CAM[CameraPose / zoom and viewport] --> VIS[VisibilityPlanner / screen-space LOD]
  VIS --> QUEUE[Priority job queue + cancel/version checks]
  DATA --> QUEUE
  EVENTS --> QUEUE
  QUEUE --> CPU[Worker: pure CPU arrays, mesh positions, vegetation instance lists]
  CPU --> MAIN[Main-thread resource upload budget]
  MAIN --> RENDER[ChunkRenderer: terrain + water + props per LOD]
  VIS --> RENDER
  EVENTS --> ENT[EntityPresentation: regional pooled instances]
  CAM --> RENDER
  RENDER --> STATS[Frame metrics: resident chunks, uploads, draw calls]
\`\`\`

No worker touches the active Godot SceneTree or unsafely mutates GPU resources. Godot explicitly warns against scene-tree mutation from worker threads and resource/GPU interactions that can stall render synchronization. [Official thread safety](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html).

## Fundamental coordinate and data contracts

\`\`\`csharp
public readonly record struct CellCoord(long X, long Y);
public readonly record struct RegionKey(long X, long Y);
public readonly record struct LocalCell(int X, int Y); // [0,63]

public interface IWorldStore {
    // Access by region, no full-world allocation.
    ValueTask<RegionSnapshot> ReadRegionAsync(RegionKey key, CancellationToken ct);
    ValueTask<CommitResult> ApplyAsync(WorldCommand command, CancellationToken ct);
    long WorldRevision { get; }
}

public sealed record RegionSnapshot(
    RegionKey Key, long Revision,
    ReadOnlyMemory<byte> TerrainKinds,
    ReadOnlyMemory<ushort> Heights,
    ReadOnlyMemory<byte> Moisture);
\`\`\`

Canonical world cell-to-region transformation must handle **negative** coordinates with floor division (not C# integer truncation toward zero). For 64-cell regions, coordinate -1 belongs to region -1 with local X=63. Add unit tests. Quantize height to \`ushort\` only when range/precision requirements justify; use physical units and a common origin across region boundaries. Terrain height is **not** equal to biome ID. Maintain a seeded region generator with deterministic seam sampling and versioned noise parameters.

Represent edits as commands \`BrushPaint(center, radius, kind, logicalTick)\`, \`SpawnPerson\`, \`Meteor\`; commit at tick boundaries or in serialized order with region revisions. A brush spanning region boundaries must atomically update all affected regions or journal one transaction with explicit replay order.

## Hierarchy and region sizes

- **Canonical data region**: initial suggestion **64×64 logical cells**, adjustable via format version. At 8192² this yields 16,384 persistent regions, but only a limited subset loaded in memory.
- **Terrain render chunk**: near layer 32×32 or 64×64 cells. Make chunk size configurable and benchmark 16/32/64 on R9 M395X: draw calls vs regeneration latency vs culling quality.
- **Storage bundle**: archive, e.g. 4×4 regions per compressed region file, to avoid excess tiny files. Disk layout keyed by region IDs; one manifest + version and journal.
- **Ghost ring**: include one-cell border around sampled data for normals, shoreline/coast blending and seamless height derivatives; multiple samples if interpolation footprint exceeds one cell.
- **Quality path**: independently vary render detail, not authoritative WorldSim cells.

**Estimate** (not a benchmark): at 8192², 64×64 world-store regions = 16,384. Keeping all of them as thousands of Godot nodes would still be wrong; render only near/visible pages.

## Terrain representation alternatives

| Technique | When suitable | Risks / conclusion |
|---|---|---|
| Single global high-res mesh | Tiny lab demo only | O(world area) cost, no streaming or LOD: reject |
| **Chunked heightfield mesh + selective cliff modules** | Terrain with editable height and controlled slopes | Good Godot C# path; near LOD meshes on CPU, versioned tile updates; **select** |
| Full marching-cubes voxel terrain | Caves, overhangs, diggable 3D geology | High meshing + storage cost for entire god world; out of scope until gameplay requires |
| Quadtree terrain LOD | Large continuous outdoor surfaces | More crack/stitch logic; good follow-up after near chunk path |
| GPU geom clipmap | Huge uniform heightfields, low CPU throughput | More shader complexity; weak Compatibility baseline without GPU compute/RenderingDevice |
| Virtual texturing | Gigantic high-detail ground texture | GPU/platform-specific; postpone until measured texture pressure |
| Hybrid near 3D / far biome raster | User zooms from creature to global map | **Necessary strategic-view LOD**: at far zoom show aggregate biome color/roads/borders, not millions of tiny tree meshes |

Selected baseline: CPU-generated heightfield chunk meshes using arrays/ArrayMesh, artist-directed materials and sculpted feature modules (rocks/cliffs), distance-aware LOD; for extreme overview use a lightweight aggregated world raster with region-level data. Godot describes ArrayMesh as marginally faster than SurfaceTool for procedural meshes, though harder to work with. [Godot procedural geometry](https://docs.godotengine.org/en/latest/tutorials/3d/procedural_geometry/index.html).

## LOD plan (screen-space, not arbitrary fixed distances)

- **Near / LOD0**: readable trees, collision-accurate cliff profiles, detailed material and unit silhouettes.
- **Mid / LOD1**: reduced terrain vertex density, batched simplified tree meshes, merged clutter, reduced shadows.
- **Far / LOD2**: region-level height/color mesh with tree impostor clusters, no tiny units, no individual clutter.
- **Global map**: 2D strategic overlay/overview texture generated from *same* WorldStore data; camera transitions between 3D and strategic representation without loading the whole world geometry.

Select using projected pixel error at current camera \`Size\`, viewport resolution, tilt and object bounds; add hysteresis to avoid LOD flipping while moving. Avoid seam cracks via shared boundary height samples, edge stitching or skirts, and explicit border tests.

Godot supports imported mesh LOD and visibility ranges, but when applying LOD to a single MultiMesh all instances may use the same LOD at once. [Godot mesh LOD](https://docs.godotengine.org/en/latest/tutorials/3d/mesh_lod.html).

## Chunk streaming state machine

\`\`\`
UNLOADED
  -> REQUESTED(priority, generation, cameraEpoch)
  -> CPU_GENERATING (immutable region snapshot)
  -> CPU_READY(revision, bytes, bounds)
  -> UPLOADING (main-thread budget / GPU resource creation)
  -> RESIDENT(LOD0/1/2, lastUsed)
  -> RETIRING (delay to avoid camera oscillation)
  -> UNLOADED
\`\`\`

Rebase/retarget job priorities on camera movement; after camera teleports, discard stale requests using \`cameraEpoch\` and cancel tokens, not by submitting unlimited background work. Version each region edit; if stale CPU mesh returns after a brush mutation, discard and rebuild latest. User brush changes should get higher priority than distant world generation.

Use **bounded worker count** (initially 1–2; measure) for pure CPU procedural generation and mesh arrays. Main thread commits meshes, resource changes and scene mutations under strict per-frame limits. Never call GPU texture updates from worker tasks. Profiler must measure wait times and queue backlogs.

Caching:
- Active visible chunks + small predictive ring; prefetch along camera velocity.
- LRU or clock eviction with hysteresis; separate CPU region cache vs GPU chunk cache.
- Persist dirty region snapshots/journals before eviction.
- Bound memory via measured budgets for low/high GPU tiers; don't invent fixed VRAM values for R9 M395X.

## Vegetation and citizens

**Plants:** deterministic seeded scatter keyed \`(seed,region,biome,variant)\`, independent of simulation RNG. Each region/LOD owns material/mesh-keyed \`MultiMesh\` batches. Build only the **changed region** after paint, reuse unchanged buffers. Near imported models can be attached/instanced only for selected hero sites; far/mid vegetation must be batched. Per-chunk frustum culling is available; no per-instance culling inside a MultiMesh. Limit shadow casting to close-range canopy/trunks and prefer baked/cheap AO at distance.

**People:** keep canonical citizens keyed by 64-bit ID and region. Use spatial grid/entity buckets to update visible region presentation; small cell movement generates a region-local transform delta, not new meshes or full people list reallocation. Near-view animation may interpolate positions between logical ticks, but the authoritative \`WorldSim\` tick remains deterministic. Distant populations use aggregated counts/icons, not 100k node graphs. Coarse out-of-view simulation must have explicitly designed determinism and reconciliation rules (one RNG stream per region/event, stable event order).

## Persistence, determinism and threading

- World manifest: schema version, seed, generator version, cell units, region size, world dimensions/infinite flag, asset palette version.
- Compressed region snapshot: heights, biome IDs, moisture and optional sparse overrides, entity state.
- Append-only command journal with checksums, monotonic revision and periodic compaction. Crash recovery replays only committed commands.
- Region boundary edits and cross-region citizens resolved in stable coordinate/order.
- Migration scripts must run on copied H:-based save directories first; never overwrite user worlds until verification.
- Rendering versions are **separate** from simulation save schema.

## Precision policy

At 8192×8192 **one-unit cells**, ordinary 32-bit render coordinates are likely usable near origin for basic terrain and tile picking, but visual jitter must be measured at maximum pan/zoom and at high local offsets. **Logical coordinates always use int64**. Use render-origin-relative positions to avoid growing large single-precision vectors. Only adopt Godot double-precision engine builds if measured requirements exceed camera-local floating-origin strategy; official docs note rebuild and performance implications. [Godot large world coordinates](https://docs.godotengine.org/en/stable/tutorials/physics/large_world_coordinates.html).

## Scale and acceptance ladder

1. **120×88 baseline**: ensure same behavior and screenshot; instrument.
2. **512×512**: region storage, chunk culling, bounded queue and edit journal; no full-region scan from small brush.
3. **2048×2048**: camera flythrough with resident chunks capped, explicit p95/p99 and memory curves; correctness after region eviction/restore.
4. **4096×4096**: distant jump, deterministic relaunch, edge region edits; representative 10k entities with only visible subset rendered.
5. **8192×8192+**: stress architecture with *sparse region residency*, not fully detailed geometry; simulation level-of-detail and save/load bench.

Acceptance gates: no unbounded resident chunk growth, no stale mesh after edits, no seams at all LOD levels, deterministic save/load, bounded per-frame uploads, quality consistent with LOD. Set exact FPS/memory thresholds after reference hardware baseline, not before.

## Core source references

- [Godot MultiMesh culling limitations](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html)
- [Godot 3D LOD/culling](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html)
- [Godot thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html)
- [Godot mesh LOD](https://docs.godotengine.org/en/latest/tutorials/3d/mesh_lod.html)
- [Godot large world coordinates](https://docs.godotengine.org/en/stable/tutorials/physics/large_world_coordinates.html)
