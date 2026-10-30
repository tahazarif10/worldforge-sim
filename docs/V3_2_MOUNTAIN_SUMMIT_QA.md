# V3.2 — Mountain summit / oversized rock-cap removal

**Target:** The user-provided 1291×856 V2 gameplay screenshot (2026-10-30). The central ridge contains an unnaturally wide horizontal reddish/teal **boulder canopy**, hiding the triangulated mountain slope. In the source this came from a rock-prop problem, not a terrain-height problem:

- `NatureScatter.Pick(Terrain.Mountain,hash)` made a rock eligible at **3/5 (60%)** of mountain cells.
- `VisualTerrain3D.RebuildVegetationChunk` placed full `rock-largea.glb` or `rock-talla.glb` models at `0.9f × variation` height per eligible mountain cell, and one additional optional imported rock. The wide `rock-largea` variant has a visibly flat top in the screenshot.
- When neighboring cells get decorated, authored slabs visually merge into a single oversized floating cap over the grey mountain. Existing true 4-triangle mountain relief is already underneath.

## Surgical fix

`Scripts/VisualLab/MountainSummitArtMath.cs` is new **Godot-free deterministic** placement policy, used **only on Mountain cells**:

- Height tier = **the exact existing** `Hash(x,y,worldSeed)%11` term used by the current mountain elevation formula. High summits (tiers 8–10) receive **zero** decorative boulders, always.
- Low exposed flanks have a low chance of an accent (17–22%); interior midsection 8%; near ridges 4%. No large dense cluster, no stacked mass.
- Every placed mountain rock is the **upright/tall** existing Kenney CC0 `rock-talla.glb` (species 7), **never** the broad plate `rock-largea.glb` (species 6). Sand beach rocks **still use the original unchanged mixture**.
- Max mountain authored detail height **0.43 world units before variation** (old was 0.9), minimum 0.17. Rocks are embedded 0.055 unit into the **already sampled exact terrain surface**; they should read as exposed rock chips on the slope, not separate caps.
- `VisualTerrain3D` uses the existing shared materials, original real mountain topology, unchanged shared-corner picker, shadow tier, authored NatureArtLibrary and per-16x16 MultiMesh batching. Existing missing-GLB fallback also now uses the same bounded mountain accent, not its original oversized boulder.
- The **underlying mountain base, terrain elevation, peak geology, citizen navigation and painting remain unchanged**. This fix exposes the broad terrain base and tiered rocky facets rather than procedurally reconstructing it. No extra triangles, textures, draw passes, per-frame allocations or model resources.
- The decisions use stable absolute cell coordinates and the existing world seed. Chunks rebuilt after panning or painting should show identical details, with no dependence on load order.

## CI acceptance

`tests/MountainSummit.Smoke` checks:
1. Every high summit tier is guaranteed clear of authored mountain boulders over thousands of hash/neighbor combinations.
2. No Mountain rock ever selects broad flat species 6, including fallback.
3. Every allowed rock is small, partly embedded and height-bounded, with variation `<= 0.51` versus old maximum `> 1.0`.
4. Placement deterministic and per-cell, independent of regeneration.
5. Actual 120×88 `WorldState` maps for four seeds, including current scene's 20261030 and screenshot example's 1315511732, show mountain mesh instancing count **at least half lower**; the exact old/new counts are printed by CI.
6. Sand/shore rock species generation remains available.

Existing original V2 camera, terrain, water, eco, citizens and deterministic simulation/regional parity suites must all stay green. Godot 4.7.2 Mono official Linux headless import and V2 scene boot must also pass.

## Manual visual and performance acceptance on Windows / H:

- Run `git status --short` before pulling; **do not reset or discard local project.godot**.
- In `H:\WorldForge-Sim`, update to the merged main and launch `.\scripts\verify-v2-art.ps1`. This is the **approved V2 3D scene** with the 20261030 reference seed.
- Zoom toward a mountain (not the water or forests); photograph with **F8** to `H:\WorldForge-Sim\artifacts\visual-review\`.
- **Visual gate:** There must be NO wide merged reddish/teal rock slab across the peak; grey faceted mountain summit must read as continuous geology, while narrow stones appear on only a few lower/middle cells. Existing forest, coastline, sand, people and camera should look as before.
- **Performance gate:** Use **F9** to store camera/zoom frame samples on H: and inspect p95/p99 versus the user's prior V2 screenshots/benchmark, with the same viewport and camera pattern. The algorithm creates fewer stone instances; this suggests a cost reduction, **not** guaranteed FPS or Windows host validation.
- Compare with the user-provided 2026-10-30 screenshot (central mountain). Same seed, same zoom and viewing position if possible. Scene on Linux CI cannot provide the same AMD GPU rendering evidence.

**Do not call this visually accepted until the user approves a Windows F8 screenshot.** No Work/Codex was invoked: direct GitHub connector and GitHub Actions only.
