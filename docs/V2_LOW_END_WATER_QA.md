# V2.6 — Low-end water and frame-stable chunk loading

**2026-10-30.** Scope: improve the real low-cost ocean and keep the user's approved authored trees/terrain art at stable input responsiveness on old GPU. [Issue #35](https://github.com/tahazarif10/worldforge-sim/issues/35).

## Research-based selection

Godot 4 Compatibility is the official renderer optimized for older desktop and mobile hardware. Do not use Forward+-exclusive SSR, volumetric fog, GPU compute or translucency overdraw to simulate an attractive sea. Godot's performance guidance warns that live shadows, excessive transparencies and large groups of visible objects can harm weaker GPUs; use sparse per-chunk MultiMesh/LOD. A dynamic `ImageTexture.Update` is cheaper than reallocating it when dimensions remain unchanged.

Sources:
- [Godot renderer features](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html)
- [Godot optimizing 3D performance](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html)
- [Godot MultiMesh culling caveats](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html)
- [Godot ImageTexture.update](https://docs.godotengine.org/en/latest/classes/class_imagetexture.html)

## Actual implementation

### Water

- `WaterCoastMath.BuildMask`: deterministic two-pass Manhattan distance to nearest coast. `O(width*height)`, not scanning the whole map for each pixel. R channel encodes a **three-cell soft shoreline band**; G shallow/deep class; B seeded blue nuance; A opaque. A water edit updates the existing RGBA8 image, but no water texture is uploaded continuously at 60 FPS.
- `stylized_water.gdshader`: **one bilinearly filtered mask sample**, warm near-shore turquoise and darker deep ocean, two low-frequency non-geometry wave harmonics, light foam motion and restrained specular glints. No depth/screen reads, alpha blend, planar reflection camera, refraction or geometry displacement.
- `water_detail` uniform toggles secondary glimmers; when Eco mode, only primary waves and coast shading remain. **No additional draw calls or ocean meshes.**
- Note: current 120×88 preview uses one world-sized mask; future 4096²/8192² worlds must use **resident-region masks**, not a global RGBA array. This PR does not claim huge-world water streaming.

### Avoiding pan/zoom hitches

- `ChunkLoadBudget.Choose` is pure.NET, deterministic, chooses nearest missing chunk IDs to the camera ground footprint. It builds no more than specified number each visibility refresh.
- Initial 120×88 startup still preloads up to 64 nearby chunks before first gameplay frame for fast visual readiness. Subsequent 50-ms visibility refreshes build at most **4 chunks** (Balanced) or **2 chunks** (Eco) while the camera moves. Previously *all* missing visible chunks could build in one 0.1s update.
- The scene still uses 16×16 vegetation and terrain batch cells, camera-footprint culling, no player-driven global asset reimport. This is a **count budget**, not yet a strict millisecond wall-clock budget: a single very dense chunk could still hitch. Actual per-chunk latency needs host profiling before declaring "no lag".

### Quality presets

**F4** toggles:
- **Balanced** (default): all 10 authored CC0 Kenney models, existing gradient terrain/cliff layers, live sun shadows, secondary surface water detail; 4 chunks per view update.
- **Eco**: exactly the same authored models, terrain and palette; disable dynamic sun and instanced foliage/citizen shadows; remove secondary small-water glint math; 2 chunks per update. Do not lower texture resolution or make trees into spheres.
- F2 continues to toggle directional sun shadows manually. F3 surface art before/after, F8 H:-based PNG, F9 H:-based frame interval CSV, F10 shows live resident/requested counts and selected quality.

Eco mode helps only when GPU or chunk build load was the bottleneck. CPU entity simulation, old hardware driver problems and Windows editor hangs remain separate. Never guarantee lag-free on every GPU.

## Current automated coverage

`tests/WaterAndStreaming.Smoke`: coast distance 1/2/3/4, crisp/soft band falloff, seeded repeatability, repainting shore to sea/back, deep/shallow water mask, invalid dimensions, 512² O(area) CPU test, deterministic 8192² planner selection of <=3 nearest visible regions, resident exclusions and zero budgets.

Existing WorldSim/Region/Camera/VisibleChunks/Art tests remain. `.github/workflows/visual-lab-ci.yml` runs official Godot Mono 4.7.2 headless import, C# build and alternate scene startup on Linux. It **cannot** prove Windows GPU visual smoothness.

## User Windows review — do not erase uncommitted files

On H:-based portable Windows setup:

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-lowend-water-streaming
.\scripts\verify-v2-art.ps1
```

If branch switch is blocked by locally modified `project.godot`, do not reset, clean or force switch. Do not install on C:.

Evaluate at exact same seed and camera position as merged PR #33:
- [ ] Sea dark/teal gradients and foam feel more natural; not heavily pixelated or filled with white wave noise.
- [ ] Paint coastline, remove beach with Ocean brush, create it with Sand brush. Within the 0.16 s terrain update, foam should follow new shoreline with no stale previous edge.
- [ ] Forest trees, mountains and biome ground gradients look **identical** in Balanced compared with PR #33; no changes to their art geometry.
- [ ] Pan/zoom quickly and keep camera moving for 30s: no large multi-chunk stutter or prolonged missing terrain; load nearest chunks first; inspect F10 pending count.
- [ ] Switch F4 Eco; forest still has real models but lower expensive shadow load. Compare F9 measurements for 60s Balanced and 60s Eco under the **same workload** and with same F2 shadow state at beginning.
- [ ] Capture F8 close-up and overview PNGs (saved H:), F9 CSV p95/p99/max. FPS targets: 60 FPS ~16.7ms budget, 30 FPS ~33.3ms; report actual host results, not promised values.
- [ ] WorldSim brush, people, meteor, pause, new world and zoom on mountains still correct; no shader/import error.

**Acceptance/merge:** CI scene import/build/short run and pure.NET test green, plus actual Windows screenshots/F9 p95/p99 before claiming performance. Keep as additive opt-in 3D scene; do not touch `project.godot` or default 2D scene. Future #34 true sculpted geometry and regional renderer remains separate from low-cost water quality.
