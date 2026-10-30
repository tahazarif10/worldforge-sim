# V2.4 — Camera-visible chunk residency (first rendering streaming step)

**Issue:** [#28](https://github.com/tahazarif10/worldforge-sim/issues/28)  
**Baseline:** merged, user-reported-better 3D camera and chunk-local foliage [PR #27](https://github.com/tahazarif10/worldforge-sim/pull/27).

## Difference from previous rendering

**Before:** `VisualTerrain3D.BindWorld` created a terrain mesh and a foliage batch for **every** 16×16 chunk, regardless of camera zoom. Local brush edits were already chunk-scoped but offscreen chunks remained resident.

**Now:** `BindWorld` retains just the simulation reference and global sea. `VisualTerrain3D.UpdateVisibility(Camera3D)` projects the four screen corners to the sea and mountain-height reference planes, builds a conservative 2D rectangle plus a one-chunk margin, selects local chunk keys through `VisibleChunkPlanner`, and lazily adds/removes only those terrain/foliage chunk nodes. The main visual lab calls it when camera initializes or world resets, and on a 0.1s navigation cadence.

Painting an offscreen cell updates the authoritative `WorldState`, but doesn't force a hidden chunk to be built. It is constructed from the updated data when the camera returns. Existing local brush invalidation still affects adjacent chunks to avoid stale edges.

## Current release boundary

- This is **visual chunk residency**, **not** true large-world streaming yet.
- Existing authoritative `WorldState` is still one 120×88 array, hard-cap 1024 per axis.
- `WorldSim.Regions.RegionStore` supports bounded resident logical regions, but `VisualLab3D` does **not yet use** it as its gameplay world.
- GPU resources are created synchronously when a new chunk becomes visible. Rapid movement may still cause a frame spike or visible small delay. A bounded CPU worker/queued GPU upload is the next stage after data source migration.
- Existing water remains one plane matching world dimensions. Full art quality still needs authentic licensed/original 3D trees, rock models and better terrain geometry.
- For 8192², the **pure planner** tests select a bounded number of chunk IDs; this is not evidence that an 8192² world can render or simulate today.

## Tests and Windows acceptance

CI now runs `tests/VisibleChunks.Smoke`, which exercises tiny view, borders, clip margin, non-intersecting camera, invalid coordinates and a bounded 8192×8192 **math-only** selection. Prior CI suites also run.

On the Windows H: workstation, close Godot and run:

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-visible-chunk-residency
.\scripts\verify-v2-integration.ps1
```

This script runs camera3D, regions, frame timing and visible-chunk smoke tests; then builds and opens the stand-alone 3D visual lab, not the editor.

Manual checks:
- [ ] Startup loads without a blank map or shader error.
- [ ] At default camera zoom, map remains visually comparable to the previously accepted PR #27 screenshot.
- [ ] Zoom close to a forest, press **F10** to print active terrain/foliage chunk counts in the game PowerShell output.
- [ ] Pan right across the map, press **F10** again; active count changes, no holes, no ghost/duplicate trees and foliage changes persist when returning.
- [ ] Paint across a visible/offscreen boundary; no stale forests when returning, correct brush under mouse.
- [ ] Spawn humans, pause, meteor, world reset remain functional.
- [ ] Press **F9** after 30 seconds of panning/zoom to save p95/p99 frame intervals to `H:\WorldForge-Sim\artifacts\performance`.
- [ ] Compare current FPS/worst frame and memory with PR #27 at the same seed, viewport and zoom. No unmeasured speedup claim.

**Merge condition:** CI success, then user standalone play and visual/performance acceptance. The main 2D scene, `Scenes/Main.tscn`, `project.godot` and local H: user modifications remain unchanged.

## Follow-up architecture

1. Make `RegionStore` the data source for an opt-in bigger 3D world; implement deterministic 64×64 region generation + edit journal, not a full world-array allocation.
2. Bound and prioritize new chunk mesh building and GPU submissions to avoid teleport spikes.
3. Introduce 3D LOD and authored art assets with valid licensing, not primitive balls/cylinders as final quality.
4. Test 512², 2048², 4096² then 8192² sparsely, recording actual active/resident regions, p95/p99 and RAM/VRAM.
