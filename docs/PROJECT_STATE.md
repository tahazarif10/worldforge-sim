---
state_schema: 3
reference_code_commit: 9dc03c8aa608220971b1da3516074f8fd68508a8
last_verified_date: 2026-11-01
audited_repository_commit: bf70b825528dda2f2bf8fee16e9f7245ec016c15
baseline_core_ci_commit: bf70b825528dda2f2bf8fee16e9f7245ec016c15
baseline_core_ci_run: 41109317518
baseline_engine_ci_commit: 41eaaba01e9a6678583c376f7f841751ae4506e6
baseline_engine_ci_run: 41105302717
windows_verified_commit: null
user_accepted_visual_commit: null
completed_through: V3.2-engineering
active_milestone: V3.3.1
active_status: "Monotonic capture, fixed scenario fixture and H: runner implemented; actual Windows and user acceptance pending"
next_gate: "Run H: acceptance runner on reference seed; review valid direct/mirror captures and complete #53 manual visual/input gates"
user_facing_baseline: "Scenes/VisualLab3D.tscn, V2 approved 3D art/camera"
open_blockers: ["#46 V3 visual+camera regression", "#34 production terrain/LOD/art", "#45 regional entity simulation blocked"]
---

# WorldForge Sim — factual project state

**Source audit updated 2026-11-01.** This file is the factual status ledger, not the entire history and not a promise of future implementation. The prior chronology is preserved verbatim in [PROJECT_HISTORY_2026-10-30.md](PROJECT_HISTORY_2026-10-30.md). The actionable future plan is [ROADMAP.md](ROADMAP.md).

## Product status

WorldForge Sim is an original **3D stylized isometric god-sandbox**. The user explicitly approved the **V2** visual direction, forest/water and camera after previously rejecting 2D pixel-art and then rejecting a standalone region-world V3.0 scene. The accepted *visual baseline* is the live V2 screenshot with world seed **1315511732**, AMD Radeon R9 M395X, Godot 4.7.2 Mono Compatibility/OpenGL and a visible HUD around 60 FPS/16.7ms for a short interval. That screenshot is evidence of a good appearance and a single instantaneous counter, **not 30–60s p95/p99 under pan/zoom stress**.

