# ADR 0001 — One accepted V2 visual/camera pipeline, Compatibility first

- **Status:** Accepted — records existing user acceptance and the recovery choices already implemented in PR #47/#49
- **Date:** 2026-10-30
- **Supersedes:** none (first formal WorldForge V3 ADR)
- **Scope:** User-facing world renderer, camera and low-end GPU tier

## Context

V2 in `Scenes/VisualLab3D.tscn` earned explicit positive user feedback for its stylized 3D terrain, water, forests, mountain look and responsive camera on the Radeon R9 M395X. The separate V3.0 `Scenes/RegionalWorldLab3D.tscn` was an architectural proof of 8192×8192 logical addressing, but the user rejected both **visual appearance** and **camera/movement**. Postmortem [#46](https://github.com/tahazarif10/worldforge-sim/issues/46) identifies a replacement sea/terrain pipeline, loss of citizens and arbitrary render chunk truncation with no visual fallback.

The project's hardware gate is Windows, Godot 4.7.2 Mono/.NET 10, AMD R9 M395X, OpenGL Compatibility. Godot [renderer overview](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html) documents Compatibility as suitable for older desktops with no required compute shader/Forward+ stack.

## Alternatives

1. **Rebuild large-world renderer independently (rejected):** duplicates art and camera, forces reapproval for every effect; actual V3 failure.
2. **Flat/2D atlas for speed (rejected):** conflicts with explicit user requirement for true stylized 3D and depth.
3. **Shared V2 renderer/camera + alternate region data source (selected):** incremental conversion, exact visual regression tests and reversible flags; more interfaces and constraints initially.
4. **Require Vulkan/Forward+ (rejected for baseline):** not supported as a mandatory path on target older GPU.

## Decision

- Only the approved **`VisualTerrain3D` / `StrategyCameraRig` / stylized water / Kenney CC0** visual path is the baseline for new worlds. It may be refactored in small parity-tested slices, **not recreated in another inferior scene**.
- Do not silently change the V2 sea shader, coast color/foam, mountain silhouette, forest composition, brush picking, camera pan/zoom behavior or human HUD.
- Default code path remains `DirectWorldTerrainSource` while region mirror/migration is opt-in. RegionStore prototype generator is not equivalent to V2 WorldState; migration needs explicit parity/provenance.
- Future LOD/streaming must cover **every viewport-visible land/water cell** with at least an artistically acceptable coarse representation; never solve frame-time pressure by leaving holes.
- Keep Compatibility renderer mandatory as baseline. Optional premium render effects can use different higher-end hardware behind separate gated settings but cannot reduce lowest-tier usability.
- `Scenes/Main.tscn`, user-modified `project.godot` and H:-portable local files must remain intact without explicit user approval for changes.
- User-facing promotion **requires real AMD screenshots, right-drag/zoom feel and F9 p95/p99 measured under a matched reproducible scenario**; Linux Godot headless tests prove build/start only.

## Consequences

**Positive:** consistent art identity, less migration risk, tests with exact same scene, feasible H:-Windows acceptance.  
**Costs:** region interface/halo/mesh data refactor is more work than one-off V3 scene; GPU LOD and saves need careful design.  
**Known limits:** PR #49 region mirror is still a cache of WorldState 120×88. ADR acceptance does NOT mean 8192×8192 playable region simulation is implemented.

## Verification

- [x] [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47) prevents accidental default rejected V3 launch (source CI).
- [x] [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) shares exact V2 scene in direct and RegionMirror modes, source/engine CI passing.
- [x] [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50) bounds summit rock extras; core/engine CI.
- [ ] H:-Windows same-seed F8/F9 direct/mirror, accepted mountain correction and camera UX — **PENDING**, not bypassable by green CI.
- [ ] Future no-hole stream/LOD and full region authority — **NOT IMPLEMENTED**.

This ADR is a binding **target architecture and release rule**, not a claim of a completed streaming engine.
