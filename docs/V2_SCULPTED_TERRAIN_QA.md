# V2.7 — Sculpted terrain facets (performance-safe first geometry stage)

**2026-10-30 | Issue [#37](https://github.com/tahazarif10/worldforge-sim/issues/37) | Baseline main includes water/Eco PR #36**

## Design and why this specific step

The old high-quality terrain surface was *flat at each tile* even though its material had gradients and rock strata. This first geometry stage gives each land tile a small, deterministic central relief and **correct sloped facet normals**. It changes 3D silhouettes and light response at close zoom, while preserving the old edge heights and thus old cliff seam geometry.

Crucially: **zero additional top triangles vs existing RichTerrainDetails**. The previous 4-triangle color-gradient fan had four corners and a center; now only that center's Y is elevated. Each top triangle has a true geometric normal, not the former forced `Vector3.Up`. In F3 simple mode the old flat quad remains.

- Mountain interior lift 0.12–0.28 world units; forest 0.035–0.085; meadow 0.018–0.05; sand only 0.008–0.02. Water has 0 relief and remains the optimized flat opaque lagoon from PR #36.
- `TerrainReliefMath` computes repeatable peak relief from biome/cell/seed and the **same exact 4-triangle fan interpolation** used in `AddTopFace`. Four tile edges stay at original authoritative elevation. Neighboring top edges and cliff walls do not develop new height seams.
- `TerrainGridPicker` now optionally ray-tests the exact four triangles per cell using a two-sided Möller–Trumbore intersection, retaining its existing bounded X/Z cell DDA instead of distance stepping; it reports the **same Y** as rendered facets.
- `StrategyCameraRig` passes the optional relief callback so mouse zoom anchoring and painting use the raised mesh. F3/F5 toggling changes rendering **and** picking in sync.
- Forests/rocks are grounded at sub-tile surface height; citizens use center height; vegetation meshes remain Kenney CC0 per-visible-chunk MultiMesh, unchanged size/style. Water, Eco mode (F4), and 2/4 chunk load budget remain intact.
- Old per-cliff heap allocations `float[] marks` are removed in favor of constants; fewer managed allocations during terrain edits/streaming.
- **F5** toggles facet relief to compare new vs old at same camera/seed. When F5 toggled, it rebuilds currently resident terrain and foliage synchronously; this diagnostic toggle may briefly hitch even though normal per-frame gameplay does not do so.

### Important scope limit

This is **not** yet a full continuous multi-region heightfield or geologic erosion model. It is one meaningful **zero-new-top-triangle** improvement for the current 120×88 Godot visual lab. The future full-heightfield work in issue #34 still needs region-elevation persistence, slope/cliff topology and renderer/physics synchronization. Do not claim the current preview supports fully resident 8192×8192 maps; WorldState remains small and monolithic.

## Tests and accuracy acceptance

- `tests/TerrainRelief.Smoke`: 63 sub-cell locations on all four triangles, compares exact vertical ray hit to analytical four-fan surface height; checks water unchanged, coast nearly flat, apex bounded, F5 fallback and deterministic generation.
- CI: earlier WorldSim, WorldRegion, Camera3D, Art, FrameTiming, VisibleChunks, WaterAndStreaming tests plus actual Godot C# build.
- `.github/workflows/visual-lab-ci.yml`: official Godot 4.7.2 Mono Linux CC0 asset import, C# build and alternate standalone VisualLab startup. These checks cannot prove high-end graphics or Windows R9 M395X FPS.

## Windows H: user review

Close the Godot Editor and any running game before switching branches; **do not reset or discard locally modified `project.godot`**.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-sculpted-terrain-facets
.\scripts\verify-v2-art.ps1
```

In the stand-alone 3D game:

- [ ] See more dimensional mountain/forest/meadow tile surfaces at close zoom; no terrain holes or floating trees.
- [ ] Press **F5** to toggle new geometry, compare before/after. Use **F8** to capture screenshots at identical camera pose. F3 still toggles rich gradients/cliffs; F4 still switches Eco/Balanced.
- [ ] Paint Mountain/Grass/Ocean/Forest near chunk seams: click cell and actual 3D facet match; no missing cliff faces, no water gap.
- [ ] Zoom into mountain while pointer rests at peak and lower slope; object under mouse stays anchored reasonably well.
- [ ] Move quickly across forest with F4 Eco and Balanced, check F10 active/pending counts. No worse prolonged missing chunks or foliage.
- [ ] Compare F9 frame-times at identical viewport and workload to PR #36. Real 60 FPS is ~16.67 ms/frame, 30 FPS ~33.33 ms/frame; p95/p99 must be **measured** on target Windows host.
- [ ] Capture at least one overview and one close F8 screenshot and verify person/meteor/pause/forest brushes.

**Merge safety:** may merge when all CI/Godot headless tests succeed since project.godot and default Main.tscn are unchanged; actual Windows visual/FPS acceptance must be documented separately before claiming "weak hardware runs without lag". Source user-approved previous quality stays at feature-toggle F5 OFF and Git PR rollback.

## References

- Official Godot [SurfaceTool attributes, normals and geometry](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html).
- Official Godot [3D performance / LOD / culling](https://docs.godotengine.org/en/stable/tutorials/performance/optimizing_3d_performance.html).
- Official Godot [camera ray picking for orthographic views](https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html).
