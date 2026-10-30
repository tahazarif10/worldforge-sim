# V3.0 rejected: playable preview rollback and redesign gates

Date: 2026-10-30 · [Critical regression #46](https://github.com/tahazarif10/worldforge-sim/issues/46)

## User verdict

User explicitly rejected **both visual appearance and movement/camera quality** of the independent V3 preview compared with the previously accepted V2.9 game. This is a failed release candidate, **not** a successful graphical or playable large-world milestone. The 8192² coordinate/math and region persistence tests are useful engineering artifacts only.

## Source-backed causes

These are **source audit findings**, not speculative measured FPS causes:

1. **Loss of established art pipeline.** `RegionalWorld3D.BuildChunk` hand-authors an inferior terrain-color pass and per-cell water plane with `regional_water.gdshader`. It does **not reuse** the approved `VisualTerrain3D` art pipeline, stratified coast/foam shader, vegetation composition or `WorldState` inhabitants. It therefore cannot be presented as preserving the look of V2.9 merely because it imports the same Kenney GLBs.
2. **Actual visible-map holes are possible by design.** `MaxVisibleChunks=48` truncates an arbitrary larger visible chunk footprint by proximity to camera. It creates no LOD proxy for dropped chunks; those locations remain empty. This is not a quality setting or a legitimate fix for framerate.
3. **Slow residency visually lags the camera.** Initial 12 chunks and Eco 2/50ms for later updates can leave newly visible terrain absent while the camera moves. Every new chunk also builds a `SurfaceTool` terrain/water mesh and authored prop batches on the main thread, a plausible source of frame spikes. **No host p95/p99 data exists**, so this is an identified risk, not a quantified bottleneck.
4. **Real-camera feel was not tested.** `StrategyCameraRig` is shared mathematically, but its scene-level interaction with missing/rebuilding terrain, navigation bounds, brush ray hits and map-biome distribution differs dramatically between V2 and V3. A short headless scene start in CI never exercised fast pan/zoom, mouse dragging, teleports, or input responsiveness.
5. **Gameplay parity absent.** V3 has no citizens, meteors, running simulation or previous HUD; users reasonably expected a bigger version of the same game, not a stripped technical map viewer.

Godot's documentation confirms that **MultiMesh culling applies to a whole MultiMesh**, not each tree, making localized batches appropriate. For large worlds Godot recommends tile loading and LOD rather than simply truncating view geometry without a fallback:
- https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html
- https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html
- https://docs.godotengine.org/en/latest/tutorials/3d/mesh_lod.html

## Immediate product decision

**The approved V2.9 remains the only user-facing 3D gameplay baseline.**

- `scripts/verify-v3-regional.ps1` runs data and V2 regression checks then **launches V2** by default.
- `scripts/run-regional-world.ps1` now **launches V2** by default. Only explicit `-ExperimentalV3` starts the unsuccessful architecture experiment.
- Source for the regional generator, checksummed saved biomes, visualization experiment and tests is kept intact for isolated development; there is no deletion, no `git reset`, no overwriting `project.godot`.
- The project README explicitly warns against advertising V3 as a playable improvement.

## Non-negotiable next acceptance gates

1. **One engine, multiple data sources.** Extract a terrain-source interface from the proven V2 `VisualTerrain3D` renderer (biomes, continuous slopes, coast, Kenney instances and citizen interfaces), with adapters for `WorldState` and `RegionStore`. V3 may replace data storage, **not** replace the appearance/camera behavior.
2. **Visual parity first.** The same seeded 120×88 reference region in both data-source modes must produce equivalent terrain topology, biomes, tree/rock placements and opaque coast-water color/foam, validated in real Windows screenshots at matching viewport/camera/quality. This requires matching the procedural generator seed/algorithm in addition to rendering code. Do not claim pixel-perfect equality before implementing it.
3. **No visible empty map when moving.** Maintain a low-detail terrain/water fallback for newly visible unbuilt chunks or enforce a zoom/camera envelope supported by committed render budgets; eliminate current arbitrary 48-visible-chunk truncation from user-facing builds. Distance-based LOD, perhaps low-resolution far terrain and detailed near 16×16 chunks, with bounded main-thread uploads.
4. **Input parity and latency.** The same right-drag/zoom anchor and brush hit algorithm must be reused. Test 30s pan, 30s zoom, rapid teleport and land edit while streaming; instrument p50/p95/p99 frame intervals **and actual input-to-camera response**, not just average FPS.
5. **Low-end performance.** First validate against the existing AMD R9 M395X Compatibility rendering baseline. Compare matched scene, resolution, zoom, seed and identical movement script. 30 FPS is a ~33.3ms budget, 60 FPS ~16.7ms, not an achieved result. Provide Eco without degrading near-camera terrain, silhouettes, authored models, citizens or approved sea.
6. **Gameplay and save parity.** Migrate inhabitants and current paint/disaster tools using regional update limits, deterministic state and explicit persistence. The technical 8192² region-coordinate test alone is not a playable 8192² god sandbox.
7. **Human art acceptance required.** Official Godot Mono Linux headless import+boot and C# CI are necessary but not proof of aesthetics or Windows camera smoothness. The user rejected V3 despite green CI; no next V3 feature is promoted without screenshots and user approval.

## Scope / workflow

This PR fixes **the wrong default launch path, README, release status and regression guard**, not the deeply flawed V3 engine. The engineer-only prototype can be inspected with `-ExperimentalV3`. It does **not** pretend the user's disliked scene is repaired.

No Codex CLI, Codex Cloud or ChatGPT Work invoked for these source changes; changes used a directly connected GitHub integration. GitHub Actions tests may consume GitHub runner resources, but do not establish the user's OpenAI weekly-credit accounting.

Everything intentionally installed or generated by the Windows project stays on H:\WorldForge-Sim; respect locally edited `project.godot`.
