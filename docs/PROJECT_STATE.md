---
state_schema: 3
reference_code_commit: 9dc03c8aa608220971b1da3516074f8fd68508a8
last_verified_date: 2026-11-02
audited_repository_commit: 19a7dd797306ed654b9218ff95bd942a0e587ad8
baseline_core_ci_commit: bf70b825528dda2f2bf8fee16e9f7245ec016c15
baseline_core_ci_run: 41109317518
baseline_engine_ci_commit: 41eaaba01e9a6678583c376f7f841751ae4506e6
baseline_engine_ci_run: 41105302717
windows_verified_commit: null
windows_attempted_commit: 2d1dd5ed8cff3f9ab1c9214ccb2bed6f69ea6ea9
windows_attempt_status: "Import/build and direct startup reached; three late C# UIDs detected; capture.json absent; run incomplete"
user_accepted_visual_commit: null
completed_through: V3.2-engineering
active_milestone: V3.4.2
host_acceptance_milestone: V3.3
viewport_planner_issue: 63
surface_fallback_issue: 65
last_interactive_feedback: "2026-11-02 positive qualitative V3.4.1 report; exact local SHA not supplied"
measurement_implementation_pr: 60
active_status: "Opt-in continuous surface fallback and ready-only handoff implemented; exact-head evidence in #65; physical baseline and promotion remain pending"
next_gate: "Verify V3.4.2 exact-head CI and physical behavior; V3.4.3 immutable jobs and measured upload budgets next; no default promotion before host gates"
user_facing_baseline: "Scenes/VisualLab3D.tscn, V2 approved 3D art/camera"
open_blockers: ["#46 V3 visual+camera regression", "#34 production terrain/LOD/art", "#45 regional entity simulation blocked"]
---

# WorldForge Sim — factual project state

**Source audit updated 2026-11-02.** This file is the factual status ledger, not the entire history and not a promise of future implementation. The prior chronology is preserved verbatim in [PROJECT_HISTORY_2026-10-30.md](PROJECT_HISTORY_2026-10-30.md). The actionable future plan is [ROADMAP.md](ROADMAP.md).

## Product status

WorldForge Sim is an original **3D stylized isometric god-sandbox**. The user explicitly approved the **V2** visual direction, forest/water and camera after previously rejecting 2D pixel-art and then rejecting a standalone region-world V3.0 scene. The accepted *visual baseline* is the live V2 screenshot with world seed **1315511732**, AMD Radeon R9 M395X, Godot 4.7.2 Mono Compatibility/OpenGL and a visible HUD around 60 FPS/16.7ms for a short interval. That screenshot is evidence of a good appearance and a single instantaneous counter, **not 30–60s p95/p99 under pan/zoom stress**.

