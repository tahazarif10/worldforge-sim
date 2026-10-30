---
state_schema: 2
reference_code_commit: 9dc03c8aa608220971b1da3516074f8fd68508a8
last_verified_date: 2026-10-30
completed_through: V3.2-engineering
active_milestone: V3.3.0
active_status: "Planning/docs under review; real H:-Windows baseline measurements and new mountain screenshot acceptance are still pending"
next_gate: "Golden V2 same-seed/screenshots/F9 p95-p99, identical direct vs RegionMirror input/art behavior on AMD R9 M395X"
user_facing_baseline: "Scenes/VisualLab3D.tscn, V2 approved 3D art/camera"
open_blockers: ["#46 V3 visual+camera regression", "#34 production terrain/LOD/art", "#45 regional entity simulation blocked"]
---

# WorldForge Sim — factual project state

**Current as of 2026-10-30.** This file is the factual status ledger, not the entire history and not a promise of future implementation. The prior chronology is preserved verbatim in [PROJECT_HISTORY_2026-10-30.md](PROJECT_HISTORY_2026-10-30.md). The actionable future plan is [ROADMAP.md](ROADMAP.md).

## Product status

WorldForge Sim is an original **3D stylized isometric god-sandbox**. The user explicitly approved the **V2** visual direction, forest/water and camera after previously rejecting 2D pixel-art and then rejecting a standalone region-world V3.0 scene. The accepted *visual baseline* is the live V2 screenshot with world seed **1315511732**, AMD Radeon R9 M395X, Godot 4.7.2 Mono Compatibility/OpenGL and a visible HUD around 60 FPS/16.7ms for a short interval. That screenshot is evidence of a good appearance and a single instantaneous counter, **not 30–60s p95/p99 under pan/zoom stress**.

The latest accepted code snapshot before this docs-only milestone is `main` merge **`9dc03c8aa608220971b1da3516074f8fd68508a8`** (PR #51), with prior mountain correction merge PR #50. User has not yet supplied a new F8 screenshot confirming the corrected mountain summit or the direct vs mirrored 3D frame-time comparison. Do **not** mislabel new mountain art as user accepted merely because merged/Godot CI passed.

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

## Next action: V3.3.0 — golden host acceptance and camera/art parity

**Live tracker:** [#52](https://github.com/tahazarif10/worldforge-sim/issues/52); implementation/evidence slices [#53](https://github.com/tahazarif10/worldforge-sim/issues/53), [#54](https://github.com/tahazarif10/worldforge-sim/issues/54).

**Do not start V3.4 or V3.5 data expansion as the next user-visible action.** First:

1. On H:-Windows, safely update main without touching `project.godot`.
2. Run `.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732`, collect F8 summit/coast screenshot and F9 CSV after controlled pan/zoom.
3. Run `.\scripts\verify-v2-region-parity.ps1` in same camera/viewport/seed where supported; extend runner with optional seed and pose if needed. Direct vs mirror must look and feel identical.
4. Implement planned `scripts/run-v3-3-acceptance.ps1` wrapper in a **new implementation PR** (file does **not** exist yet), producing H:-only session result + hashes + matched scenarios. No scene changes required.
5. Record quantitative baseline and explicit user art/camera verdict; then authorize V3.4 no-hole background/LOD architecture.

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

- [Milestone roadmap](ROADMAP.md)
- [Detailed engineering workflow](ENGINEERING_WORKFLOW.md)
- [Source/data/renderer architecture](V3_ARCHITECTURE.md)
- [Godot and art research](V3_TECHNICAL_RESEARCH_2026.md)
- [Physical acceptance scenarios and evidence template](V3_ACCEPTANCE_MATRIX.md)
- [Architecture decisions](decisions/README.md)
- [Original project development chronology](PROJECT_HISTORY_2026-10-30.md)

This document is updated at every milestone acceptance/rollback. Do not invent acceptance and do not silently replace the known rejected V3 user feedback with green CI.
