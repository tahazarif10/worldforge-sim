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

The initial geometry is an **interactive style prototype**, not final asset-rich quality. If a trusted CC0 pack is obtained on H:, use `scripts/import-nature-pack.ps1` to stage a GLB tree and rock, then run Godot headless `--editor --import` on H:. The pack itself is not bundled or automatically downloaded. Existing 2D gameplay remains the fallback.

## Engineering workflow
Each milestone goes into a dedicated branch and PR. Do not merge without CI, build, and Windows visual acceptance. See [roadmap](docs/ROADMAP.md) and [project state](docs/PROJECT_STATE.md).
