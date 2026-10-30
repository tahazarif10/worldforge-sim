# V3.0 — Region-backed 8192×8192 logical world laboratory

**2026-10-30 | Separate opt-in scene | GitHub issue #43**

## Architecture and scope

The approved V2 3D game remains `Scenes/VisualLab3D.tscn`, with the same Kenney CC0 GLBs, responsive low-end sea and gameplay. The **new** `Scenes/RegionalWorldLab3D.tscn` exercises an 8192×8192 logical terrain coordinate space, but uses **zero global 8192² terrain arrays**, zero full-world water masks, and **no global mesh construction**.

| Component | Existing V2 | V3 regional proof |
|---|---|---|
| Terrain authority | `WorldState` 120×88 single array | `WorldSim.Regions.RegionStore` 64×64 generated/edited regions |
| Logical coordinates | 120×88 | 8192×8192 tiles; integer camera origin |
| Resident data | Whole tile map | **24 max** cached data regions (4096 biome bytes/region), additional sparse edit maps |
| Rendering | 16×16 camera-visible terrain and GLB batches | 16×16 **visible-only**, cap **90** resident chunk nodes |
| Loading budget | 2 Eco / 4 Balanced every 50ms | 2 Eco / 4 Balanced every 50ms; 16 at first start |
| Models | 10 Kenney CC0 GLBs, batched by chunk | same `NatureArtLibrary` per-chunk batched authored models |
| Water | approved lagoon with 120×88 dynamic coast mask | separate, **simpler** opaque per-visible-chunk sea shader (no 8192² mask) |
| Save | separately editable elevation (F11/F12) | separately editable biome regions (F11/F12) |
| Citizens | WorldState living citizen simulation | **not yet ported** |
| User default | unchanged original Main.tscn and V2 visual scene | new opt-in standalone preview only |

Godot's [MultiMesh culling notes](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html) advise splitting geometry into **spatial batches**: a single MultiMesh cannot cull individual instances. Existing Kenney art is thus batched into the visible 16×16 chunks. Godot [ArrayMesh](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html) and SurfaceTool support per-chunk generated geometry. We deliberately preserve the Godot 4.7.2 **Compatibility** renderer and avoid SSR, geometry shaders, full-world textures and extra GPU reflection passes.

## What users can test in the new scene

- Right mouse drag or WASD moves the isometric camera; wheel zooms, with exact ground ray picking. `Home`, `End`, `PageUp`, `PageDown` teleport to distant corners **without loading the intervening map**.
- Press 1 Grass, 2 Forest, 3 Mountain, 4 Ocean or 5 Sand, then paint using left click/drag with a radius-two brush.
- F4 switches Eco (sun shadow OFF, max 2 new chunk builds per 50ms) / Balanced (shadows ON, 4 chunks per step).
- F10 logs resident biome regions, dirty region count, visible/pending chunk count and total builds to the console.
- F11 writes only changed 64×64 biome regions to `H:\WorldForge-Sim\artifacts\regional-world\seed_20261030\biomes\`. F12 validates every region's magic/version/checksum, seed and filename **before modifying the in-memory region store**. Repaint and revisit a distant location to confirm persistent changes.
- F8 saves a screenshot and F9 saves frame intervals under `H:\WorldForge-Sim\artifacts\regional-world\seed_20261030\...`.

## Test it on the H:-only Windows machine

Close Godot Editor and the game. **Do not reset, clean or overwrite locally modified project.godot**.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v3-regional-world-lab
.\scripts\verify-v3-regional.ps1
```

After merging use `git switch main; git pull --ff-only origin main` rather than the feature branch.

### Acceptance sequence

1. At map center, camera, coast/water shader, authored trees and ground chunks should render without missing shader material. Old V2 game should still look exactly the same.
2. Teleport via Home/End/PageUp/PageDown. With F10 observe that resident biome region count **never exceeds 24** and visible chunk count **never exceeds 90**, even as logical coordinates move thousands of cells.
3. Paint Forest near x=8192 limit and another region; pan/teleport away and return — edit must persist in memory after LRU eviction. Edit on a 64-cell border.
4. F11 save; F12 load; ensure unchanged seed correct and no corrupt edit source is accepted. Try manual corruption only on a **copy of a saved file**, never the original.
5. F4 Eco and Balanced A/B; F9 frames at the same camera/viewport and actions, F8 screenshots at forest/coast. No universal FPS claim until the actual AMD R9 M395X p95/p99 is measured.
6. Re-run the old `scripts/run-visual-lab.ps1` after verification; the original 3D living-world game must remain unchanged.

## Known, deliberately honest limits

- An 8192² **logical address space and camera** are functional; this is not an 8192² living-person simulation or fully-featured world. This version has **terrain painting only**, without citizens, disasters, automatic population updates, regional elevation edit integration or save-state for agent simulation. These need a separate sector simulation scheduler, serialization schema and visual LOD.
- The regional biome generator currently uses deterministic coarse lattice classification; its art direction can look less organic than the approved 120×88 visual lab. This is a systems-proving scene, not a replacement for the good graphics. Old V2 stays intact.
- A capped render-chunk count is a node count budget, not a measured time/memory guarantee. 90 chunks × 256 cells × several triangles and CC0 model batches still requires **real GPU measurement**. Most cells are water and produce fewer land meshes.
- The new global region painter stores sparse overrides in process memory and supports explicit regional file persistence; the disk writer uses same-directory atomic single-file replacement, not a multi-file crash-transaction, journaling or cloud sync. Files must be written on H:.
- Currently, the camera still uses `float` world coordinates, acceptable for 8192 tiles but **not** a true infinite-precision origin-rebasing engine.
- On future 8192-wide maps do not rebuild a giant coast mask: keep water/tint inside visible chunks, as this test does. A future visual pass can generate per-region shoreline textures with one-cell halo, not a global atlas.

## Automated acceptance

- `tests/RegionalWorld.Smoke` verifies edit persistence, negative coords, bounded LRU after 300 jumps, 8192 math-only view planning, saverestore and corruption/wrong-seed rejection.
- `.github/workflows/core-ci.yml` runs all existing V2 regression tests and the new region suite.
- `.github/workflows/visual-lab-ci.yml` imports CC0 GLBs, builds the Godot .NET project and headlessly launches **both** the V2 living-world scene and the new regional lab on Godot 4.7.2.
- **These cannot certify host FPS or visual quality.** Do not declare low-end performance achieved without F8/F9 artifacts.
