# WorldForge Sim

**Original stylized 3D living-world god sandbox** — island terrain, coastal water, forests, mountains, authored CC0 nature, human agents and god powers, with an orthographic/isometric camera. Inspired by the sandbox-simulation **genre**, not WorldBox proprietary art/source/UI.

**Development state — reviewed 2026-11-04:** the user-approved playable experience is the **V2.9+ 3D VisualLab** with subsequent merged regional-cache and summit-rock improvements. **V3 is withdrawn** as a playable release: the standalone regional demo was rejected for degraded visuals and controls, and is **not** the playable successor. The **V3.3 measurement/parity runner is implemented**; actual Windows baseline acceptance remains pending. The **V3.4.3 workers and V3.4.4 staged native publication are separately opt-in**, extending the V3.4.2 viewport/fallback path. The preceding interactive candidate received positive qualitative feedback; exact-host worker visual/performance acceptance, distance LOD and authoritative region simulation remain open. A complete small-world gameplay loop precedes optional maximum-map expansion. A CI-green merge alone does not certify Windows GPU frame time or visual approval.

## Start the approved 3D game (Windows, H: drive)

This project was configured for **Godot 4.7.2 Mono/.NET 10**, AMD Radeon R9 M395X, **Compatibility/OpenGL**. Tools, .NET packages, game assets, builds, cache and screenshots intentionally stay under **H:\WorldForge-Sim**. Third-party/Windows system activity on C: cannot be globally prevented.

