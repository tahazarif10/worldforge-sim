# M1.1 — HUD visual and functional review

Issue [#4](https://github.com/tahazarif10/worldforge-sim/issues/4), tracker [#2](https://github.com/tahazarif10/worldforge-sim/issues/2).

## Intent
The first M1 implementation replaces the tiny top debug controls with an H:-safe UI built from Godot native Controls. The background, active tool state, two-row layout, larger fonts (16–21) and buttons (≥43 logical pixels high) are authored in `Scripts/UI/GameHud.cs`. Gameplay remains in `Scripts/GameRoot.cs`; `WorldSim` is unchanged.

## User acceptance sequence

1. Check out `feat/m1-ui-scale` on the H:-based clone in a PowerShell process; dot-source `scripts/enter-dev.ps1`.
2. Run `H:\WorldForge-Sim\Tools\dotnet\dotnet.exe build .\WorldForgeSim.csproj -c Debug --disable-build-servers -p:UseSharedCompilation=false`.
3. Launch `scripts/run-editor.ps1`, press F5.
4. Capture native game screenshot at 1280×800. Repeat at 1920×1080 if available; embedded Godot preview may be physically downscaled, so compare native window.
5. Click every tool button; selected tool should visibly highlight, text should not clip.
6. Paint Land/Ocean/Sand/Forest/Mountain; spawn Person and use Meteor. Buttons must not paint terrain behind the UI.
7. Test Size −/+, Pause/1x/2x/4x, New World; test keyboard Space and wheel/right-drag on world.
8. Verify Godot Debugger has no errors; note any overflow, legibility or cursor targeting issues.

## Acceptance status
- [ ] Local Godot C# build pass (not yet run for this branch)
- [ ] HUD appears and scales in native window
- [ ] All brush and time controls tested
- [ ] UI hit-testing verified
- [ ] Screenshot and diagnostics attached
- [ ] Regression-free F5 visual acceptance

Do not merge until actually observed on Windows and documented.
