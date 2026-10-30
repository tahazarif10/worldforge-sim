# V2 integration build — camera, region data, foliage, telemetry

Branch: `feat/v2-integration-camera-foliage` · [Draft PR #27](https://github.com/tahazarif10/worldforge-sim/pull/27) · Reference hardware: AMD R9 M395X / Godot 4.7.2 Mono OpenGL Compatibility.

## Exact implementation

- Geometry: Camera3D true orthographic ray intersection for ground-relative movement and mountain/forest-height zoom anchor. Numerical math is tested without Godot by Camera3D.Smoke.
- Picker: grid DDA walks terrain cells on a ray; no fixed 0.22 unit sampling loop. The surface is still per-tile plateaus/cliffs, not future continuous heightfield geometry.
- Navigation: keyboard pan uses screen-ray differential, making world shift correspond to requested visible direction; current map keeps finite bounds.
- Vegetation: region-local 16x16 foliage batches are rebuilt only for changed terrain chunks, *not all world foliage*. Initial world creation still builds all chunks. No streaming, culling/LOD or artist-quality models claimed yet.
- Data foundation: `WorldSim.Regions` code merged in PR #24 with 64x64 data regions, LRU snapshot caching and checksum codec; **the visual lab is not yet consuming this new store**.
- Telemetry: bounded per-frame `FrameTimeProbe` records `_Process(delta)` intervals, reports p95/p99/max and saves F9 CSV to `H:\WorldForge-Sim\artifacts\performance`. This is NOT GPU time or response latency and cannot measure any editor freeze that never resumes.

## First-run steps on Windows H: machine

**Close both game and Godot Editor.** Do not force reset local modifications to `project.godot`.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-integration-camera-foliage
.\scripts\verify-v2-integration.ps1
```

The verification script automatically enters the H-only dev environment, executes the camera, region and frame-time tests, builds Godot C# and runs the 3D scene separately (not in Editor). It does not change `project.godot`. If switching branches fails because of local changes, stop; do **not** force/reset.

## Manual interactive matrix

- [ ] Right-drag 100px horizontally and vertically at 1× and over mountains. Direction and speed must feel natural; no jump.
- [ ] Zoom wheel while mouse points to a mountain top, forest, shoreline and water. Aimed tile must stay under cursor unless at map boundary.
- [ ] WASD and arrows: equal visible pan speed, both axes. Test when zoomed out/in.
- [ ] Paint Forest/Ocean/Mountain and Meteor under pointer. Check no gaps during quick mouse motion; report wrong hits at cliff sides.
- [ ] Spawn and move 100 Person actors at 1× and 4×; Pause and New World.
- [ ] Paint near a 16-cell chunk boundary; verify all nearby vegetation updates and far away trees do not disappear.
- [ ] F2 shadows ON/OFF and compare performance.
- [ ] Play 60 sec idle + 20 sec painting + 20 sec dragging, then press **F9** to save frame CSV. Collect generated `v2-frames-*.csv` and screenshot of actual game HUD.
- [ ] Compare p95/p99 with previous V2; remember recorded series is last 7200 frames (rolling window) and is NOT automatically separated by action.
- [ ] Confirm no new renderer/GLB import runtime errors in `artifacts\visual-lab\godot-3d-console.log`.

## Acceptance and merge

Automated GitHub CI plus local H:-based build are necessary but **NOT sufficient**. This PR remains Draft until user reports genuinely improved drag/zoom accuracy, no gameplay regression and measured AMD frame-times. Main 2D scene remains unchanged. Separate Godot editor hang issue #12 still open; do not infer it is fixed.

## Roadmap after this integration

1. Replace all eager world-wide visual chunk creation with **visible-region streaming** using the merged `RegionStore`.
2. Model correct smooth terrain heights/material blending and matching collision picker.
3. Import licensed/created coherent author-quality trees/rocks/buildings with LOD, bound per-chunk MultiMesh and low/medium/high presets.
4. World saving with atomic file write and journal (current `RegionCodec` is serialization only), then larger worlds.

