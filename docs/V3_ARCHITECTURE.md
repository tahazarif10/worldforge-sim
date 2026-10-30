# V3 architecture — one visual pipeline, region-backed data

**Status:** target architecture (research + design), 2026-10-30. Existing source is explicitly identified; the remainder is **NOT IMPLEMENTED**. Superseding the following contracts requires a recorded ADR and the V2 parity evidence.

## 1. Known source snapshot

| Element | Current implementation | Owner / risk |
| --- | --- | --- |
| Visual scene | `Scenes/VisualLab3D.tscn`; `Scripts/VisualLab/VisualLab3D.cs` | Stable user-approved visual/UX foundation |
| Camera | `Scripts/Camera3D/StrategyCameraRig.cs`; terrain triangle DDA | Must remain the only pan/zoom/brush algorithm |
| Visual terrain | `Scripts/VisualLab/VisualTerrain3D.cs` 16×16 chunks, shared-corner fans, original shoreline shader, per-chunk Kenney batches | Reuse; *do not recreate separate second renderer* |
| Terrain authority | `WorldSim.WorldState` 120×88 single-array world, population and tools | Small-world authoritative gameplay; region migration pending |
| Opt-in parity data | `ITerrainTileSource`, `DirectWorldTerrainSource`, `RegionMirrorTerrainSource` (PR #49) | Second source is **cache of WorldState**, not independent huge world |
| Regional biome data | `WorldSim.Regions.RegionStore`, 64×64 byte cells with bounded LRU, sparse edits, snapshots / `BiomeRegionFiles` | Prototype generator differs from V2 art; must not blindly use |
| Regional elevation | `ElevationRegionStore`: 64×64 quantized signed height deltas at global corner positions, H:-based snapshot files | Live V2 integration for sculpt, not yet transactional whole-world save |
| World-scale preview | `RegionalWorldLab3D.tscn` at 8192² logical addresses | **WITHDRAWN / user-rejected** for missing terrain and camera/art regression |
| CI | Core .NET smoke tests + official Godot 4.7.2 Mono Linux scene boots | Necessary, cannot establish Windows FPS/visual quality |

## 2. Target dependency direction

```text
User commands: pan / zoom / brush / powers / pause
          |
          v
StrategyCameraRig + InputAdapter (THE APPROVED V2 CODE)
          |                                  +-- UI / GameHud
          v                                  |
VisualTerrain3D (THE APPROVED V2 LOOK) <-----+
  |  near: shared-corner terrain, authored mountain/forest
  |  near: stylized opaque ocean+coast / Kenney chunk batches
  |  far: compatible low-LOD proxy + coast silhouette
  |  scene + GPU state only on main thread
  |
  v
IVisibleChunkSource / IWorldTerrainSource [PROPOSED]
  |                |          |
  |                |          +--> immutable density/elevation/shore halo
  |                +--> versioned chunk snapshots
  +--> region read and edit interface
          |
          v
RegionCatalog / RegionCache (64x64) [PROPOSED AUTHORITY]
  |  generator: same art as V2, seed+absolute-coordinate stable
  |  no per-chunk global tile allocations
  |  16x16 visual chunk belongs to one 64x64 region
  |  border halo / canonical vertex heights / revisions
  |
  +--> Biome + Elevation + Weather + Resources
  +--> EntityRegionStore + bounded fixed-step simulation [FUTURE]
  +--> SaveJournal/Manifest + checksum/version [FUTURE]

Workers [FUTURE]: immutable region snapshot -> pure managed arrays
Main thread [EXISTING/FUTURE]: ArrayMesh / MultiMesh / water shader upload
```

### Non-negotiable ownership

1. **Only one authoritative tile value.** During V3.3 WorldState is authority and RegionMirror is a strictly validated read cache. In V3.5 the versioned region store must be authority and WorldState replaced by an adapter for compatibility; never maintain two independently editable copies.
2. **Separate coordinate systems.** `CellCoord(long X,long Y)` and `RegionKey` are canonical logical positions. Render positions in Godot `Vector3` are **camera-relative** only when floating-origin work later justifies it (8192² floats can still be precise enough for one-unit tiles; verify). Convert through one well-tested transform.
3. **Logical partition vs display partition.** 64×64 persistent data regions ≠ 16×16 near-mesh chunks. Each visual chunk needs exactly the terrain snapshots and 1-cell neighbor halo necessary for color, foam, corner heights, brush pick.
4. **Frame versioning.** `WorldGenerationId` changes on New World / load; `ChunkRevision` / `RegionRevision` on edits; `CameraEpoch` when queued viewport work becomes obsolete. A queued result cannot update scene if any token mismatch. No simultaneous active world generations.
5. **No SceneTree off thread.** Workers may calculate arrays only. Native Godot scene objects, texture updates, array upload and shading changes remain on main thread. Use bounded queue and deterministic disposal.

## 3. Terrain generator and parity

**Problem discovered by PR #49:** the V3 RegionGenerator hash classifier yields different biomes from WorldState, even at identical world seed and position. A simple type adapter was not enough. The successful parity prototype samples the real V2 world as the baseline. To make regions truly authoritative:

- Extract V2 terrain generation *functionally*, not a 120×88 output snapshot. Identify any full-world procedural passes (coast, smoothing, random placement) and choose an algorithm with bounded halo or hierarchical global seeds.
- Preserve reference seeds (20261030 and 1315511732) for exact baseline screenshots, unless a separate user-approved world format version explicitly migrates the art.
- For deterministic generator: `CellCoord + Seed + GenerationSchemaVersion` → terrain biome, elevation, decor seed. Region visitation/eviction order must not consume a global RNG stream. For older WorldState maps requiring global passes, provide a versioned migration and full comparison, not a silent change.
- Multi-region edits become atomic commands over sorted regions, with revision validation and localized rebuild; avoid full ocean mask rebuild as width grows.
- Height vertices belong to one **absolute world lattice coordinate**, even when four adjacent cells or regions use that corner. Write authority consistently: no duplicates with independent rounding.

### Model simulation data sample (target, not implemented)

```csharp
record struct WorldId(Guid Value);
record struct ChunkKey(long X, long Y, int Lod);
record struct RegionRevision(ulong Value);
record struct RequestStamp(WorldId World, long WorldEpoch,
                           ChunkKey Chunk, RegionRevision Revision,
                           long CameraEpoch);
// Pure worker input: immutable byte[] biomes + short[] height halo;
// Pure worker output: Vertex[] + int[] indices + DecorationPlacement[].
// Godot array/mesh objects instantiated ONLY on the main thread.
```

A useful proof: generate an arbitrary edited region, unload it, generate unrelated regions, return, and compare exact terrain, coast and inhabitants against a never-evicted reference. Also compare rendering and picking across ±64 boundaries.

## 4. Streaming without blank geometry

**Rejected V3 flaw:** a 48 chunk cap removed tiles *without any fallback*. User sees holes; the camera feels disconnected as new chunks build slowly. Never repeat it.

Proposed state machine:

```text
Unseen -> Requested -> CPUReady -> UploadQueued -> GPUReady
                |                |            |
                +------ stale ---+------------+--> Discarded
    world epoch / revision / camera epoch validation on each transition
```

- A renderable **coarse** near/far proxy must exist for every screen-visible terrain/water position. If a detailed mesh isn't ready, show the proxy rather than the empty backdrop.
- Detail priority is screen-space error, camera proximity and current tool interaction, not blind FIFO.
- Rebuild dirty chunks once per edit burst; combine overlapping invalidations with halo and revision checks.
- Bound work by both **number** of chunks and *measured* milliseconds/bytes. Existing fixed `2 Eco/4 Balanced chunks per 50ms` is a starting control, not evidence of stable latency.
- Retain old visible geometry until a replacement is valid; avoid `QueueFree` of an old chunk before a replacement exists for that area.
- Workers use fixed capacity / coalesced latest camera requests; cancel on new world or branch, dispose of old arrays, main-thread-only install. State machine tests must cover errors, stale results, cancellation and rapid reverse-camera travel.

### LOD modes for evaluation (not chosen as fixed numbers)

| Band | Representation | Rule |
| --- | --- | --- |
| Near LOD0 | existing 4-face fan mountain/slope, exact coast and authored tree/rock batches | Always match approved V2 at camera focus |
| Middle LOD1 | lower-density terrain with same biome palette/shore silhouette, less decoration | Must not lose coastline shape or create gaps |
| Far LOD2 | coarse topology and opaque sea with cheap local shoreline approximation | Always rendered instead of an empty cell |

Godot automatically-generated imported mesh LOD is for imported mesh assets and screen-space selection, **not** a guarantee of crack-free LOD transitions for runtime-generated terrain, nor of individual MultiMesh instance culling. Need deliberate terrain seam stitching/skirting and spatial MultiMesh splitting.

## 5. Coast and ocean

The current `WaterCoastMath.BuildMask(width,height,Tile,seed)` fills **RGBA for the entire 120×88 map**. It must not run unmodified for 8192²: that is 8192 × 8192 × 4 = **268,435,456 bytes (256 MiB)** CPU raw RGBA before GPU copy, mipmaps or allocations. Design regional water patches with one/two cell halo depending on coast-distance semantics; use **same approved shader/colors**, a shared global coordinate for wave continuity and bounded per-region textures. Avoid:
- 8192² global ImageTexture and synchronous full-image updates.
- old flat V3 per-tile water shader.
- expensive alpha blending / reflections / Forward+-only features on R9 M395X.
Test coast mask regression on 120×88 (bitwise), compare per-region halo to global reference around every boundary, inspect visual screenshot.

## 6. Nature, mountain and people

- Current authored Kenney Nature Kit GLBs: 10 CC0 models under `Assets/Art/KenneyNature`. Each visible 16×16 chunk has separate species MultiMeshes so one giant forest isn't always drawn.
- PR #50 removed huge flat cap boulders from mountain tops; only conservative tall/embedded accent rocks on select lower slopes. Keep reference seed **1315511732** with the real mountain ridge.
- `NatureArtLibrary` should cache imported meshes/materials once, then compose per-chunk transforms. Future higher-quality Quaternius Standard models are **optional art experiments**, not drop-in replacement without measured GPU and style approval.
- `WorldState.People` currently drives a persistent MultiMesh; the future `EntityRegionStore` must maintain stable entity IDs and exactly-once cross-region ownership, bounded visible-instance buffers, deterministic fixed-tick simulation and save state. Separate logical simulation from render visibility (offscreen entities may still need summarized progression).
- A prototype with 8192² biome addresses but zero agents does **not** meet living-world product requirements.

## 7. Determinism, saves and failure recovery

Current `RegionCodec` and `ElevationRegionCodec` are per-region checksummed, versioned. Saving individual files with same-folder rename prevents a partially-written **single file** on common filesystems but cannot guarantee an all-or-nothing change across multiple regions. Production target:

1. Stage modified region and entity snapshots under a new transaction ID.
2. Write/flush and verify every staged file, hashes and region references.
3. Write a new manifest referencing only validated staged objects; promote manifest atomically on same H: volume.
4. Only after successful manifest promotion may old generations be collected; retain last accepted manifest and backup.
5. Load by verifying schema, world ID, seed, hash, every region dependency and entity version **before mutating live world**.
6. Negative tests: crash after a subset written, missing files, corrupted checksum, wrong seed, interrupted load, duplicate entity ID, cross-version migration.
7. Network/cloud sync is not in scope.

## 8. Profiling and complexity budget

Do not use "FPS: 60" from the 1-second HUD as proof of 60 FPS. Sample frame deltas and per-stage durations, compare p50/p95/p99/max and GC counts. The Godot Mono Linux headless runner is a correctness/import gate, **not** a GPU benchmark for the old Windows AMD.

Suggested counters per frame: active regions, active detailed/fallback chunks, uncovered screen tiles, queued/stale/discarded meshes, bytes waiting upload, upload ms, chunk-build CPU ms, shader draw calls/triangles if measurable, people simulated vs drawn, per tick ms, heap/VRAM estimate. Preserve performance logs only on H:.

**Target** 30 FPS class at 1280×800 / Eco; initial p95 <=33.3 ms and no recurring >100ms stall are *provisional acceptance goals*, not measured current behavior. Always compare same seed/camera/zoom/window against approved V2 and report tradeoffs.

## 9. Exact architecture decisions required

- [ADR 0001](decisions/0001-preserve-v2-renderer-and-compatibility.md): keep single V2 renderer/camera, Compatibility tier, user visual gate.
- Planned ADR 0002: immutable region snapshots and authoritative procedural generator/versioning.
- Planned ADR 0003: bounded streaming queue, cancellation, main-thread native upload.
- Planned ADR 0004: cross-region canonical height + LOD seam strategy.
- Planned ADR 0005: crash-atomic multi-region manifest save.
- Planned ADR 0006: regional population clock and inactive-world semantics.

## 10. Technical research

See [V3 technical evidence and rejected alternatives](V3_TECHNICAL_RESEARCH_2026.md). Never depend on unverified API availability from Godot "latest" development documentation without checking the exact 4.7.2 .NET runtime imported by CI.
