# WorldForge Sim

**Original stylized 3D living-world god sandbox** — island terrain, coastal water, forests, mountains, authored CC0 nature, human agents and god powers, with an orthographic/isometric camera. Inspired by the sandbox-simulation **genre**, not WorldBox proprietary art/source/UI.

**Development state — 2026-10-30:** the user-approved playable experience is the **V2.9+ 3D VisualLab** with subsequent merged regional-cache and summit-rock improvements. The standalone V3 regional demo is **rejected** for degraded visuals and controls, and is **not** the playable successor. We are planning **V3.3 physical baseline acceptance**, then no-hole streaming and authoritative region simulation. A CI-green merge alone does not certify Windows GPU frame time or visual approval.

## Start the approved 3D game (Windows, H: drive)

This project was configured for **Godot 4.7.2 Mono/.NET 10**, AMD Radeon R9 M395X, **Compatibility/OpenGL**. Tools, .NET packages, game assets, builds, cache and screenshots intentionally stay under **H:\WorldForge-Sim**. Third-party/Windows system activity on C: cannot be globally prevented.

From Windows PowerShell (safe Git update **only if local modifications do not block it**):

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin
git switch main
git pull --ff-only origin main

# Start the same mountain world as the user screenshot:
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732
```

**Do not `git reset --hard`, `git clean`, force a branch switch or overwrite locally modified `project.godot`** if Git stops. The default old `Scenes/Main.tscn` remains in the repo but the **preferred** approved 3D scene is `Scenes/VisualLab3D.tscn`, launched without changing project.godot.

Use **F8** screenshot (H:/artifacts/visual-review), **F9** frame-time CSV (H:/artifacts/performance), **F10** chunk counters, **F4** Eco/Balanced, **F6** smooth/legacy slopes. `-WorldSeed 1315511732` is optional and recreates the exact screenshot seed; omit for the original launch seed.

### Engineering terrain-source parity (opt-in; same scene)

```powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-region-parity.ps1
```

This **still runs the approved V2 renderer and camera** but accesses the existing 120×88 WorldState tiles through a bounded 64×64 `RegionStore` cache. It is not a playable 8192×8192 world. Do not run the user-rejected `RegionalWorldLab3D.tscn` for gameplay. Its engineering-only scripts require `-ExperimentalV3`.

## Current code capabilities and limitations

| Implemented | Not yet implemented / not yet physically verified |
| --- | --- |
| 3D biome terrain, Kenney 10 CC0 GLBs, coast/waves/foam, shared-corner mountain elevation and exact pointer picking | Verified premium Quaternius hero pack and high-end effects without host AMD cost |
| F4 Eco, 16×16 visual chunks, 64×64 sparse region height edits | No-hole streamed far-LOD coverage, pure off-main-thread mesh build pipeline |
| WorldState people/meteor/brush/tick controls on V2 map | Persistent authoritative giant regions, 8192² simulated life |
| Signed 64×64 biome and elevation region stores, checksummed per-region file codecs | Full crash-atomic multi-region manifest/backup/world saves |
| V2 direct/RegionMirror data parity and mountain rock cap reduction, merged + CI passed | Final user Windows screenshot review of corrected summit; direct/mirror p95/p99 at matching camera |

## Source-of-truth documents — same milestone discipline as Local AI Desktop Copilot

| Document | Why to read it |
| --- | --- |
| [Project State](docs/PROJECT_STATE.md) | Factual implemented/verified status, exact accepted SHA, blockers and next gate |
| [Master Roadmap](docs/ROADMAP.md) | Detailed V3.3 through V5 dependency-ordered slices, scope, goals and exit criteria |
| [Engineering Workflow](docs/ENGINEERING_WORKFLOW.md) | Branch/issue/PR, CI, H:-Windows acceptance, evidence, rollback process |
| [V3 Architecture](docs/V3_ARCHITECTURE.md) | Data/scene ownership, region streaming, worker safety, LOD/coast, save schemas |
| [Technical Research](docs/V3_TECHNICAL_RESEARCH_2026.md) | Godot official sources, asset licensing, alternatives, rejected approaches |
| [Acceptance Matrix](docs/V3_ACCEPTANCE_MATRIX.md) | Exact F8/F9/camera/painting scenarios, reproducible metadata and pass/fail |
| [ADRs](docs/decisions/README.md) | Persistent architecture choices and why V3 failed |
| [Historical Project Log](docs/PROJECT_HISTORY_2026-10-30.md) | Original M0/M1/V2 chronological development notes kept intact |
| [Mountain correction](docs/V3_2_MOUNTAIN_SUMMIT_QA.md) | Summit rock art, numeric regression evidence, reference seed 1315511732 |

Review [AGENTS.md](AGENTS.md) before modifying code. The project's [critical V3 visual/input regression issue #46](https://github.com/tahazarif10/worldforge-sim/issues/46) remains open; a Linux Godot boot is not user Windows acceptance.

## Architecture summary

```text
WorldSim (pure C#, authoritative small-world gameplay today)
          |
          +--> DirectWorldTerrainSource [default]
          +--> RegionMirrorTerrainSource [experimental, source parity]
                        |
                        v
VisualTerrain3D + approved stylized ocean/coast + Kenney MultiMesh
                        |
                 StrategyCameraRig
                        |
              VisualLab3D / GameHud
```

Future bounded regional world: make 64×64 data regions authoritative behind this **same** art/camera path, introduce far-LOD fallback with no blank visible cells, then persistent region/entity simulation and crash-safe saves. No destructive replacement of the V2 visual scene.

## Historical milestones

- M0/M1 established the original Godot project and 2D gameplay; the original 2D/pixel-art art direction was later rejected.
- V2 introduced true stylized 3D, mouse-anchored orthographic camera, animated ocean and real CC0 models.
- V3.0's alternate region demo proved only logical coordinates and some storage; **the user rejected appearance AND movement**. [Postmortem](docs/V3_REJECTION_POSTMORTEM.md).
- [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47) restored approved V2 as the safe default; [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) added opt-in exact data-source parity; [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50) removed dense rock caps from summits; [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) added exact screenshot seed replay.
- **Next:** measured V3.3 baseline/QA and only then streaming/LOD development, per the [roadmap](docs/ROADMAP.md).

## Rights, CI and product boundaries

All art/code must be original or use verified licensing. Bundled Kenney Nature Kit models are CC0; track hashes and source [here](Assets/Art/KenneyNature/README.md). Do not copy WorldBox assets or proprietary game logic.

GitHub core smoke and Godot Mono headless scene tests are **engineering gates**, not performance measurements. Real Windows **AMD R9 M395X** FPS, p95/p99, camera responsiveness and user visual acceptance are distinct required release gates. No new feature is promoted when the screen looks worse, the camera lags or visible land disappears.