The last runtime-code merge before this audit was **`9dc03c8aa608220971b1da3516074f8fd68508a8`** (PR #51), with prior mountain correction PR #50. Audited main was **`bf70b825528dda2f2bf8fee16e9f7245ec016c15`** after documentation PR #58. Neither reference establishes an exact user-accepted visual commit; that field remains unknown. User has not yet supplied a new F8 screenshot confirming the corrected mountain summit or the direct vs mirrored 3D frame-time comparison. Do **not** mislabel new mountain art as user accepted merely because merged/Godot CI passed.

### Code scope in main

| Capability | Actual status | Evidence |
| --- | --- | --- |
| 3D terrain/forest/coast/camera/brush/citizens | Implemented, user-approved general V2 visuals; separate editor-hang history | `VisualLab3D`, `VisualTerrain3D`, `StrategyCameraRig`; original screenshot |
| CPU DDA screen-to-triangle picking, shared-corner slopes | Deterministic source CI-green, Windows high-stress not measured | [PR #38](https://github.com/tahazarif10/worldforge-sim/pull/38), [PR #40](https://github.com/tahazarif10/worldforge-sim/pull/40) |
| 64x64 sparse height-offset edits, local F7/F11/F12 | Merged/CI green, actual physical save cycle pending | [PR #42](https://github.com/tahazarif10/worldforge-sim/pull/42) |
| 8192² logical RegionStore prototype | **User-rejected as a playable scene**, logical/codec CI green only | [PR #44](https://github.com/tahazarif10/worldforge-sim/pull/44), [issue #46](https://github.com/tahazarif10/worldforge-sim/issues/46) |
| Safer default launcher for rejected V3 | Merged, CI success | [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47) |
| V2 same scene with optional 64×64 RegionMirror | Merged, CI proven exact biome/shore/height/citizen RNG on 120×88; **does not yet own a large world** | [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) |
| Mountain cap rock removal | Merged, deterministic count 66→9 at seed 1315511732; user Windows screenshot verdict pending | [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50) |
| Exact screenshot seed replay via `-WorldSeed 1315511732` | Merged; Godot headless CI proved selected seed and imports 10 GLBs | [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) |
| Persistent independent world-wide authoritative regions | **Not implemented**; RegionMirror still samples `WorldState` | [Architecture](V3_ARCHITECTURE.md) |
| Completely seamless large map / far LOD / pure background mesh jobs | **Not implemented**; old V3 scene had visible holes | [#46](https://github.com/tahazarif10/worldforge-sim/issues/46) |
| 8192² active citizens / villages / kingdoms | **Not implemented**; user-facing large-world simulation blocked | [#45](https://github.com/tahazarif10/worldforge-sim/issues/45) |
| Crash-atomic multi-region save manifest | **Not implemented**; individual checksummed per-region save only | [V3 architecture](V3_ARCHITECTURE.md) |
| Quaternius hero art | **Not imported**; Kenney 10 GLBs are currently bundled | [Bundled asset provenance](../Assets/Art/KenneyNature/README.md) |

## Evidence register (split by proof level)

| Date | PR / head | Verified automatically | Verified on Windows | Still outstanding |
| --- | --- | --- | --- | --- |
| 2026-10-30 | PR #40 | 10,096 shared boundaries and 2,975 triangle pick checks reported, Godot Mono boot | V2 visuals described as better in user feedback | Screenshot/quantified p95/p99 of that exact HEAD |
| 2026-10-30 | PR #42 | 64×64 elevation files, recovery, corrupt file refusal, exact height ray tests, Godot boot | Not independently recorded for elevation save/load | H:-Windows F7/F11/F12 end-to-end |
| 2026-10-30 | PR #44 | 8192² logical region traversal, LRU, edited biome saves, Godot headless startup | User explicitly rejected both appearance and camera feel | Do NOT promote this scene |
| 2026-10-30 | PR #47 | Safe V2 default experimental V3 launcher guard | N/A to art quality | Shared art/camera regional-world design |
| 2026-10-30 | PR #49 | Direct vs RegionMirror every V2 tile, shoreline byte parity, heights, citizen RNG; official Godot both boots | Mirror physical UX/FPS comparison PENDING | Baseline V3.3.0 |
| 2026-10-30 | PR #50 | Mountain summit law and exact V2 map seed: 66 old boulders -> **9** small accents, **36** top tiles no extra rock; all old core+Godot CI green | Pre-fix screenshot was approved except cap; post-fix screenshot pending | F8/F9 same-seed verification |
| 2026-10-30 | PR #51 | Same V2 scene boot at exact reference seed 1315511732 in official engine CI; all core tests green | Not explicitly run on user host after merge | One-command host screenshot replay |

The above numeric PR50 counts are code/test counts, **not a pixel-level proof of improved art**. The existing V2 HUD screenshot had 60 FPS and 16.7 ms worst in one displayed 1-second interval; this is not an AMD frame-time distribution nor a guaranteed sustained 60 FPS.

### Audit and repository engineering update — 2026-11-01

- Existing core baseline: [run 41109317518](https://github.com/tahazarif10/worldforge-sim/actions/runs/41109317518), head `bf70b825528dda2f2bf8fee16e9f7245ec016c15`, success.
- Existing engine baseline: [run 41105302717](https://github.com/tahazarif10/worldforge-sim/actions/runs/41105302717), head `41eaaba01e9a6678583c376f7f841751ae4506e6`, success. This is an earlier PR #51 head, not a fresh engine run on audited main.
- This revision adds the documentation index, [Persian roadmap](ROADMAP_FA.md), [source audit](REPOSITORY_AUDIT_2026-11-01.md), [ADR 0007](decisions/0007-measurement-evidence-and-scenario-identity.md), contributor templates, broader engine triggers and reference-seed direct/mirror regression coverage. Its own CI evidence is recorded on its PR; do not substitute the baseline runs above.
- At that audit, measurement/streaming/save/entity mechanisms were planned and F9 used rolling engine delta. The V3.3.1 implementation below supersedes the measurement status; streaming, transactional world saves and regional entities remain planned.
- Windows candidate SHA, corrected-summit approval and sustained GPU/input measurements are still **PENDING**. No automated run fills those fields.

The detailed audit also identifies main-thread edit/upload work, three-cell foam halo requirements, unbounded retained edit metadata, direct WorldState terrain reads, and simulation/RNG/save contracts required before scaling.

### V3.3.1 capture implementation

| Deliverable | Status |
| --- | --- |
| Monotonic callback capture, engine delta separate, reset/stop/overflow and timestamps | Implemented; meaningful correctness tests added |
| Fixed seed/camera/quality/phase and idle/pan/zoom/paint traces | Implemented, opt-in; simulation paused |
| H: PowerShell runner, metadata/raw frames/pose traces/images/hashes | Implemented; shared validator fails incomplete or mismatched captures |
| F9 monotonic rolling diagnostic and mirror -WorldSeed | Implemented |
| Real target-host run, image/control verdict and live simulation/input scenarios | PENDING |

## Current hardware and local rules

- User PC: Windows, Godot **4.7.2 Mono**, **.NET 10**, GPU **AMD Radeon R9 M395X**, Compatibility/OpenGL renderer.
- Canonical project: `H:\WorldForge-Sim`. All intentional project tools, builds, downloads, caches, logs and F8/F9 outputs on H:. Some Windows-managed writes may still exist on C:; do not promise otherwise.
- Exact user-local file `project.godot` has reported uncommitted modifications previously. **Never** overwrite/reset/clean it. No forced Git branch switch.
- Default old 2D scene `Scenes/Main.tscn` remains. Playable preferred user experience is opt-in **`Scenes/VisualLab3D.tscn`**, launched via `.\scripts\verify-v2-art.ps1`.
- Godot **Editor** hang history is not the same as the standalone game performance. Do not conflate them.

## Blockers and decisions

1. **#46 OPEN:** failed independent V3 visual and control parity. Only shared V2 renderer/camera path is allowed for new world streaming. A source-only region proof does not close this.
2. **#34 OPEN:** production terrain LOD, larger regional elevation authority, art asset/picking quality beyond current sparse overlays.
3. **#45 OPEN / BLOCKED:** regional population until streaming and region terrain source-of-truth accepted.
4. **Physical gate missing:** matched direct/mirror screenshot and drag/wheel input performance, p50/p95/p99 on R9 M395X. User approvals are a distinct column.
5. **Actual huge-world save/restore** missing; per-region checksummed files are not yet a durable all-regions transaction.

## Next action: V3.3.1 implementation → V3.3.0 host acceptance

**Live tracker:** [#52](https://github.com/tahazarif10/worldforge-sim/issues/52); implementation/evidence slices [#53](https://github.com/tahazarif10/worldforge-sim/issues/53), [#54](https://github.com/tahazarif10/worldforge-sim/issues/54).

**V3.3.1 measurement foundation is implemented.** The [runner guide](V3_3_ACCEPTANCE_RUNNER.md) specifies the fixed camera/seed/phase fixture, four scripted scenarios, monotonic timestamps, bounded capture, raw-statistics validation, H: output layout and limitations. The mirror verifier now forwards `-WorldSeed`.

Next: run `.\scripts\run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1` on the target host, inspect the bundle, then collect the full repeated matrix and the remaining manual #53 scenarios. The runner deliberately pauses simulation and invokes camera/brush methods; physical input latency, live simulation, art approval and save/load remain separate.

The implementation's exact-head core, PowerShell and official-engine CI results are recorded in its PR. No Linux/headless or hosted Windows script test establishes R9 M395X GPU acceptance. #52/#53/#54 stay open for remaining evidence; V3.4 promotion remains blocked.

### Existing commands

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin
# Only if working-tree state permits:
git switch main
git pull --ff-only origin main
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732
```

Do not run `git reset --hard`, `git clean`, or auto-delete `.godot`/assets to "repair" import. `F8` screenshot at `artifacts\visual-review`, `F9` frame time at `artifacts\performance`, `F10` chunk counters. If any check is missing, report **PENDING**.

## Documentation pointers

- [Documentation guide](README.md)
- [نقشهٔ راه فارسی](ROADMAP_FA.md)
- [Source audit](REPOSITORY_AUDIT_2026-11-01.md)
- [Milestone roadmap](ROADMAP.md)
- [Detailed engineering workflow](ENGINEERING_WORKFLOW.md)
- [Source/data/renderer architecture](V3_ARCHITECTURE.md)
- [Godot and art research](V3_TECHNICAL_RESEARCH_2026.md)
- [Physical acceptance scenarios and evidence template](V3_ACCEPTANCE_MATRIX.md)
- [Architecture decisions](decisions/README.md)
- [Original project development chronology](PROJECT_HISTORY_2026-10-30.md)

This document is updated at every milestone acceptance/rollback. Do not invent acceptance and do not silently replace the known rejected V3 user feedback with green CI.
