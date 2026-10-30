# M1.4 — Original biome pixel art and shoreline transitions

**Issue:** [#7](https://github.com/tahazarif10/worldforge-sim/issues/7) · **Tracker:** [#2](https://github.com/tahazarif10/worldforge-sim/issues/2).

## What changed

Prior M1.3 terrain rendering used one flat RGBA texel per tile then scaled it to 8 world units. M1.4 now draws **8×8 distinct texels per world tile** at one texel per logical world unit, using a deterministic procedural pixel-art generator:

- Deep water: dark tonal bands and small wave fragments
- Shallow water: lighter ripples and brighter blue
- Sand: sandy granular tones and wet/coastal highlights
- Grass: grass blades, occasional flowers and shade variation
- Forest: green multi-tone tree-crown silhouettes and small trunks
- Mountain: angular light ridges, shaded rock faces
- Burned land: dark ash and occasional ember-brown detail
- Shore: based on north/south/east/west water-land neighbors, foam and beach-border pixels connect along tiles
- Adjacent forest/grass: subtle seeded dark-green blending

**Art is original code** under `Scripts/Rendering/TerrainPixelArt.cs`, not taken from WorldBox or other games. The sprite concept may evolve to hand-authored atlases, but this stage intentionally establishes a coherent original procedural style without proprietary assets.

## Architecture and invariants

- WorldSim `WorldState` logic and deterministic map generation are **unchanged**.
- Rendering buffer is `world.Width * 8` by `world.Height * 8` in RGBA8, with `TextureFilter=Nearest`; the destination world rectangle remains `world.Width * 8` by `world.Height * 8`.
- Exact camera paint/world coordinates and actor positions therefore remain unchanged.
- `TerrainPixelArt.Paint` updates only dirty tile rectangles in the CPU RGBA buffer, without allocating a new render buffer every paint.
- MarkBrush includes a **1-tile halo** to refresh adjacent shoreline foam after water-land changes.
- GPU upload via Godot `ImageTexture.Update` still uploads **the entire image**, once per flushed frame. Initial 160×120 sample map => 1280×960 texture, ~4.9 MB raw RGBA8; old 160×120 texture was ~0.08 MB.
- The new texture is significantly larger than M1.3; no performance gain claimed and Windows GPU/RAM/FPS measurements are a release requirement.
- `Seed` controls visual variation; same seed and terrain produce byte-identical RGBA buffers. Visual decoration is not part of WorldSim simulation RNG and cannot alter citizen behavior.

## Automated tests

`tests/TerrainPixelArt.Smoke`:
- [ ] 8×8 RGBA8 texels per terrain tile
- [ ] deterministic same-seed regeneration
- [ ] different visual seeds change decoration
- [ ] opaque alpha throughout
- [ ] local paint plus halo matches full rebuild byte-for-byte
- [ ] outside dirty region remains unchanged
- [ ] sand/water both sides of shoreline highlights
- [ ] shallow/deep visual distinctions
- [ ] invalid buffer size rejected

GitHub CI also runs prior WorldSim simulation, CameraMath and DirtyTileRegion smoke tests plus full Godot C# compile.

## Actual Windows acceptance (H: only)

1. Exit running WorldForge and Godot Editor. Save any local user edits to `project.godot`; do not overwrite or reset this file.
2. In PowerShell under H: run:

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch main
git pull --ff-only origin main
. .\scripts\enter-dev.ps1
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' run --project '.\tests\TerrainPixelArt.Smoke\TerrainPixelArt.Smoke.csproj' -c Release
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-game.ps1
```

3. Visually check trees, mountain slopes, grass and wave details, sand/water foam, and compare at 1× / 2× / 3×. Capture actual game screenshot (not editor preview).
4. Paint Ocean beside Sand, remove Ocean with Land, and confirm foam transitions update without stale edges. Paint continuously to test memory/CPU impact.
5. Try Person spawn, movement, Meteor, New World, Pause, speeds and cursor-centered zoom. No old controls may regress.
6. Inspect FPS and Worst-frame readout both idle and while painting, and compare to M1.3 if available. Collect CPU/memory if slower. **Do not claim acceptable performance without evidence.**
7. Godot Editor freezes are separate outstanding issue #12. Prefer `scripts/run-game.ps1` standalone.

## Known limitations

- This is original **procedural pixel art**, not polished hand-painted sprite atlas with dozens of variations; advanced artistic direction continues after screenshots.
- Shore transitions are cardinal-neighbor based; diagonal corners and animated water are planned in M1.5 / M2.
- GPU updates use whole texture; large worlds will need tiled/chunked GPU resources before scaling to 1024×1024 simulation tiles.
- No project asset downloads, installations, caches, temporary files or intentional outputs on C:. Canonical root `H:\WorldForge-Sim`.
