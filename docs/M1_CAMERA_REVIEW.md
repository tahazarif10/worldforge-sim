# M1.2 — Camera, pixel snapping and input acceptance

**Issue:** [#5](https://github.com/tahazarif10/worldforge-sim/issues/5) · **Parent:** [#2](https://github.com/tahazarif10/worldforge-sim/issues/2)

## Implementation overview

- `Scripts/Camera/CameraMath.cs`: deterministic, pure .NET camera calculations with no Godot engine dependency.
- `Scripts/Camera/WorldCameraController.cs`: Godot Camera2D adapter.
- `Scripts/GameRoot.cs`: delegates wheel and mouse drag to camera adapter, adds WASD/arrow-key pan.
- `tests/CameraMath.Smoke`: deterministic geometry regression tests on every CI build.
- No source edits to `project.godot`, no changes to `src/WorldSim` simulation logic.

## Camera design invariants

- **Zoom levels:** overview 0.5× and crisp integer 1× / 2× / 3× / 4×. First load 1×. This changes original 0.85× prototype default.
- **Pointer-anchored zoom:** the map feature beneath cursor remains approximately stationary, except when clamped to world boundary (or in overview mode where entire map fits).
- **Drag:** right-drag moves visible terrain in the same direction as the pointer. UI clicks must not paint.
- **Keyboard:** WASD and arrow keys pan at constant visible on-screen speed independent of zoom.
- **Bounds:** cannot pan into endless outside-map space; on viewports bigger than world, automatically center world.
- **Pixel policy:** nearest-neighbor existing terrain texture and camera center snapped at 1 screen pixel. At 0.5× overview, tiny details are inherently reduced in resolution.
- No built-in Camera2D position smoothing: this can cause mouse-to-world inaccuracies while painting. Smooth **interaction** here means stable responsive controls, not interpolated visual easing.

## Acceptance on the Windows/H: host

Do not overwrite local `project.godot` modifications. Note `git status --short` before switching branches.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/m1-camera-polish
. .\scripts\enter-dev.ps1
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' run --project '.\tests\CameraMath.Smoke\CameraMath.Smoke.csproj' -c Release
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-game.ps1
```

Run outside the Godot editor first because [#12](https://github.com/tahazarif10/worldforge-sim/issues/12) tracks a **Godot editor** hang, not a demonstrated game-frame-rate issue. No new installations required.

### Manual checks

- [ ] Scroll over a forest edge far from the map boundary; the same forest tile stays under cursor as zoom changes.
- [ ] Scroll in and out through 0.5×, 1×, 2×, 3×, 4×. No invalid zero scale or fuzzy sampling at integer levels.
- [ ] Right-drag map at 1× and 3×. Camera moves without jumping.
- [ ] Hold WASD and arrows. Diagonal direction normalized; cannot escape world bounds.
- [ ] Select Forest or Ocean and paint exactly where pointing at each tested zoom.
- [ ] Move view to map edge; verify boundary clamping (pointer anchoring has to yield when clamped).
- [ ] Confirm gameplay still works: spawn Person, Meteor, Pause, brush size, New World.
- [ ] At 1280×800 and fullscreen/1920×1080, verify map centered/limited properly.
- [ ] Godot F5 check optionally later; existing Editor-hang investigation tracked separately.
- [ ] Record any actual FPS/worst-frame values; **do not claim measured metrics without evidence**.

## Current verification

GitHub CI and user's Windows runtime are pending on this camera branch. Neither CI success nor runtime success should be claimed until independently observed.

## Dependency and merge safety

M1.2 PR is stacked on `fix/m1-hud-stutter-diagnostics`, which itself depends on `feat/m1-ui-scale` and M0. Do not merge M1.2 directly into `main` before accepted bases. A local modified `project.godot` is a **user change** and must not be overwritten or reset.
