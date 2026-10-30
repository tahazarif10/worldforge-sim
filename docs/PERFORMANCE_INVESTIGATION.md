# M1.1 — Stutter / freeze investigation

**Issue:** [#12](https://github.com/tahazarif10/worldforge-sim/issues/12) (release blocker)  
**Parent HUD:** [#4](https://github.com/tahazarif10/worldforge-sim/issues/4) / [PR #11](https://github.com/tahazarif10/worldforge-sim/pull/11)  
**Hardware to reproduce:** Windows 11, AMD Radeon R9 M395X, Godot 4.7.2 .NET Compatibility, .NET 10.0.403 under `H:\WorldForge-Sim\Tools\dotnet`.

## Evidence at intake

User confirmed new HUD is readable and works but reported *very severe hangs*. No time-domain measurements, FPS logs, debugger profiler capture or exact trigger have been supplied. **Root cause unknown.**

Code hypotheses:
1. Prior HUD `UpdateState` was called every rendered frame and blindly assigned three `Label.Text` values, potentially provoking layout/invalidations.
2. Pointer motion while painting called `UpdateTerrain` for every input event, including a full 160×120 upload each time.
3. Citizen colors were allocated during draw. Small but unnecessary.
4. Editor debugger or Compatibility renderer/driver may be responsible for stalls, independent of gameplay logic.

## Minimal mitigation on diagnostic branch

- Throttle HUD status update to 4 Hz with instant refresh for button/keyboard actions and write labels only on actual text changes.
- Coalesce terrain uploads to one update per process frame even when brush events arrive faster.
- Reuse static citizen colors.
- Add a *sampled* readout of FPS and **worst frame duration measured using `_Process(delta)`**. It is not an isolated GPU-timing metric and won't measure freezes that never recover.
- Provide an optional process sampler `scripts/capture-host-performance.ps1` to `artifacts\performance\` **on H:**.
- Provide `scripts/run-game.ps1` to launch the game without Godot editor debugging for A/B editor-overhead diagnosis.

## Windows acceptance procedure (no installation)

1. Record baseline if possible: on existing `feat/m1-ui-scale`, Godot F5, note whether lag is **editor**, **game window**, or **both**; inspect `Debugger > Profiler` from within Godot. The profiler itself can affect performance, so first measure without it.
2. Close the running game and editor. On H:-based PowerShell: `git fetch origin`, `git switch fix/m1-hud-stutter-diagnostics`, `. .\scripts\enter-dev.ps1`.
3. Compile with `& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build .\WorldForgeSim.csproj -c Debug --disable-build-servers -p:UseSharedCompilation=false`.
4. Run `.\scripts\run-editor.ps1` then F5. Note FPS and worst-frame ms shown in HUD for 30 seconds idle at speed 1x, 30 seconds paused, 20 seconds terrain painting, and while spawning 100 people. Confirm all M0 brush controls continue working.
5. If hangs persist, close the editor then run `.\scripts\run-game.ps1`; compare whether the standalone game also hangs. No extra downloads.
6. For optional CPU/RAM capture, launch a **second** H:-based PowerShell and run `.\scripts\capture-host-performance.ps1 -Seconds 40 -IntervalSeconds 2`, while performing the hang reproduction. Send `artifacts\performance\godot-host-*.csv` and a HUD screenshot. All logs remain on H:.
7. If there is a spike, in Godot open `Debugger > Profiler`, start profiling, reproduce it, and screenshot frame-time graph. Profiling adds overhead and should not be mistaken for baseline performance.
8. Report native game FPS/worst-frame values, time between freezes, whether the editor also stops responding, CPU/RAM sampling and Godot errors; compare same conditions to previous branch.

## Success criteria

- No hanging in a repeatable 2-minute representative test on user hardware.
- No silent regressions in terrain painting, person spawning, meteor, new world, keyboard shortcuts, speed controls, pan or zoom.
- Worst frame and FPS evidence before/after with actual screenshots, not guessed figures.
- GitHub Actions C# compile and simulation smoke tests green; Windows build + F5 checked.
- Root cause documented or narrowed to a specific engine/driver/editor effect; further targeted fixes as needed.

**Data policy:** Project H: root, logs under `H:\WorldForge-Sim\artifacts\performance`; no intentional C: storage. Windows-managed background writes remain outside absolute control.
