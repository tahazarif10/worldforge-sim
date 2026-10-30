# V2 — Standalone 3D visual lab acceptance

This is an **experimental alternate scene**, not a replacement of the stable 2D game. The user rejected the M1.4 pixel-art direction; the selected research-backed direction is 3D with an orthographic/isometric camera and licensed cohesive nature art to follow.

**Issue:** [#17](https://github.com/tahazarif10/worldforge-sim/issues/17)  
**Research:** [V2_GRAPHICS_RESEARCH.md](V2_GRAPHICS_RESEARCH.md)

## Current 3D prototype scope

- Genuine 3D terrain chunks, elevation and vertical cliff sides (not scaled 2D tiles)
- Card-based palette applied to lit meshes and foamy shoreline accents
- Batched 3D tree canopies/trunks, rocks and shrub/flower elements (placeholder geometry, not final authored asset quality)
- Animated low-cost water spatial shader for OpenGL Compatibility
- Single warm directional light with real shadows, ambient color
- Real Camera3D orthographic/isometric panning and screen-point zoom
- Same `WorldSim` simulation, Person/Meteor and land/ocean/sand/forest/mountain brushes, tick/pause speeds and New World
- Reuses GameHud; F2 toggles 3D shadows, ESC quits; 2D main scene remains unchanged

**Important honesty:** Primitive meshes still have a low-poly look; the target **premium** style requires authored Quaternius glTF nature models, materials and art direction. This prototype proves whether depth and lighting are better than M1.4 and whether the old GPU handles it, not a final screenshot-quality game.

## Installation/build on user Windows host (H: only)

All intentional tools, Git downloads, NuGet packages, temp and logs must remain on H:. Never use `git reset --hard` or discard local `project.godot` edits.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-stylized-3d-visual-lab
. .\scripts\enter-dev.ps1
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-visual-lab.ps1
```

The launcher explicitly selects `res://Scenes/VisualLab3D.tscn` via Godot's documented `--scene` CLI argument and logs to `H:\WorldForge-Sim\artifacts\visual-lab\godot-3d-console.log`. It does **not** change `project.godot`.

## Manual review

- [ ] Scene runs on Godot 4.7.2 .NET, AMD Radeon R9 M395X Compatibility renderer; no missing shader/import error
- [ ] Camera is noticeably angled/3D, elevation and shadows visible
- [ ] Tree/rock volumes recognizable; water visible and moving
- [ ] Right-drag pans; wheel zooms; WASD/arrows move camera
- [ ] Tool brushes paint correctly when clicking 3D terrain including edge areas
- [ ] Humans spawn / move, meteor and pause/time controls work
- [ ] New World changes seed and refreshes terrain and vegetation
- [ ] FPS and worst-frame metrics reported after 30 sec idle, 20 sec paint, and 100 citizens; not inferred from CI
- [ ] Try F2 to compare sun shadows on/off; report whether FPS changes and whether editor itself hangs (separate #12)
- [ ] Screenshot actual standalone 3D game at 1280×800 and zoomed in
- [ ] Compare art direction with previous M1.4 screenshot and user feedback

## Quality gate

**Do not switch main scene/default visual mode or remove 2D fallback until approved.** Existing 2D game remains the safe version. CI C# compile is not a substitute for actual Godot startup/shader import/visual quality and GPU testing. If this is worse than expected, return to quality target and integrate real licensed models before adopting.

## Optional: import genuine authored nature models (separate from geometry demo)

The visual lab can now replace a capped subset of placeholder tree/rock objects with **actual .glb models** and their materials from a legal CC0 nature pack. Because GitHub integration only writes UTF-8 source files here, asset binaries are **not included** or claimed downloaded. You must obtain the pack from its official publisher and save the ZIP **directly on H:** (e.g. `H:\Downloads\StylizedNature.zip`). Suggested pack: Quaternius Stylized Nature MegaKit (<https://quaternius.com/packs/stylizednaturemegakit.html>) or Kenney Nature Kit (<https://kenney.nl/assets/nature-kit>).

```powershell
Set-Location 'H:\WorldForge-Sim'
. .\scripts\enter-dev.ps1
.\scripts\import-nature-pack.ps1 -ZipPath 'H:\Downloads\YOUR_NATURE_PACK.zip'
# Re-import copied GLB resources once (do not run the editor interactively).
$godot = 'H:\WorldForge-Sim\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
& $godot --headless --path 'H:\WorldForge-Sim' --editor --import
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-visual-lab.ps1
```

The importer accepts only H:-based ZIPs and stages/copies inside the project on H:. It selects one `*tree*.glb` and one `*rock*.glb` by filename; you can override `-TreePattern` and `-RockPattern` to choose different files. The preview caps imported scene-node instances to **140 trees and 80 rocks** on the reference GPU; the remainder use batched fallback primitives. Production art needs imported meshes baked into a MultiMesh renderer to scale to larger forests without hundreds of scene nodes. Keep a record of model source and verified CC0 license before distribution.

Note: the current asset extractor does **not** import .gltf + external texture/.bin files, only self-contained .glb. Do not assume a downloaded archive contains GLB until inspected; if it doesn't, use a GLB pack or implement a dependency-preserving glTF importer. If import errors arise, revert to source branch without discarding user data.
