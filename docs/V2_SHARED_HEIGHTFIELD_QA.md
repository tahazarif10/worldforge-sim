# V2.8 — Continuous shared-corner slopes, without increasing top triangle count

**Issue:** [#39](https://github.com/tahazarif10/worldforge-sim/issues/39) • **Baseline:** V2.7 accepted + merged [PR #38](https://github.com/tahazarif10/worldforge-sim/pull/38)

## Problem identified from the actual project

The old V2.7 terrain still had each tile's four outer corners all at its own biome-assigned plateau height. Even though there were four sloped triangles **inside each tile**, neighboring tiles often disagreed about their common edge's height, forcing vertical cliffs along every change. Adjacent chunks reused the same authoritative height values, but could not share corner vertex geometry.

This iteration genuinely makes **land-to-land edges geometrically continuous** while preserving the approved art assets, Compatibility water, camera, brush commands and Eco quality tier.

## Source implementation

- `Scripts/VisualLab/SharedCornerHeightfield.cs`: pure C#, does not allocate a full-world height array. At lattice vertex (vx,vz) average the four adjacent cell heights in absolute coordinates (with boundary clamping). The value is **identical no matter which tile/chunk computes it**. Each land tile keeps its original four outer top-face triangles; their four corners now come from the shared grid and the center is the deterministic prior `HeightAt + ReliefAt`.
- `SharedCornerHeightfield.Fan.Sample(u,v)`: exact piecewise barycentric height of those four triangles for citizens and authored plants/rocks.
- `VisualTerrain3D`: in smooth mode, no interior vertical cliff faces between land tiles; only 3-band shoreline / world-border skirts drop into the sea. There is one shared heightfield for all visual 16×16 chunks, so a new chunk matches an old resident chunk at all edge vertices. Existing water plane and foam shader unchanged; old flat shore strips are skipped in smooth mode because the water shader provides foam.
- `CameraRayMath` + `StrategyCameraRig`: optional exact four-vertex-and-center triangle hit for shared slopes, still using bounded DDA through the target terrain cells; old flat/cliff picker is preserved when smooth mode is disabled. Camera mouse grabbing, cursor zoom and brush use **rendered triangle elevation**, not earlier plateau height.
- Person position uses `SurfaceHeightAt(x,y,.5,.5)`. Authored GLB trees/rocks use their deterministic original fractional placement with the same surface height. The same 10 Kenney CC0 meshes/materials, per-visible-chunk MultiMesh and Water/Eco F4 are retained.
- **F6** switches between continuous land slopes (default) and the previous terraced/craggy terrain (without overwriting simulation). F5 still toggles *interior* relief; F3 toggles richer vertex ground shading, F4 Eco, F8 H: screenshot, F9 H: frame-time report, F10 active chunks. F6 is an on-demand diagnostic: rebuilding already resident chunks synchronously can cause a transient hitch on keypress, not during normal camera motion.

## Performance and limitations

**What we preserve:** four top-face triangles per land cell; no terrain-texture read, new GLSL effects, physics collider per tile, separate water mesh, or global height array. On smooth land, removing interior vertical cliffs can **reduce** triangle count in mountainous transition bands. CPU cost of computing four shared corners plus a center is incurred only when visible chunks are generated/dirty. Existing camera-driven chunk residency and 2/4 new chunks per 50ms remain unchanged.

**What this does NOT implement:** fully independently sculptable elevation layer in `WorldSim.Regions`, stored chunk heights, cave/overhang geometry, large-scale erosion, high-resolution LOD, 8192×8192 playable world, or actual GPU-host performance guarantee. The authoritative `WorldState` map is still finite/monolithic and biome classes still determine base heights. A full region-elevation format and smooth edit brushes are separate future work tracked by [#34](https://github.com/tahazarif10/worldforge-sim/issues/34).

**Visual trade-off:** some old rock wall layers between land tiles disappear in smooth mode and become sloped grass/stone instead. The exact previous cliff silhouette is restored using F6. Shoreline rock skirts and authored rock plants remain.

## Automated tests and real host QA

- `tests/SharedHeightfield.Smoke`: traverse all 80×64 sample cells and >10,000 land tile boundaries, testing multiple points along each shared edge; confirm x=16 and y=16 visual chunk boundaries match exactly, including an edit on the boundary.
- Same tests cast thousands of vertical rays against the actual 4-triangle top fan and compare DDA hit position with calculated surface elevation. A sparse sample at logical coordinate 8191 is **math-only**, not evidence of rendering a massive map.
- Existing `tests/TerrainRelief.Smoke` still checks original flat/faceted fallback. Other simulation, camera, water, chunk, nature and GLB integrity tests continue in CI.
- `.github/workflows/visual-lab-ci.yml`: actual Godot 4.7.2 Mono Linux headless import, C# build and alternate 3D scene startup. Running scene in CI is not equivalent to a Windows AMD GPU performance/visual test.

### Windows H: verification

Close editor and standalone scene. Preserve all local `project.godot` changes.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-shared-corner-heightfield
.\scripts\verify-v2-art.ps1
```

After merge, use `git switch main; git pull --ff-only origin main` instead.

**Acceptance checklist:**

- [ ] Compare F6 OFF vs ON at identical camera location/seed and zoom. In the new smooth mode, hills should look less blocky, with no open holes or steep fences between land tiles.
- [ ] Move camera across chunk boundaries x≈16/32/48 and return; no height cracks, cliff ghosts or floating trees.
- [ ] Paint Forest and Mountain directly across a chunk border; adjoining slopes must rebuild and reconnect without seams. Try Ocean/Sand near shore: water foam unchanged, no hole below sea plane.
- [ ] Select/move cursor on slopes and zoom on mountainous center and edge; mouse brush picks expected tile.
- [ ] Spawn people and observe on gradients; no floating/sunken models.
- [ ] Balanced (F4 normal) retains Kenney art, shadows and previous water. Eco mode retains all models, lowers GPU shade load. Compare F9 exported p95/p99 60s camera travel and 20s editing at same map/seed to V2.7 baseline.
- [ ] F8 two real PNG screenshots for visual evaluation. No FPS promises until the AMD R9 M395X host measurements are captured.

**Release policy:** merge additive change after all C#/real Godot CI checks are green, keeping previous style available immediately via F6 and keeping Main.tscn/project.godot untouched. Windows frame-time and aesthetic signoff remain a separate validation task before declaring a stable high-quality release.

## Research sources

- [Godot 4.7 SurfaceTool manual](https://docs.godotengine.org/en/4.7/classes/class_surfacetool.html) — per-vertex attributes and normal construction.
- [Godot optimizing 3D](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html) — chunk culling, distance-based levels of detail and visibility.
