# V2.5 environmental quality pass — researched, code-gated, visually unverified

**Date:** 2026-10-30 · **Issue:** [#32](https://github.com/tahazarif10/worldforge-sim/issues/32)
**Baseline:** User positively reviewed authored 3D Nature GLBs in [PR #31](https://github.com/tahazarif10/worldforge-sim/pull/31), subsequently merged to main.

## Why this change, not another asset-only patch?

The previous forest art added genuine meshes, yet the camera-visible land still comprised entire uniformly colored, tile-flat quads and walls of single-color rock. The ocean shader used one world-agnostic UV color wave; painting land/ocean did not change the shading on the water itself. These three factors produce the biggest visible mismatch against professional diorama-like games, even when authored trees improve.

Research supports Godot **SurfaceTool vertex colors / normals** for lightweight procedural geometry; the Compatibility renderer cannot rely on every Forward+-specific real-time effect. Godot [4.7 SurfaceTool documentation](https://docs.godotengine.org/en/4.7/tutorials/3d/procedural_geometry/surfacetool.html), [renderer feature matrix](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html), [shader uniform hints](https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/shading_language.html), and [original Quaternius MegaKit Standard](https://quaternius.itch.io/stylized-nature-megakit). Professional reference art: [Preserve](https://store.steampowered.com/app/2109270/Preserve/), and [Godus](https://www.gamereactor.es/godus-20-es-un-juego-completamente-nuevo/) for layered islands.

## Implemented

1. **Terrain color composition, no height collision changes.** Each land tile surface now uses four vertex-colored triangles around a center, blending close biome colors on the borders and adding seeded small variations. Water-to-land never contributes marine blue to land vertices: sand hues become the visual transition. Color math is independently tested in `EnvironmentArtMath`.
2. **Geological cliff layering.** Existing continuous *vertical* cliff quad surfaces are subdivided into three horizontal rock bands with subtle deterministic tones and upper highlights. The wall geometry still exactly matches `HeightAt`, preserving 3D brush/picking math. The 3D terrain remains intentionally terraced rather than truly sculpted; continuous elevation/slope collision requires a larger follow-up architecture change.
3. **Responsive coastal water.** `EnvironmentArtMath.BuildShoreMask` creates one RGBA8 mask from actual WorldSim tile types: water adjacency/foam strength, shallow/deep intensity and seeded low-frequency detail. The shader samples this mask with wave patterns tied to logical world-space, so changes of world dimensions no longer change wave scale. The mask is updated after brush edits and on new-world creation **only**, not on every frame. This supports OpenGL Compatibility without screen-depth / SSR.
4. **Citizen allocation stabilization.** One Mesh/MultiMesh instance buffer is reused across ticks and brush actions; resizing only when population count changes. This avoids one managed/native mesh allocation per simulation advance. Still updates visible people transforms O(population), and no claims of 10k entity simulation yet.
5. **On-demand visual proof.** **F8** saves a real engine screenshot to `H:\WorldForge-Sim\artifacts\visual-review\v2-art-*.png`. F9 retains 7200-frame interval ring CSV on H:, and F10 active chunk debug counters. Screenshot GPU readback only on F8 (not every frame).

## Automated tests

- `tests/EnvironmentArt.Smoke`: deterministic coast mask, ocean-to-land foam update, deep/shallow distinction, alpha and RGBA dimensions, terrain gradients and corner blend invariants.
- Existing WorldSim, WorldRegion, Camera3D, VisibleChunks, NatureScatter, FrameTime and GLB binary integrity tests still run under CI.
- Godot C# compilation checks code syntax **but does not run the shader or GLB import**, cannot verify artistic quality or FPS.

## Run the new visual direction on Windows (H: only)

Close running Godot and game before branch switch. **Do not discard local `project.godot` modifications**.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-art-terrain-water-polish
.\scripts\verify-v2-art.ps1
```

This script calls the existing portable H:-based Godot to import the bundled CC0 Kenney models, runs environment/nature smoke suites, compiles and opens the alternative 3D scene without replacing the 2D default.

### Suggested A/B review, same seed and resolution

- [ ] Idle scene, 3D camera at overview: look for less checkerboard-like ground color and differentiated cliffs.
- [ ] Zoom close to forest/rock: Kenney trees remain detailed and no giant blue/green seams appear between biome surface colors.
- [ ] Paint Ocean on a beach, then Land/Sand on the same area: water should acquire/lose coast foam after the normal terrain refresh cadence (~0.16s), without stale mask.
- [ ] Move camera across chunk borders, especially near coastline: no missing triangles, cracks, wrong terrain click or flipped materials.
- [ ] Check all WorldSim tools, person movement, pause, meteor and New World. 3D screen-ray brush must still target the **unchanged** top height.
- [ ] Press **F8** to capture a real screenshot on H:, **F9** after 30–60 seconds to export p95/p99 frame intervals to H:, and **F10** for chunk counts.
- [ ] Compare against previous accepted PR #31 with identical camera pose; preserve actual rendering method and AMD R9 M395X driver details.
- [ ] Test both F2 shadows ON/OFF and zoom near/mid/far; no unmeasured claim of FPS improvement.

### Known limitations and next independent priorities

- Terrain height remains biome-class stepped with vertical edges; this pass adds color detail and strata, but a **true sculpted 3D heightfield** needs new height data independent of tile biome plus exact triangle/heightfield ray picking. Do not introduce inconsistent geometry/picking.
- GPU water surface still one PlaneMesh. Sampling a small RGBA mask is cheap in principle, but GPU cost must be measured on old AMD.
- Source authored Kenney GLBs are mid-detail low-poly; the [Quaternius Stylized Nature MegaKit Standard](https://quaternius.itch.io/stylized-nature-megakit) has **68 models** in the free `~99MB` ZIP according to its publisher/OpenGameArt listing, and Source shaders are a separate tier. No Quaternius archive has been imported yet; do not claim its compressed glTF textures are already in repo.
- Larger worlds need WorldSim.Regions integration, streaming, chunk-level GPU budgets, LOD; pure viewport planner 8192² test is NOT a 8192² playable map.
- A subsequent professional vertical slice should combine *real mesh geometry*, carefully harmonized material palette, production assets and screenshot/FPS acceptance. Avoid overwriting the old main scene until visual direction is approved.

**Acceptance:** GitHub CI green, real Godot 4.7.2 Compatibility shader import/live edit QA, measured AMD performance and user artistic satisfaction before a default-scene change. Preserve H:-only cache/output and default 2D fallback.
