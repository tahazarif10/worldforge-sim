# M1 — Visual Foundation execution plan

**Tracker:** [#2](https://github.com/tahazarif10/worldforge-sim/issues/2)
**Baseline dependency:** M0 [PR #1](https://github.com/tahazarif10/worldforge-sim/pull/1), `feat/m0-foundation`.
**Canonical local checkout:** `H:\WorldForge-Sim`.
**Outcome:** A maintainable graphics platform with dramatically better legibility and scalable visual layers, preserving gameplay. M1 alone does **not** promise finished premium artwork; richer biome atlases and animated units continue through M2/M3.

## Work breakdown and order

| ID | Issue | Branch | Scope | Depends on |
|---|---|---|---|---|
| M1.0 | [#3](https://github.com/tahazarif10/worldforge-sim/issues/3) | `docs/m1-visual-plan` | art direction, renderer contract, review gates | M0 |
| M1.1 | [#4](https://github.com/tahazarif10/worldforge-sim/issues/4) | `feat/m1-ui-scale` | legible adaptive HUD and grouping | M0; M1.0 |
| M1.2 | [#5](https://github.com/tahazarif10/worldforge-sim/issues/5) | `feat/m1-camera-polish` | pixel-sharp zoom and pan | M1.1 |
| M1.3 | [#6](https://github.com/tahazarif10/worldforge-sim/issues/6) | `feat/m1-render-pipeline` | separate terrain/entity/VFX rendering layers | M1.2 |
| M1.4 | [#7](https://github.com/tahazarif10/worldforge-sim/issues/7) | `feat/m1-terrain-art` | atlas and transition/variation pipeline | M1.3 |
| M1.5 | [#8](https://github.com/tahazarif10/worldforge-sim/issues/8) | `feat/m1-water-polish` | lightweight water animation | M1.4 |
| M1.6 | [#9](https://github.com/tahazarif10/worldforge-sim/issues/9) | `test/m1-visual-acceptance` | reproducible visual QA and benchmarks | M1.1–5 |

**Do not create all future branches prematurely.** Open the implementation branch when its prerequisite is accepted. M1.0 and M1.1 can progress in parallel only if the UI implementation does not depend on unsettled graphics contracts.

## Technical architecture

```text
WorldSim (pure .NET deterministic world state)
       |
       v immutable/owned presentation snapshot or queried data
Godot GameRoot (input, simulation update, scene orchestration)
  |- TerrainRenderer (terrain raster/tiles, dirty regions)
  |- EntityRenderer  (actors, sprite selection, interpolation)
  |- EffectRenderer  (shaders, particle effects)
  |- CameraController (pixel-sensible zoom/pan/target bounds)
  '- UI/HUD           (responsive input-consuming Control layer)
```

Avoid allocating new textures each frame and do not repaint every terrain pixel on an entity-only tick. Keep rendering caches and shader parameters out of the WorldSim simulation. Keep seed-stable decorative choices and make save/load format future-ready.

## Release workflow

1. Check `git status`, selected branch and `H:\WorldForge-Sim` before starting. Dot-source `scripts/enter-dev.ps1` **from H:**.
2. Pull feature branch; write focused change and smoke tests.
3. Run simulation smoke tests, .NET 10 Godot C# build with the H:-based SDK; no invented test results.
4. Run Godot via `scripts/run-editor.ps1`, F5. Manually check target and regressions.
5. Screenshot fixed seed and record environment, screen size and measured metrics.
6. Update `docs/PROJECT_STATE.md` and PR acceptance checklist.
7. Review PR and merge only after tests. If PR #1 isn't merged yet, base M1 PRs on `feat/m0-foundation`; retarget to `main` after M0 merge. Never merge across unverified dependencies.

## Functional regression contract

All M0 terrain/actor/meteor brush actions, brush radius, Pause/1x/2x/4x, New World, pan/zoom and deterministic WorldSim smoke tests must keep working. UI clicks must not paint through to world. Render parity is required even if visuals improve.

## Performance policy

Measure a baseline **on actual hardware** for 0, 100 and 500 citizens in a 160×120 world; note compatibility renderer, 1280×800 and 1920×1080. Capture FPS/frame-time, allocation stutter and memory before setting release thresholds. An aspirational target is 60 FPS at normal speed but not yet validated or a claim of current support.

## Out of scope for M1

No realistic simulation of hunger, kingdoms, army tactics, persistence guarantees or borrowed artwork. Those are later milestones. Prioritize playable, coherent, smoothly rendered art with a maintainable codebase.
