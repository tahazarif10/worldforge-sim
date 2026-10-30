# V3.1 recovery — identical approved V2 scene with opt-in regional terrain cache

**Purpose:** recover from the visually/interaction-regressed V3 prototype without risking the accepted V2.9 game. Issue [#48](https://github.com/tahazarif10/worldforge-sim/issues/48); hard-blocking UX regression [#46](https://github.com/tahazarif10/worldforge-sim/issues/46).

## Architecture decision

The original independent `RegionalWorldLab3D.tscn` is **still rejected**. We do not fix its art by copying colors or swapping camera offsets. Instead, the **approved V2 scene is the only render/camera owner**.

```
Existing playable V2 (unchanged default):
  WorldState(120 x 88)
       |
       +-- DirectWorldTerrainSource ------------+
       |                                        |
       +-- (opt-in) RegionMirrorTerrainSource --+--> VisualTerrain3D
              |                                        |
              +-- RegionStore 64x64 LRU               +-- V2 lagoon/coast mask
                  baseline reads EXACT WorldState     +-- original sculpted slopes
                                                      +-- Kenney CC0 tree/rock batches
  WorldState.People ---------------------------------> V2 citizen meshes
  StrategyCameraRig --------------------------------> exact V2 drag/zoom/brush
```

- `ITerrainTileSource` is a small WorldSim-owned, **Godot-free** contract for bound dimensions/seed, terrain reading and post-WorldState-edit synchronization.
- The normal path uses `DirectWorldTerrainSource`, equivalent to prior `WorldState.Tile` calls. No extra cache/height/material/generator changes in the approved launch.
- `RegionStore` now accepts an **optional baseline tile sampler**; omitted default is the existing `RegionGenerator.TerrainAt`, preserving previous V3 data generator tests. Opt-in `RegionMirrorTerrainSource` uses `WorldState.Tile` as the sampler instead — crucial because the V3 generator's biomes differ radically.
- RegionMirror starts with **zero resident regions** and caches up to eight 64x64 regions as visited. In this 120x88 source there are only a few. If evicted, the same authoritative WorldState (including recent paintings/meteor effects) is re-sampled. No separate full-world terrain copy is allocated.
- `VisualTerrain3D.BindWorld(world, terrainSource)` uses the same existing materials/geometry/water shader/vegetation path for either source. `MarkChanged` synchronizes the affected cells before mesh or coast sampling. The SAME StrategyCameraRig and `WorldState.People` are retained.
- `VisualLab3D` selects the mirror **only** through explicit Godot user args `-- --terrain-backend=region-mirror`. Regular `scripts/run-visual-lab.ps1`, default `Scenes/Main.tscn`, `project.godot`, and rejected V3 experimental scene are not replaced. New World resets the region cache to a fresh seed with the same source route.

## What is and is not proven

**Proven by automated parity tests, if CI green:**
- Every terrain enum across the 120x88 V2 seed for several seeds and after brushes/meteor; no generator drift after cache eviction with resident max set to one.
- Full byte-for-byte coastline mask, seeded vertex color gradients, exact shared-corner terrain heights (the same DDA picker sees the same surface).
- Unchanged WorldState citizen RNG/ticks while running region read cache; edited tiles stay consistent across chunk and 64-cell borders.
- Exact same Godot 4.7.2 Mono scene loads with normal direct terrain and region mirror, validating the opt-in arg is active in the engine log.

**NOT yet proven:**
- Exact GPU pixel-identical screenshots or frame-time parity on the user's AMD R9 M395X; Linux headless CI is not a graphics benchmark.
- RegionStore as the independent authoritative world biome system. In this step the region cache deliberately reads V2 WorldState; gameplay simulation is still 120x88/limited by `WorldState` max 1024, not true 8192x8192 living world.
- Background mesh workers, far-terrain LOD and chunk teleport fallback; the rejected V3 problems need separate GPU-backed A/B host validation before any user-facing promotion.
- RegionMirror uses a mutable reference baseline. Edits performed OUTSIDE `WorldState.Paint` / `WorldState.Meteor` and without `MarkChanged` synchronization could leave already-resident cached cells stale; the VisualLab3D gameplay event path calls MarkChanged for the relevant painting tools. Production architecture will require explicit tile edit events to eliminate this coupling.

## Windows test with H: only

**Close running Godot and game. Never reset locally modified `project.godot`.**

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v3-1-renderer-source-parity
.\scripts\verify-v2-region-parity.ps1
```

After PR merge, switch back to main and pull fast-forward, then run the same script. It runs a pure.NET exact parity test, old V2 art regression suite, CC0 GLB import and C# build, then launches **the approved V2 scene** using the regional *data* cache.

Compare against `.\scripts\verify-v2-art.ps1` at the **same window resolution and seed**. Startup scene logs must show:
- Normal: `terrain source = direct`
- New opt-in: `terrain source = region-mirror`

Manual acceptance:
- [ ] Zoom overview to forest/coast and press F8; screenshots have identical terrain/coast/tree structure, lighting and shadow state.
- [ ] Repeated right-drag, WASD, mouse-centered zoom (forest mountain shoreline) and brush targeting must feel **indistinguishable** from V2.
- [ ] Paint Forest, Ocean, Mountain, Meteor near x=63/64 and x=79/80; undo through another brush, then return. No old coastline shade, holes, ghost trees.
- [ ] Spawn and move citizens, Pause/Advance, New World, F3/F4/F5/F6/F7 and F11/F12 all still work. Main 2D fallback unaffected.
- [ ] Record F9 frame CSVs for 30s idle, 30s camera movement and 30s painting in both modes. Do not claim negligible overhead until p95/p99 on AMD are measured.
- [ ] Confirm same CC0 GLB model meshes, water shader and original V2 camera code are running. No separate `RegionalWorldLab3D` scene must load.

**Release gate:** a PR may merge once all source parity tests and the official Godot import/direct+mirror startup CI are green (default is unchanged). This is an **internal architecture milestone**, not acceptance to enlarge the playable map. The product gate [#46](https://github.com/tahazarif10/worldforge-sim/issues/46) remains OPEN until real user Windows image/control acceptance. No Work/Codex invoked; GitHub connector and GitHub Actions only.
