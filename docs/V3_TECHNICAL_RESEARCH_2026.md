# V3 engineering research — Godot, streaming, compatibility and original art

**Research date:** 2026-10-30. **Purpose:** choose architecture based on published technical documentation and what WorldForge Sim's source/failed V3 prototype actually demonstrated. This is a research/decision aid; it does not claim new features have been implemented or benchmarked on AMD.

## 1. Research question

How can WorldForge Sim evolve from the user-approved 120×88 stylized 3D living scene to a large **region-simulated** world while (1) preserving its lagoon, sculpted mountain/forest colors, Kenney nature and drag/zoom, (2) avoiding missing land on fast movement, (3) limiting CPU/GPU/RAM on Radeon R9 M395X, (4) preserving deterministic region edits and saving safely?

The rejected V3 scene demonstrated that **optimizing the map architecture while replacing the renderer was a product failure**. Design should now optimize data and upload behavior **behind** the existing renderer.

## 2. Findings / evidence / resulting decisions

| Finding | Source | Implication for this project |
| --- | --- | --- |
| Compatibility renderer is based on OpenGL and targets older desktops; advanced features differ from Forward+ | Official [Overview of renderers](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html) | **Keep** Compatibility as baseline. Do not prescribe compute shaders, SSR, SDFGI, Forward+ or Vulkan-dependent art as required. Optional premium modes need separate tested hardware. |
| A MultiMesh culls its batch as a whole; it cannot individually frustum-cull every tree | Official [Optimization using MultiMeshes](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html), [MultiMesh class](https://docs.godotengine.org/en/stable/classes/class_multimesh.html) | Use **spatially local** 16×16 or measured region-level foliage batches. Don't merge all 8192² forest trees into one giant MultiMesh. Avoid one Node3D per grass blade. |
| SceneTree active modification and GPU resource changes are not generally thread-safe | Official [Thread-safe APIs](https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html), [Using multiple threads](https://docs.godotengine.org/en/stable/tutorials/performance/using_multiple_threads.html) | Compute immutable geometry/decor arrays on controlled C# workers, then instantiate/upload Godot ArrayMesh, MultiMesh and shaders on main thread. Queue capacity + cancellation and correct teardown are necessary. |
| Imported mesh automatic LOD uses camera/screen-space metrics; one MultiMesh uses the batch's chosen LOD | Official [Mesh LOD](https://docs.godotengine.org/en/stable/tutorials/3d/mesh_lod.html) | For hand-generated 3D terrain, **build explicit multi-resolution terrain** and solve seams. Imported Kenney/Quaternius models can use asset-LOD if tested, but it won't repair holes in camera coverage. |
| Rendering load is not just polygons: material reuse, fragment fill, shadows, transparency and texture reads matter | Official [GPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html) | Preserve opaque V2 sea shader, share materials, compare resolution and shadow settings, and use p95/p99 frame intervals. Don't replace art with a flat sea and call it optimization. |
| Many SceneTree nodes have CPU maintenance cost; the optimum is hardware-dependent | Official [CPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html) | Favor chunk mesh nodes + batched props + citizen MultiMesh, not independent tile/agent nodes. Profile on R9 M395X; avoid claiming fixed optimal batch size. |
| Quaternius Stylized Nature MegaKit provides textured low-poly vegetation/rocks under CC0; free Standard lacks full Source engine shaders | [Original Quaternius](https://quaternius.com/packs/stylizednaturemegakit.html), [publisher's itch page](https://quaternius.itch.io/stylized-nature-megakit) | **Optional** future hero-biome experiment; verify which models Standard ZIP contains, materials/scale/draw calls/imported bytes and license. Do not claim all 116 models are free or already imported. |
| User-facing V3 scene recreated visuals and lacked foreground/remote rendering continuity | Source [V3 rejection postmortem](V3_REJECTION_POSTMORTEM.md), [#46](https://github.com/tahazarif10/worldforge-sim/issues/46) | Never create another independent downgraded world renderer. Reject any viewport method that truncates visible chunks without guaranteed fallback. |
| WorldState seed art differs from prototype RegionGenerator art; source parity matters more than both having seed 20261030 | Source [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) tests and [V3.1 design](V3_1_RENDERER_SOURCE_PARITY.md) | Preserve the V2 generator during migration or version saved worlds explicitly; region eviction must not substitute a different biome algorithm. |
| Mountain summit flat slab was primarily dense authored rock placement rather than insufficient summit mesh triangles | Source [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50) | Favor composition/scale/placement first. Same user screenshot seed `1315511732` replay via [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) before introducing heavier peak geometry. |

## 3. Architectural options weighed

### A. Maintain one giant whole-world tile/mesh array

**Upsides:** simple, uniform pixel coordinates, easy deterministic iteration.  
**Downsides:** memory/time scale with total 8192² logical map, all-regions water mask already raw **256 MiB** at 8192² RGBA (before GPU texture allocations), save rebuild cost and unavoidable world-wide update passes.

**Decision:** REJECT for large playable worlds; retain WorldState at 120×88 as baseline until region authority is ready.

### B. Independent V3 renderer with fixed 48 active chunks

**Upsides:** simple bound on scene objects, visible fast isolated data region test.  
**Downsides:** no LOD/fallback; viewport can show **holes**; altered water/terrain/forest look and camera feedback; citizens absent.

**Decision:** REJECT as a user-facing architecture based on direct 2026-10-30 rejection. Preserve prototype only as a test of data contracts.

### C. V2 shared renderer + versioned region data + conservative fallback LOD

**Upsides:** matches approved palette/surface/camera, supports bounded cache + incremental rendering, testable in slices behind flags.  
**Downsides:** high upfront design cost in generator parity, region halos, mesh source ownership, async lifecycle, LOD seam stitching, save journal.

**Decision:** SELECT as the preferred target, subject to V3.3 physical parity and LOD benchmark; do not mark complete.

### D. Immediate compute-shader/GPU-driven terrain

**Upsides:** may speed very large-scale terrain on newer APIs/devices.  
**Downsides:** **Compatibility cannot rely on compute shaders**. Target hardware/driver baseline is OpenGL. High implementation cost, fragile old AMD support.

**Decision:** REJECT as a required baseline. May be separately researched for higher-end Vulkan/Forward+ tier only after proven separate compatibility.

### E. SurfaceTool native mesh building on worker threads

**Upsides:** potentially faster CPU utilization.  
**Downsides:** resource/RenderingServer/thread-safety and shared-material hazards, GPU sync stalls; concurrency bugs on Windows.

**Decision:** Do NOT build Godot-native resource objects in worker threads for initial implementation. Worker output pure C# mesh arrays; **main-thread** bounded native upload. Reconsider direct RenderingServer APIs only after profiling and architecture ADR.

### F. One global shoreline mask for the large map

**Upsides:** preserves exact V2 shader/sampling quickly; central logic simple.  
**Downsides:** 8192² ×4 bytes=256 MiB raw CPU, full upload, dependency on all tiles, huge edit invalidation.

**Decision:** REJECT. Create bounded coast mask patches with 1–3 tile halos matching old `WaterCoastMath` and `EnvironmentArtMath` semantics, and compare edge/mask parity byte-for-byte.

## 4. Specific implementation research before each milestone

**V3.3 baseline research**
- Instrument existing F9 frame CSV to separate CPU frame from GPU stall if Godot profiling supports it; track short slow frames rather than just 1s FPS display.
- Test both V2 direct and opt-in RegionMirror with same seed, camera pose, window size and quality, before any additional GPU modifications. If gameplay redraw differs, first inspect change synchronization path in `MarkChanged`.
- Use F8 image comparison with fixed camera/seed. The viewer's subjective good/bad is a required manual acceptance field, not a claim that a quantitative image metric alone measures aesthetics.

**V3.4 streaming design research**
- Prototype a pure managed `VisibleCoveragePlanner` that outputs detailed + fallback footprints. Test **zero uncovered cells** for screen corners and elevation envelopes at several aspect ratios and zoom levels.
- Choose queue policies with measured camera motion. Cap pending items to current screen region plus margin; superseded camera generations canceled and results discarded. Test 500 rapid pan/zoom changes without uncontrolled growth.
- Compare one pure C# array worker vs main thread all-in-one. Record `MeshBuildCpuMs` / `UploadMainThreadMs` and frame percentiles. Only add workers if actual main-thread savings justify complexity.

**V3.5 source-of-truth / saves research**
- Build a characterization test for V2's world generator across seeds, borders and known player edits before choosing an equivalent procedural generator.
- Model persistent region **manifest** and atomic root commit; crash-injection cases after every step. Do not treat per-region checksum as full transaction guarantee.
- Keep region/data/visual granularity separate, with canonical 64×64 cells and 16×16 chunk fan. Design coast/elevation halo cache based on exact math.

**V3.6 life simulation research**
- Define active-world time stepping vs unloaded regions with deterministic replay semantics, before adding thousands of entities. Options: coarse aggregate advance; background scheduled active sets; event-driven updates. Benchmark and record ADR. Do not silently freeze whole world when user pans away.
- Require cross-border ID uniqueness, reproducible RNG and no double-application of meteor/disaster events.

**V3.7 art quality research**
- Kenney already provides the stable CC0 fallback. For Quaternius Standard, first download/inspect archive on **H:**, verify original licensed source, list specific imported models, test Godot glTF import on 4.7.2 and GPU draw/VRAM per asset; create 1 small hero-biome A/B, not blanket replacement of all forests.
- Treat lighting and silhouette as more important than sheer object count. Keep mountain rock cap fix and original V2 sea for art parity.

## 5. Research pitfalls and unverified assumptions

- Godot docs labeled `latest` may describe unreleased features. Prefer **stable / version-matched** docs and verify exact 4.7.2 C# compile/import behavior in CI.
- Godot built-in LOD on imported GLBs does not imply runtime-generated quad terrain changes resolution or stitches chunk borders automatically.
- A successful CI on Linux with software/headless renderer proves API, import and scene init, NOT Windows GPU FPS, shader appearance or camera responsiveness.
- Visibility chunks are not "world streaming" if `WorldState` still loads every tile or all water pixels at startup.
- 8192² **logical coordinates** are not 8192² persistent editable terrain and living-world entity simulation. Separate the three acceptance claims.
- The user-screenshot FPS HUD showing 60 FPS / 16.7 ms worst in a 1-second interval is a **single illustrative moment**, not measured 30–60s p95/p99 under streaming stress.
- Neither Wikipedia screenshots nor another game's proprietary assets license copying its code/art/music; use style references as inspiration only.

## 6. References and verification register

**Externally published, checked 2026-10-30**
1. Godot renderer differences: https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html
2. MultiMesh culling tradeoff: https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html
3. MultiMesh class: https://docs.godotengine.org/en/stable/classes/class_multimesh.html
4. Thread-safe APIs: https://docs.godotengine.org/en/stable/tutorials/performance/thread_safe_apis.html
5. Threading considerations: https://docs.godotengine.org/en/stable/tutorials/performance/using_multiple_threads.html
6. Imported mesh LOD: https://docs.godotengine.org/en/stable/tutorials/3d/mesh_lod.html
7. GPU optimization: https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html
8. CPU optimization: https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html
9. Quaternius publisher: https://quaternius.com/packs/stylizednaturemegakit.html
10. Quaternius current download and tier information: https://quaternius.itch.io/stylized-nature-megakit
11. Kenney Nature Kit: https://kenney.nl/assets/nature-kit

**Repository evidence (not external general authority)**
- `Scripts/VisualLab/VisualTerrain3D.cs`, `VisualLab3D.cs`, `Camera3D/StrategyCameraRig.cs`
- `src/WorldSim/Regions/RegionStore.cs`, `src/WorldSim/TerrainSources.cs`
- [PR #44](https://github.com/tahazarif10/worldforge-sim/pull/44), [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47), [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49), [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50), [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51)
- [docs/V3_REJECTION_POSTMORTEM.md](V3_REJECTION_POSTMORTEM.md), [V3_1_RENDERER_SOURCE_PARITY.md](V3_1_RENDERER_SOURCE_PARITY.md), [V3_2_MOUNTAIN_SUMMIT_QA.md](V3_2_MOUNTAIN_SUMMIT_QA.md)
