# ADR 0004 — Continuous surface fallback and canonical-edge handoff

- Status: Accepted for the **V3.4.2 opt-in engineering slice**; full distance LOD and coast-mask tiling remain planned
- Date: 2026-11-02
- Parent: [#55](https://github.com/tahazarif10/worldforge-sim/issues/55); slice [#65](https://github.com/tahazarif10/worldforge-sim/issues/65)
- Baseline: `19a7dd797306ed654b9218ff95bd942a0e587ad8` / PR #64
- Extends: [ADR 0003](0003-bounded-viewport-demand-and-generation-leases.md)
- Preserves: [ADR 0001](0001-preserve-v2-renderer-and-compatibility.md)

## Context and authorization

V3.4.1 computes bounded visible demand, but a queued detailed chunk can have no land geometry. Its missing counter used dictionary membership, which cannot distinguish a completed land mesh, a legitimate water-only chunk or an incomplete native mesh. Detail construction runs on a 50ms cadence; camera movement can expose new cells between passes.

The user reported that the interactive candidate looked good and explicitly authorized advancing the next slice on 2026-11-02. This is qualitative feedback, not an exact-SHA screenshot or an isolated R9 M395X performance result. Keep the experiment OFF by default and V3.3 physical gates open.

**Falsifiable hypothesis:** after each successful opt-in visibility update, including a zero-detail-budget update, every planned visible chunk has current surface coverage backed by visible land mesh buffers and/or the existing ocean; incomplete detail must not remove that coverage.

## Alternatives

1. **Wait for detail or retain only old chunks.** Cheap to implement, but a teleport exposes cells that have never had geometry.
2. **Coarse fixed-stride height grid plus another ocean shader.** Fewer vertices, but a coarse cell can erase a small island or bridge water; unmatched boundary samples introduce cracks. A second water renderer risks repeating the rejected V3 appearance.
3. **Selected: bounded surface-only proxy using canonical per-cell tops and simplified banks.** Preserve every land tile, the existing four-triangle top, center relief, corner palette and shared edges. Omit foliage, legacy tide-stripe decoration, stratified bank layers and proxy shadows. This reduces decoration and bank complexity without changing the near detailed renderer or inventing a new coarse coastline.

This first fallback deliberately retains top-surface resolution. It is not a complete distance-based LOD system, and no GPU speedup is inferred from its design.

## Decision

- `TerrainFallbackMesh` receives at most 16x16 sampled value cells and returns pure managed position/normal/color arrays. It does not read live world state or create Godot objects. This slice calls it synchronously; immutable worker ownership is V3.4.3.
- The adapter samples the current WorldState-backed source, canonical editable fans and existing palette. Smooth coast banks use the exact same edited edge heights as their tops. Legacy terraced mode uses existing plateau/neighbour heights.
- Reuse the current opaque V2 water plane and shader, with exact small-world bounds. A water-only proxy has a verified cell classification and no fictitious land mesh. Water availability is checked separately.
- Main thread alone creates ArrayMesh resources, materials and nodes.
- Publish coverage for the entire new visible plan **before eviction**. Apply the new camera plan every game frame in opt-in mode, while keeping the existing 50ms 2/4 detailed-build cadence.
- Stage native detailed resources before modifying a resident node. A proxy stays visible while the detailed terrain/foliage lease is incomplete. Complete the matching generation/lease, show detail and hide/retire its proxy within the same update. Deferred QueueFree nodes are hidden immediately.
- World epoch, data revision and geometry format qualify coverage. Edits, toggles, elevation load and world replacement invalidate the relevant records. A Ready entry with a missing/hidden/wrong native mesh is repaired through a new revision; dictionary membership alone never counts as coverage.
- Diagnostics separate missing **detail** from missing **combined surface**. The planner/fixture identity becomes `viewport-v2-fallback`; old `viewport-v1` evidence is not equivalent.

## Bounds and remaining risks

- At method return, fallback records are limited to the visible plan; detailed records and queue are limited to desired demand. The existing cap is 2048, with refusal before eviction. The 120x88 reference world has at most 48 display chunks.
- Worst-case numeric output per fallback chunk: 256 land cells x (12 top + 24 bank vertices) = 9216 vertices. Position/normal/color payload is 368,640 bytes (360 KiB), excluding array headers, input cells, managed/native conversion and native/GPU storage.
- Replacement temporarily retains previous resources plus a newly constructed replacement; queued native frees finish at the engine's deferred-free boundary. This is not a peak-RAM/VRAM measurement.
- Fallback construction and native uploads remain synchronous. A wide zoom/teleport can cost frame time. Count-bounded detail work is not a millisecond or byte budget; V3.4.3 must instrument and bound construction, edits and uploads.
- This fallback is for the existing small world. It does not authorize a whole 8192² water mask, a larger WorldState or default feature promotion.
- Headless mesh-buffer coverage is not GPU pixel coverage, art acceptance or p95/p99 evidence.

## Verification and rollback

Pure tests independently integrate projected land area, reject water bridging, preserve single-cell islands/partial edges, inspect canonical shared vertices and sculpted coast banks, and enforce array bounds and invalid-input rejection.

The official Godot probe exercises actual camera planes, pan/zoom/teleport/resize, zero detail budget, both terrain sources, real mesh buffers, water-only coverage, edits and format changes, the sub-50ms game-frame cadence, failure between terrain construction and handoff, late completion refusal, missing native resource repair and world reset. The original baseline capture checks remain enabled. Exact run IDs and tested SHA belong to the slice PR/issue; planned tests are not a PASS.

Physical Windows review: same seed, normal controls, sustained edge travel, zoom and resize, shoreline sculpt/paint, New World, F8 appearance and matched frame-time evidence. The feature remains opt-in until those gates pass. Omit `-ViewportStreaming` to restore the ordinary planner; preserve local `project.godot` and files.

## Primary research

- [Godot 4.7 ArrayMesh](https://docs.godotengine.org/en/4.7/tutorials/3d/procedural_geometry/arraymesh.html): aligned native vertex/normal/color arrays and main-thread adapter API; exact C# compatibility is checked by the 4.7.2 Mono build.
- [Godot 4.7 3D performance](https://docs.godotengine.org/en/4.7/tutorials/performance/optimizing_3d_performance.html): tiled on-demand worlds and LOD are available techniques; transparency and geometry have costs. These do not supply hardware-specific performance guarantees.