**Historical 2026-11-01 audit snapshot:** the last runtime-code merge at that point was **`9dc03c8aa608220971b1da3516074f8fd68508a8`** (PR #51), with prior mountain correction PR #50. Audited main was **`bf70b825528dda2f2bf8fee16e9f7245ec016c15`** after documentation PR #58. Neither reference establishes an exact user-accepted visual commit; that field remains unknown. User has not yet supplied a new F8 screenshot confirming the corrected mountain summit or the direct vs mirrored 3D frame-time comparison. Do **not** mislabel new mountain art as user accepted merely because merged/Godot CI passed.

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
| Opt-in V3.4.1 bounded viewport demand + V3.4.2 continuous surface fallback | Implemented for the current small world; actual-resource checks, ready-only handoff and canonical sculpted coast banks | [Guide](V3_4_VIEWPORT_STREAMING.md), [#65](https://github.com/tahazarif10/worldforge-sim/issues/65) |
| Physically accepted seamless large map / distance LOD / background mesh jobs | **Not implemented/accepted**; V3.4.3 and V3.4.4 remain open | [#55](https://github.com/tahazarif10/worldforge-sim/issues/55) |
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
| 2026-11-02 | PR #64, reviewed `c28fa4ba184be3f1b35cf6c501b017359c49f6f9`, main `19a7dd797306ed654b9218ff95bd942a0e587ad8` | Core/native PowerShell [41462871822](https://github.com/tahazarif10/worldforge-sim/actions/runs/41462871822), engine [41462871730](https://github.com/tahazarif10/worldforge-sim/actions/runs/41462871730), merged-main [41463368663](https://github.com/tahazarif10/worldforge-sim/actions/runs/41463368663): success; 63,253 pure and 43,567 engine camera samples per backend | User says interactive candidate looks good; exact host SHA not supplied | Isolated GPU metrics, exact-head screenshots and V3.4.2 review |

The above numeric PR50 counts are code/test counts, **not a pixel-level proof of improved art**. The existing V2 HUD screenshot had 60 FPS and 16.7 ms worst in one displayed 1-second interval; this is not an AMD frame-time distribution nor a guaranteed sustained 60 FPS.

### Audit and repository engineering update — 2026-11-01

- Existing core baseline: [run 41109317518](https://github.com/tahazarif10/worldforge-sim/actions/runs/41109317518), head `bf70b825528dda2f2bf8fee16e9f7245ec016c15`, success.
- Existing engine baseline: [run 41105302717](https://github.com/tahazarif10/worldforge-sim/actions/runs/41105302717), head `41eaaba01e9a6678583c376f7f841751ae4506e6`, success. This is an earlier PR #51 head, not a fresh engine run on audited main.
- This revision adds the documentation index, [Persian roadmap](ROADMAP_FA.md), [source audit](REPOSITORY_AUDIT_2026-11-01.md), [ADR 0007](decisions/0007-measurement-evidence-and-scenario-identity.md), contributor templates, broader engine triggers and reference-seed direct/mirror regression coverage. Its own CI evidence is recorded on its PR; do not substitute the baseline runs above.
- At that audit, measurement/streaming/save/entity mechanisms were planned and F9 used rolling engine delta. The V3.3.1 implementation below supersedes the measurement status; streaming, transactional world saves and regional entities remain planned.
- At the audit, the Windows candidate SHA was unknown. A later host attempt identified `6e73e3db555ed7f49768daee0c6db0a898f91ea3` but stopped in preflight, as recorded below. Corrected-summit approval and sustained GPU/input measurements remain **PENDING**.

The detailed audit also identifies main-thread edit/upload work, three-cell foam halo requirements, unbounded retained edit metadata, direct WorldState terrain reads, and simulation/RNG/save contracts required before scaling.

### V3.3.1 capture implementation

| Deliverable | Status |
| --- | --- |
| Monotonic callback capture, engine delta separate, reset/stop/overflow and timestamps | Implemented; meaningful correctness tests added |
| Fixed seed/camera/quality/phase and idle/pan/zoom/paint traces | Implemented, opt-in; simulation paused |
| H: PowerShell runner, metadata/raw frames/pose traces/images/hashes | Implemented; shared validator fails incomplete or mismatched captures |
| F9 monotonic rolling diagnostic and mirror -WorldSeed | Implemented |
| Real target-host run, image/control verdict and live simulation/input scenarios | PENDING |

### Host preflight observation and correction — 2026-11-01

The user's PowerShell transcript confirms a successful fast-forward from `9dc03c8` to `6e73e3d` on the existing local `feat/v2-authored-nature-art` branch. The runner found the portable tools and entered the H: environment, then rejected `Assets/Art/KenneyNature/flower-yellowb.glb.import`. The status also listed the other model imports, shader/C# UID companions and the existing modified `project.godot`.

**Observed result: BLOCKED before capture.** This is a candidate-checkout/preflight observation, not a Windows performance or image pass; `windows_verified_commit` remains null.

The corrected [worktree helper](../scripts/acceptance-worktree.ps1) recognizes companions only for tracked resources, preserves original/prepared copies and hashes, allows preparation-time generation, and freezes configuration before/after every measured process. Source edits and orphan/unrelated files remain blocked. Windows tests reproduce the actual Git state; engine CI checks actual Godot-generated metadata. Exact CI evidence belongs to the correction's PR.

Next host command after safely obtaining the fix: `.\scripts\run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1`. See [sidecar policy and output layout](V3_3_ACCEPTANCE_RUNNER.md#existing-local-godot-import-and-uid-files). The target-host rerun remains PENDING.

### Host runtime preparation observation — 2026-11-01

At candidate `2d1dd5ed8cff3f9ab1c9214ccb2bed6f69ea6ea9`, the supplied transcript confirms 60 existing companions recognized, successful import/build, and entry into the direct idle run. Comparing the saved prepared manifest to the host afterward showed exactly three added companions:

- `Scripts/Instrumentation/AcceptanceOptions.cs.uid`
- `Scripts/Instrumentation/FrameCapture.cs.uid`
- `Scripts/VisualLab/VisualLab3D.Acceptance.cs.uid`

The bundle `artifacts/acceptance/v3-3/20261101-174534-733f465f` had no `idle-1-direct/capture.json`. **Result: INCOMPLETE**, not performance or visual acceptance. The late UID generation is observed; the reason for the missing completed capture is not yet established. Disabled input in this explicit idle fixture is intentional and does not describe normal gameplay.

The [shared runtime preparation](../scripts/acceptance-runtime.ps1) now performs post-build import and bounded direct/mirror boots before requiring all companions and freezing identity. Each measured process preserves its ending worktree state. Missing output and exact changed paths receive distinct diagnostics. The test window title explains paused controls and automatic completion; normal play remains `.\scripts\run-visual-lab.ps1 -WorldSeed 1315511732`.

CI reproduces the exact three missing UIDs in its own disposable checkout and uses the same preparation/observed-process helpers for actual Godot captures. Exact-head evidence is recorded in the fix PR and #54. **H:-Windows rerun remains PENDING.**

### Host startup evidence and authorized V3.4.1 engineering — 2026-11-02

The supplied follow-up confirms all **63** companions recognized, import/build success, all three preparation stages completed and the direct idle fixture initialized on the R9 M395X. The new bundle is `artifacts/acceptance/v3-3/20261102-140354-abc21f68`.

The host's displayed file timestamps show process logs created at 18:34:49, fixture startup logged by 18:34:50 and the ending worktree manifest written at 18:34:57. Stderr is empty; no before/after image or completed capture files exist. Together with the user's confirmation of closing the window, this is an approximately eight-second run ending **before the ten-second warm-up completed**, not a completed performance capture. It does not establish a renderer hang. The exact local HEAD was not printed in this newer transcript; the earlier `windows_attempted_commit` field remains the last explicitly established historical attempt, not a guessed SHA for this run.

The user then explicitly authorized advancing the next development step. [#63](https://github.com/tahazarif10/worldforge-sim/issues/63) implements the **V3.4.1 engineering foundation behind an OFF-by-default flag**: finite camera/height bounds, bounded residency, generation/lease validation and same-scene integration. See [guide](V3_4_VIEWPORT_STREAMING.md) and [ADR 0003](decisions/0003-bounded-viewport-demand-and-generation-leases.md).

No V3.3 physical gate is closed. At that V3.4.1 slice, no coarse fallback, asynchronous mesh jobs, whole-world authority, time/byte upload budgets or large-world performance was claimed. The following V3.4.2 update supersedes only the surface-fallback status. Exact implementation-head CI evidence belongs to the slice PR and issue #63. Windows visual/control/performance remains **PENDING**.

Interactive review command after a safe update: `.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming`. It enables normal controls; closing that interactive session is expected. Omit the switch to use the ordinary planner.

### Positive interactive feedback and V3.4.2 surface fallback — 2026-11-02

After the normal interactive V3.4.1 launch instructions, the user reported **“خوب شده”** and explicitly requested the next step. Record this as positive qualitative feedback for the recommended candidate, not proof of the exact host SHA, a fresh summit screenshot or completed V3.3 measurements. `user_accepted_visual_commit` and `windows_verified_commit` remain null because those identities/evidence were not supplied.

[#65](https://github.com/tahazarif10/worldforge-sim/issues/65) implements the V3.4.2 slice behind the same OFF-by-default switch. [ADR 0004](decisions/0004-continuous-surface-fallback.md) records its alternatives and limits:

- Surface-only land proxies preserve exact V2 tile tops, colors, center relief and shared edited edges; banks are simplified and decoration waits for detail.
- The existing opaque water plane/shader covers water-only cells. Coverage checks inspect current generation, native mesh availability and actual node visibility, not dictionary membership.
- Coverage is refreshed every opt-in game frame before eviction. Terrain/foliage completion with a matching lease publishes detail and retires the proxy. Failed/incomplete replacement leaves coverage available.
- Edits, quality/format changes and world replacement update proxies. Smooth coast banks now use the same sculpted edge heights as their tops.
- F10/F9 distinguish missing detail from missing combined surface coverage; fixture identity is `viewport-v2-fallback`.

The pure and real-engine regression suite covers zero detail budget, actual triangle coverage, islands/water-only/partial chunks, two viewports, fast moves, edited shorelines, incomplete/late handoff and missing native resource repair. **Exact reviewed source SHA, actual CI outcomes and merge evidence are recorded in the #65 implementation PR and issue.** A listed scenario is not by itself a PASS.

**V3.4.2 physical visual/performance acceptance: PENDING.** Fallback construction remains synchronous and adds work; no p99 improvement or GPU/RAM result is claimed. Full distance LOD, worker jobs and time/byte budgeting remain open. Next engineering slice: V3.4.3, immutable numeric generation and bounded measured main-thread upload including dirty edits.

Next interactive command after a safe fast-forward:

```powershell
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming
```

Normal controls remain active; closing the window is expected. F10 should show `MissingVisibleChunks = 0` after successful updates even while detail is pending. F8/F9 remain H:-local evidence. See [the current guide](V3_4_VIEWPORT_STREAMING.md) for the focused review and rollback.

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

## Current work: V3.4.2 opt-in coverage and outstanding V3.3 host acceptance

**Live tracker:** [#52](https://github.com/tahazarif10/worldforge-sim/issues/52); implementation/evidence slices [#53](https://github.com/tahazarif10/worldforge-sim/issues/53), [#54](https://github.com/tahazarif10/worldforge-sim/issues/54).

**V3.3.1 measurement foundation is implemented.** The [runner guide](V3_3_ACCEPTANCE_RUNNER.md) specifies the fixed camera/seed/phase fixture, four scripted scenarios, monotonic timestamps, bounded capture, raw-statistics validation, H: output layout and limitations. The mirror verifier now forwards `-WorldSeed`.

Next: run `.\scripts\run-v3-3-acceptance.ps1 -Scenario idle -Repeats 1` on the target host, inspect the bundle, then collect the full repeated matrix and the remaining manual #53 scenarios. The runner deliberately pauses simulation and invokes camera/brush methods; physical input latency, live simulation, art approval and save/load remain separate.

The implementation's exact-head core, PowerShell and official-engine CI results are recorded in [PR #60](https://github.com/tahazarif10/worldforge-sim/pull/60). No Linux/headless or hosted Windows script test establishes R9 M395X GPU acceptance. #52/#53/#54 stay open for remaining evidence; V3.4 promotion remains blocked.

### Existing commands

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git fetch origin main
if ($LASTEXITCODE -ne 0) { throw 'Download failed. Stop and inspect the Git output.' }
git merge --ff-only --no-autostash FETCH_HEAD
if ($LASTEXITCODE -ne 0) { throw 'Update stopped. Preserve local changes and inspect the Git output.' }
.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732 -ViewportStreaming
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
