# ADR 0019 — Same-frame native nature instancing during terrain painting

- Status: Implemented candidate; user Windows GPU visual/performance gate pending
- Date: 2026-11-06
- Extends: ADR 0018 terrain-only immediate preview (now terrain and nature together)
- Preserves: ADR 0001 V2 original scene/camera/shaders and ADR 0015+ world/house/save authority

## Problem

PR #111 made terrain/biome colors appear before mouse release. The user confirmed this, but reported that the original tree and nature props appeared only after the expensive full foliage build completed. Full regeneration on every cursor movement was already rejected for its worst-frame stalls.

## Decision

Use an **independent native MultiMesh per authored Kenney species/part and edited V2 visual chunk**:

- At first touch of a camera-resident 16×16 chunk, capture the current original seeded biome and height samples, group placements by species, create bounded native MultiMesh arrays using the **same cached mesh resources, materials and exact GLB part-local transforms** as `NatureArtLibrary.Populate`, and atomically hide the previous complete foliage root only after the new visual cover is ready.
- Keep one stable slot per active logical tile within its species. Subsequent brush movements recompute canonical placements for that touched 16×16 chunk but **write only changed/removed/added instance transforms**. Deletion uses swap-with-last and updates the slot map; capacity grows geometrically. No GLB import or per-tree Node3D instantiation in mouse callbacks; no unbounded per-frame native worker queues.
- The existing terrain-only live preview and live authored foliage update together in `PresentLiveBrushPreview`. Trees (and consumed-tree stumps), sand rocks, small grass/flowers and sparse mountain accents honor the same stable `NatureScatter` selection and measured terrain height as final art. Optional historic non-Kenney prefabs remain a compatibility fallback; the ten approved bundled models are the supported target art.
- A separate WorldSim-owned resource art adapter batches berry/stump visuals, and uses a short coalesced refresh interval while relevant biome edits are in flight. This does not change resource counts, regrowth or conservation.
- After release, the existing budgeted native terrain+full foliage regeneration still runs. The temporary ground and foliage both disappear **only after a full matching chunk is published**. Eviction, New World and quality changes release/update live instances. A changed stump immediately removes any stale live tree.
- Never rewrite `project.godot`, change the camera, world generator, terrain/water shader, WorldSim, AI, houses or versioned save format.

## Alternative approaches rejected

- Rebuild all GLB scenes on every drag event: stalls, repeated allocations and unreliable FPS.
- Draw low-poly fake placeholders for the trees until real art appears: apparent lag and visual pop are still present.
- Enable experimental regional/worker renderer by default: breaks the established user-accepted V2 fallback and lacks physical approval.

## Verification

The native default-mode Godot probe asserts an eligible Forest-to-Grass-to-Forest site has visible loaded Kenney MultiMesh instances during the held stroke, that the old full foliage is hidden, that native full foliage chunk rebuilding is not triggered by every edit, and that the live batch remains after mouse-up then is correctly retired on completed full publication. Existing worker-mode and raw-scene tests must still compile and run. These checks do not certify exact Radeon GPU frame-time or pixel parity on the user's Windows machine; ask the user to test a held Forest brush and a Land eraser, and report any remaining leaf/flower or FPS defect.