The portable launch environment ([#77](https://github.com/tahazarif10/worldforge-sim/issues/77)) validates configured destinations before creating them and refuses existing junctions, symbolic links or wrong file/directory kinds along those paths. Godot launchers require a real `_sc_` file beside the portable editor and validate its `editor_data` path. The environment applies only to the current process and its children; it does not change Windows user/machine environment or registry settings.

| Controlled data | Destination under the checkout |
| --- | --- |
| Temporary files, NuGet scratch, .NET bundle extraction | `Temp`, `Temp\NuGetScratch`, `Temp\DotNetBundle` |
| NuGet packages / HTTP cache / plugin cache | `Cache\NuGet`, `Cache\NuGetHttp`, `Cache\NuGetPlugins` |
| .NET first-run state and MSBuild user extensions | `Cache\DotNetHome`, `Cache\MSBuild` |
| Tool roaming/local application data, including NuGet user config | `Cache\AppData\Roaming`, `Cache\AppData\Local` |
| SDK, editor and portable editor data | `Tools\dotnet`, `Tools\Godot` |
| Game/editor logs, captures and reports | `artifacts` |

Supported .NET switches disable first-run ASP.NET certificate creation, global-tool PATH modification, telemetry and background workload update notification. The obsolete `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` flag is no longer relied on. [Microsoft environment reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables) and [NuGet cache controls](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders) describe these controls. NuGet's .NET implementation honors the scoped APPDATA/LOCALAPPDATA values for its user settings and migration data; Godot also uses these environment paths in Windows, alongside its [self-contained mode](https://docs.godotengine.org/en/4.7/tutorials/io/data_paths.html#self-contained-mode).

`RUN-TEST.cmd` additionally disables the PowerShell module-analysis file cache **before** starting a fresh shell. Dot-sourcing `enter-dev.ps1` in an already-open shell cannot change that shell's startup cache selection; see [Windows PowerShell 5.1](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_windows_powershell_5.1?view=powershell-5.1#module-analysis-cache). The copied diagnostic report includes resolved portable paths, with full bindings in `portable-environment.json`. These are configuration checks, not a machine-wide filesystem trace or a guarantee about OS/driver-managed writes.

From Windows PowerShell (safe Git update **only if local modifications do not block it**):

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin main
if ($LASTEXITCODE -ne 0) { throw 'Download failed. Stop and inspect the Git output.' }
git merge --ff-only --no-autostash FETCH_HEAD
if ($LASTEXITCODE -ne 0) { throw 'Update stopped. Preserve local changes and inspect the Git output.' }

# Play the interactive game with its complete start/pause menu:
.\scripts\run-visual-lab.ps1 -WorldSeed 1315511732
```

**Manual launcher menu (November 2026):** The `run-visual-lab.ps1` game opens a new **empty world, with zero citizens**, behind a start menu. Choose **START NEW WORLD**, then **PERSON** and click suitable land to place the initial people. **CONTINUE SAVED WORLD** is the only way that menu loads older citizens; the prior auto-resume behavior is disabled specifically for this manual menu. Esc/MENU pauses and opens Save, Load (with confirmation), New Empty World (with confirmation), Settings (graphics, speed, fullscreen), How to Play and Quit. Ctrl+S/Ctrl+L remain. When starting afresh on the same seed as a previous save, automatic saving cannot replace that save without an explicit confirmation. Legacy/direct Godot scene and automated acceptance stay unchanged; `-FreshWorld` still disables save/load. The user will check visuals and behavior on their physical Windows machine; this is not a passed host acceptance claim. See [ADR 0017](docs/decisions/0017-explicit-empty-new-game-and-game-menu.md).

**Do not `git reset --hard`, `git clean`, force a branch switch or overwrite locally modified `project.godot`** if Git stops. The default old `Scenes/Main.tscn` remains in the repo but the **preferred** approved 3D scene is `Scenes/VisualLab3D.tscn`, launched without changing project.godot.

Use **F8** screenshot (H:/artifacts/visual-review), **F9** frame-time CSV (H:/artifacts/performance), **F10** chunk counters, **F4** Eco/Balanced, **F6** smooth/legacy slopes. `-WorldSeed 1315511732` is optional and recreates the exact screenshot seed; omit for the original launch seed.

### Engineering terrain-source parity (opt-in; same scene)

```powershell
Set-Location 'H:\WorldForge-Sim'
.\scripts\verify-v2-region-parity.ps1
```

The mirror wrapper now accepts `-WorldSeed 1315511732`. For matched camera, phase, timing windows and a validated direct/mirror bundle, use the [V3.3 acceptance runner](docs/V3_3_ACCEPTANCE_RUNNER.md). F9 now records monotonic intervals with a diagnostic JSON sidecar; its rolling mixed session is still not an isolated acceptance scenario.

This **still runs the approved V2 renderer and camera** but accesses the existing 120×88 WorldState tiles through a bounded 64×64 `RegionStore` cache. It is not a playable 8192×8192 world. Do not run the user-rejected `RegionalWorldLab3D.tscn` for gameplay. Its engineering-only scripts require `-ExperimentalV3`.

### Experimental viewport streaming (same interactive scene)

The [V3.4 guide](docs/V3_4_VIEWPORT_STREAMING.md) documents `verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming`. This opens normal interactive play with finite camera/height bounds, bounded demand and surface-only land/ocean coverage while detail is queued. F10 separates missing detail from missing combined coverage. Omit the switch for the ordinary planner.

For the new [V3.4.3 worker path](docs/V3_4_MESH_WORKERS.md), run `verify-v2-art.ps1 -WorldSeed 1315511732 -MeshWorkers`. It includes surface fallback, at most two numeric workers/four retained results, and main-thread count/payload plus soft time admission. F10/F9 include worker diagnostics. `-ViewportStreaming` alone preserves the previous synchronous streaming path. Native upload duration, final GPU pixels and target-host performance still require physical review.

The worker path also uses the [single-fan picker](docs/PROJECT_STATE.md#single-fan-picking-follow-up--78): one terrain fan supplies all five vertex heights for each picked cell, preserving the scalar path as the parity reference and ordinary-play default. Deterministic and native camera tests check equal surfaces, selections and zoom/drag behavior. Reduced source-read counts do not by themselves establish an R9 M395X frame-time improvement.

### One-click game test and copied report

Double-click [RUN-TEST.cmd](RUN-TEST.cmd) in the existing H: checkout after a safe update. It builds/imports, runs correctness checks, then automatically captures idle/pan/zoom/paint on both backends with mesh workers enabled. Eight windows run for 3 seconds of warm-up + 8 seconds recording each, plus startup/settling; let them close automatically.

The report is copied for pasting in the active project conversation, and a local ZIP is prepared under artifacts/acceptance/self-test. Failure/cancellation preserves partial evidence. Nothing uploads automatically. This is a short diagnostic; physical performance acceptance remains pending. [Runner and coast-patch guide](docs/V3_4_COAST_PATCHES_AND_SELF_TEST.md).

**Latest target-host result, 2026-11-04:** the received report at `a0fd5131fab345792f6b957b5c98c316d3d52a52` passed all eight short worker captures on R9 M395X (3,750 samples). [Measured rows, provenance and pending gates](docs/V3_ACCEPTANCE_MATRIX.md#i-received-staged-publication-host-report--2026-11-04). Paint p95 was 18.538 ms direct / 21.647 ms mirror; mirror zoom p95 remained 31.858 ms. Its slow paint rows place substantial work in visibility outside the pump, with zero native publication in those particular rows. The zoom interval remainder remains unclassified. These short summaries do not establish repeated performance benefit or full visual/input acceptance.

[V3.4.4 slice 2](docs/V3_4_STAGED_PUBLICATION.md) is **engineering complete and merged in [PR #75](https://github.com/tahazarif10/worldforge-sim/pull/75)**. It separates terrain and foliage admission while holding one hidden replacement until matching handoff. The requested schema 3 host report has now arrived. Its portable-tool and single-fan picker follow-ups are also **merged in [PR #79](https://github.com/tahazarif10/worldforge-sim/pull/79) and [PR #80](https://github.com/tahazarif10/worldforge-sim/pull/80)** after exact-source core, native Windows PowerShell, actual Windows Godot preflight and official engine checks passed. [The state ledger](docs/PROJECT_STATE.md) records source/merge identities and run links. Safely update main to include `fff01bc5`, then run RUN-TEST.cmd and let all eight windows close automatically. The changed candidate still needs its R9 M395X report and interactive camera/brush review; host speed improvement and full physical acceptance remain open.

Slice 1 proves bounded coast-mask patches against V2; resident coast rendering and distance LOD remain later slices.

## Current code capabilities and limitations

| Implemented | Not yet implemented / not yet physically verified |
| --- | --- |
| 3D biome terrain, Kenney 10 CC0 GLBs, coast/waves/foam, shared-corner mountain elevation and exact pointer picking | Verified premium Quaternius hero pack and high-end effects without host AMD cost |
| F4 Eco, 16×16 visual chunks, sparse region height edits; opt-in surface fallback and immutable workers with bounded publication | Physically verified no-hole far LOD, worker GPU/frame-time benefit and native-memory limits |
| WorldState people/meteor/brush/tick controls on V2 map | Persistent authoritative giant regions, 8192² simulated life |
| Signed 64×64 biome and elevation region stores, checksummed per-region file codecs | Full crash-atomic multi-region manifest/backup/world saves |
| V2 direct/RegionMirror data parity and mountain rock cap reduction, merged + CI passed | Final user Windows screenshot review of corrected summit; direct/mirror p95/p99 at matching camera |

## Documentation and execution plan

| Document | Why to read it |
| --- | --- |
| [V3.4.3 worker guide](docs/V3_4_MESH_WORKERS.md) | Exact switch, ownership, budgets, counters, tests and rollback |
| [V3.3 measurement runner](docs/V3_3_ACCEPTANCE_RUNNER.md) | One-command H: captures, exact fixture, output schema and remaining manual gates |
| [Documentation guide](docs/README.md) | Current vs historical documents and where each kind of evidence belongs |
| [نقشهٔ راه فارسی](docs/ROADMAP_FA.md) | توضیح مراحل، وابستگی‌ها، معیار پایان و اقدام بعدی |
| [Repository audit](docs/REPOSITORY_AUDIT_2026-11-01.md) | Source findings, risks, current evidence and concrete follow-up |
| [Project State](docs/PROJECT_STATE.md) | Implemented/verified state; source, CI and physical acceptance identities; blockers |
| [Master Roadmap](docs/ROADMAP.md) | Detailed V3.3 through V5 dependency-ordered slices, scope, goals and exit criteria |
| [Engineering Workflow](docs/ENGINEERING_WORKFLOW.md) | Branch/issue/PR, CI, H:-Windows acceptance, evidence, rollback process |
| [V3 Architecture](docs/V3_ARCHITECTURE.md) | Data/scene ownership, region streaming, worker safety, LOD/coast, save schemas |
| [Technical Research](docs/V3_TECHNICAL_RESEARCH_2026.md) | Godot official sources, asset licensing, alternatives, rejected approaches |
| [Acceptance Matrix](docs/V3_ACCEPTANCE_MATRIX.md) | Exact F8/F9/camera/painting scenarios, reproducible metadata and pass/fail |
| [ADRs](docs/decisions/README.md) | Persistent architecture choices and why V3 failed |
| [Historical Project Log](docs/PROJECT_HISTORY_2026-10-30.md) | Original M0/M1/V2 chronological development notes kept intact |
| [Mountain correction](docs/V3_2_MOUNTAIN_SUMMIT_QA.md) | Summit rock art, numeric regression evidence, reference seed 1315511732 |

Review [AGENTS.md](AGENTS.md) before modifying code. The project's [critical V3 visual/input regression issue #46](https://github.com/tahazarif10/worldforge-sim/issues/46) remains open; a Linux Godot boot is not user Windows acceptance.

**Current engineering tracker:** [V3.3 exact Windows baseline #52](https://github.com/tahazarif10/worldforge-sim/issues/52), with first slices [#53](https://github.com/tahazarif10/worldforge-sim/issues/53) and [#54](https://github.com/tahazarif10/worldforge-sim/issues/54). The larger-world stages [#55](https://github.com/tahazarif10/worldforge-sim/issues/55), [#56](https://github.com/tahazarif10/worldforge-sim/issues/56) and [#57](https://github.com/tahazarif10/worldforge-sim/issues/57) retain their physical acceptance dependencies. #55's planner [#63](https://github.com/tahazarif10/worldforge-sim/issues/63), fallback [#65](https://github.com/tahazarif10/worldforge-sim/issues/65) and worker [#67](https://github.com/tahazarif10/worldforge-sim/issues/67) slices are opt-in; distance LOD, large-world authority and physical promotion remain open.

## Architecture summary

| Layer | Current responsibility |
| --- | --- |
| WorldSim / WorldState | Pure C# small-world simulation and authoritative terrain |
| DirectWorldTerrainSource / RegionMirrorTerrainSource | Direct access or optional cache of the same terrain |
| VisualTerrain3D / ocean / local MultiMesh batches | Approved terrain, water, nature and people presentation |
| StrategyCameraRig / VisualLab3D / GameHud | Camera, picking, interaction and scene coordination |

Future bounded regional world: make 64×64 data regions authoritative behind this **same** art/camera path, introduce far-LOD fallback with no blank visible cells, then persistent region/entity simulation and crash-safe saves. No destructive replacement of the V2 visual scene.

## Historical milestones

- M0/M1 established the original Godot project and 2D gameplay; the original 2D/pixel-art art direction was later rejected.
- V2 introduced true stylized 3D, mouse-anchored orthographic camera, animated ocean and real CC0 models.
- V3.0's alternate region demo proved only logical coordinates and some storage; **the user rejected appearance AND movement**. [Postmortem](docs/V3_REJECTION_POSTMORTEM.md).
- [PR #47](https://github.com/tahazarif10/worldforge-sim/pull/47) restored approved V2 as the safe default; [PR #49](https://github.com/tahazarif10/worldforge-sim/pull/49) added opt-in exact data-source parity; [PR #50](https://github.com/tahazarif10/worldforge-sim/pull/50) removed dense rock caps from summits; [PR #51](https://github.com/tahazarif10/worldforge-sim/pull/51) added exact screenshot seed replay.
- **Current:** portable-tool hardening and opt-in single-fan picking are verified and merged in [PR #79](https://github.com/tahazarif10/worldforge-sim/pull/79) and [PR #80](https://github.com/tahazarif10/worldforge-sim/pull/80). The preceding staged-publication host report is recorded; the next checkpoint is RUN-TEST.cmd on this changed candidate. V3.3 physical baseline/QA, resident coast rendering and distance LOD remain open. See the [state](docs/PROJECT_STATE.md) and [roadmap](docs/ROADMAP.md).

## Rights, CI and product boundaries

All art/code must be original or use verified licensing. Bundled Kenney Nature Kit models are CC0; track hashes and source [here](Assets/Art/KenneyNature/README.md). Do not copy WorldBox assets or proprietary game logic.

GitHub core smoke and Godot Mono headless scene tests are **engineering gates**, not performance measurements. Real Windows **AMD R9 M395X** FPS, p95/p99, camera responsiveness and user visual acceptance are distinct required release gates. No new feature is promoted when the screen looks worse, the camera lags or visible land disappears.
Contributor entry point: [CONTRIBUTING.md](CONTRIBUTING.md). Research and repository maintenance stay within the authorized project scope; do not copy project material into unrelated chats, persistent personal memory or public technical search queries.
