# WorldForge Sim

Original 2D god-sandbox simulation, influenced by the *genre* of WorldBox. This is not WorldBox and does not use its source code, art or assets.

**Status:** M0 bootstrap proposed in `feat/m0-foundation`. The Godot C# build and Windows runtime are **not yet verified**.

## Windows location policy
The project lives in **H:\WorldForge-Sim**, not under AIProjects. Keep intentional downloads, dependencies, logs, build outputs and tool caches on H:. Neither this script nor Godot can guarantee Windows or third-party software never writes OS-managed data on C:.

From PowerShell:
```powershell
Set-Location 'H:\WorldForge-Sim'
. .\scripts\enter-dev.ps1
.\scripts\acceptance.ps1
```
The dot before `enter-dev.ps1` is important: it applies environment variables to the **current PowerShell session**. The script fails if this checkout is not on H:.

Install **Godot 4.7.2 .NET/C# edition** and .NET 10 SDK **under H:** before running the full acceptance. Do not run a conventional default-path installer without checking its paths. For Godot, unzip its portable .NET editor into `H:\WorldForge-Sim\Tools\Godot\` and create an empty file named `_sc_` beside the editor .exe **before launching it**, activating self-contained editor data under `Tools\Godot\editor_data\`. Install the H:-based SDK with `scripts/install-dotnet.ps1`; then dot-source `scripts/enter-dev.ps1` again so the H: SDK takes precedence. The existing C: SDK is left untouched.

If both tools are installed, open `project.godot` with the Godot .NET editor, build C# and run F5.

## First playable prototype
- Procedural seeded 160x120 map with water, sand, grass, forest and mountains
- Paint terrain, spawn people, meteor impact
- Wander simulation, camera pan/zoom, pause/simulation speed
- Framework-independent deterministic core and smoke checks

There are **no** real settlements, combat, food networks, or diplomacy yet.

## Engineering workflow
Each milestone goes into a dedicated branch and PR. Do not merge without CI, build, and Windows visual acceptance. See [roadmap](docs/ROADMAP.md) and [project state](docs/PROJECT_STATE.md).
