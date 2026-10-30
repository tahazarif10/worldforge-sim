# WorldForge Sim

Original living-world god-sandbox simulation, influenced by the *genre* of WorldBox. This is not WorldBox and does not use its source code, art or assets.

**Status:** M0/M1 prototype tested on the user's H:-based Windows system; previous 2D art direction was rejected. A separate opt-in V2 stylized 3D/isometric visual lab is being evaluated. Default 2D scene remains available.

## Windows location policy
The project lives in **H:\WorldForge-Sim**, not under AIProjects. Keep intentional downloads, dependencies, logs, build outputs and tool caches on H:. Neither this script nor Godot can guarantee Windows or third-party software never writes OS-managed data on C:.

From PowerShell:
```powershell
Set-Location 'H:\WorldForge-Sim'
. .\scripts\enter-dev.ps1
.\scripts\acceptance.ps1
```
The dot before `enter-dev.ps1` is important: it applies environment variables to the **current PowerShell session**. The script fails if this checkout is not on H:.

Install **Godot 4.7.2 .NET/C# edition** and .NET 10 SDK **under H:** before running the full acceptance. Do not run a conventional default-path installer without checking its paths. For Godot, run `scripts/install-godot.ps1`, which downloads the official Windows x64 .NET edition to H:, checks the archive SHA-256, extracts under `H:\WorldForge-Sim\Tools\Godot\`, verifies the editor signature and creates `_sc_` beside the editor binary **before launch**. Run the editor via `scripts/run-editor.ps1`. Install the H:-based SDK with `scripts/install-dotnet.ps1`; then dot-source `scripts/enter-dev.ps1` again so the H: SDK takes precedence. The existing C: SDK is left untouched.

If both tools are installed, open `project.godot` with the Godot .NET editor, build C# and run F5.

## First playable prototype
- Procedural seeded 160x120 map with water, sand, grass, forest and mountains
- Paint terrain, spawn people, meteor impact
- Wander simulation, camera pan/zoom, pause/simulation speed
- Framework-independent deterministic core and smoke checks

There are **no** real settlements, combat, food networks, or diplomacy yet.

## Current development track: M1 Visual Foundation

- [Master tracker and seven subissues](https://github.com/tahazarif10/worldforge-sim/issues/2)
- [Visual foundation implementation plan](docs/M1_VISUAL_FOUNDATION.md)
- [Original art direction](docs/ART_DIRECTION.md)
- [Build, visual and performance acceptance checklist](docs/ACCEPTANCE_CHECKLIST.md)
- M0/M1.0–M1.4 PRs were merged after CI; visual art direction subsequently rejected, so V2 is under evaluation separately.

## New direction: V2 3D / isometric visual lab

The user requested much higher visual quality and specifically **not pure 2D**. We selected real 3D with an orthographic camera and a Compatibility-first GPU tier for AMD R9 M395X.

- [Research, comparisons and art decision](docs/V2_GRAPHICS_RESEARCH.md)
- [Windows screenshot and performance acceptance](docs/V2_VISUAL_LAB_ACCEPTANCE.md)
- [V2 issue tracker](https://github.com/tahazarif10/worldforge-sim/issues/17)

Start this **separate** V2 scene without replacing the approved 2D main scene:

```powershell
Set-Location 'H:\WorldForge-Sim'
. .\scripts\enter-dev.ps1
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-visual-lab.ps1
```

The V2 3D lab now includes ten bundled CC0 Kenney Nature Kit GLB models, shared-corner land slopes, camera-region streaming, low-cost lagoon water and a distinct F4 Eco tier. Full user hardware performance and premium art quality still require evidence. Original 2D gameplay remains the fallback.

## V3.0 region-backed world lab (separate, opt-in)

The existing V2 stylized 3D scene is the current approved art/gameplay baseline. V3 introduces a **second scene** to prove an **8192×8192 logical address space** with only 64×64 biome regions and 16×16 rendering chunks near the camera kept in memory. It does **not** run a full 8192×8192 living-world simulation or replace the original V2 scene.

In the portable H:-based Windows checkout:

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch main
git pull --ff-only origin main
.\scripts\verify-v3-regional.ps1
```

This verifies the region LRU/save format and original 3D art regressions, then launches `Scenes/RegionalWorldLab3D.tscn`. The standalone V2 scene remains `scripts/run-visual-lab.ps1`. Do not reset or overwrite a locally modified `project.godot` if switching branches is blocked.

In regional mode, right-drag/WASD moves, wheel zooms, Home/End/PageUp/PageDown jumps to remote corners, 1–5 chooses a biome brush, F4 toggles Eco, F11/F12 saves/loads edited biome regions on H:, F8 saves a screenshot, F9 saves frame intervals, and F10 logs chunk/region counts.

The official [V3 architecture and Windows acceptance notes](docs/V3_REGIONAL_WORLD_QA.md) separate proven logical-region functionality from remaining entity simulation, world-save and actual AMD R9 M395X FPS testing.

## Engineering workflow
Each milestone goes into a dedicated branch and PR. Do not merge without CI, build, and Windows visual acceptance. See [roadmap](docs/ROADMAP.md) and [project state](docs/PROJECT_STATE.md).
