# M1.3 — Separated terrain, entity and future effects rendering

**Tracker:** [#2](https://github.com/tahazarif10/worldforge-sim/issues/2) · **Task:** [#6](https://github.com/tahazarif10/worldforge-sim/issues/6) · **Dependency:** merged M0 / M1.0 / M1.1 / camera M1.2.

## Before

`GameRoot._Draw()` drew an entire raster texture **and** all citizens together. A 0.18s simulation tick invalidated the same CanvasItem even if only one actor moved. Brush input recopied all 160×120 terrain samples to CPU image and performed an image texture upload, even for a small 3-cell-radius brush.

## After

```text
GameRoot : Node2D         (input, world stepping, camera, UI)
 |- TerrainLayer          (TerrainRenderer, z=0)
 |   |- owns Image/ImageTexture
 |   |- processes dirty tile bounding region once per process frame
 |   '- keeps persistent texture when size unchanged
 |- EntityLayer           (EntityRenderer, z=10)
 |   '- redraws citizens only on spawn/death/tick/world switch
 |- EffectsLayer          (z=20; reserved, no VFX behavior yet)
 |- Camera2D              (world-space)
 '- CanvasLayer           (HUD: no world-space scrolling)
```

`WorldSim` remains Godot-free. Terrain and people share the **same** `WorldState` reference; a new world explicitly rebinds both renderers. No citizen list copies or per-frame texture recreation.

### Dirty-region contract

The pure .NET `DirtyTileRegion` maintains an inclusive bounding rectangle of changed cells. Adjacent/overlapping input events coalesce into a single conservative rectangle. It clamps at world edges and clears on consumption. Dirty CPU pixels are recolored only within this region.

**Important GPU nuance:** Godot `ImageTexture.Update(Image)` still uploads the backing full image to the GPU, even if only part of the CPU image was recolored. Thus M1.3 reduces CPU pixel writes and independent canvas redraw pressure; **it does not yet implement a GPU subimage/partial texture upload**. Do not misreport that optimization.

### Invalidation matrix

| Event | Terrain CPU recolor | Texture upload | Citizen redraw |
|---|---|---|---|
| Idle frame | no | no | no |
| Simulation tick | no | no | yes |
| Person spawn | no | no | yes |
| Paint soil/forest/ocean/sand/mountain | bounded dirty rectangle, on next frame | once per frame | yes (paint may eliminate citizens) |
| Meteor | bounded dirty rectangle, on next frame | once per frame | yes |
| New world | all tiles | once | yes |
| Pan/zoom | no | no | viewport updates drawing placement |

`TerrainRenderer.TextureUploads` and `RecoloredPixels` are development counters but **not** GPU profiler metrics. `EntityRenderer.DrawPasses` tracks draw invalidations, not actual GPU draw calls.

## Automated acceptance

- [ ] Existing seven WorldSim simulation checks
- [ ] Existing CameraMath smoke checks
- [ ] New `tests/DirtyTileRegion.Smoke` checks for empty/edge/clipping/merge/full invalidation
- [ ] Godot C# compile; strict warnings-as-errors CI
- [ ] Real Windows/H: build and standalone game run

## User acceptance checklist

1. On the H: checkout: `git status --short` **first**, then `git fetch origin`, then `git switch feat/m1-render-pipeline`. Never reset local `project.godot` modifications; contact if branch switch conflicts.
2. Dot-source `scripts/enter-dev.ps1`.
3. Run `& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' run --project '.\tests\DirtyTileRegion.Smoke\DirtyTileRegion.Smoke.csproj' -c Release`.
4. Run `& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false`.
5. Launch `scripts/run-game.ps1` (standalone; editor hangs tracked separately as #12).
6. Check identical terrain and actor appearance at seed `20261030`; pan/zoom with pointer editing at 1× and 3×.
7. Test Land, Ocean, Sand, Forest, Mountain, Person, Meteor; Pause, 1×/2×/4×; New World.
8. Paint continuously (fast mouse movement) and confirm no missing cells, stale texture, or citizen ghost remnants.
9. Check Godot output has no runtime errors. Optional measure FPS/CPU/memory on R9 M395X and compare to previous build with identical seed and viewport; do not invent results.

## Limits and follow-ups

This PR is **architectural, not a completed graphics overhaul**. A separate M1.4 issue provides original biome textures, transition masks, atlas/palette enhancements; M1.5 handles water movement. EffectsLayer is currently intentionally empty. CPU dirty rectangles may still be expensive in extreme zoom/large terrain workloads, to be measured on hardware.

**No intentional project download/install/cache/output on C:. Local root H:\WorldForge-Sim.**
