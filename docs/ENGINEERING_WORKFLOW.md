# Engineering workflow — WorldForge Sim (Local Copilot-grade)

**2026-10-30.** Based on the established small-milestone, ADR, tests, physical acceptance and evidence discipline used in the user's local-ai-desktop-copilot project. WorldForge has its own unique **visual / low-end GPU gate**; do not transplant the copilot's privacy requirements as game architecture requirements.

## 1. Order of authority

1. User's latest approved actual gameplay/art screenshot and explicit feedback; **do not overrule with CI**.
2. [PROJECT_STATE](PROJECT_STATE.md) — factual code/evidence state (not the wish list).
3. Accepted [ADRs](decisions/README.md) — architecture commitments and reasons.
4. [ROADMAP](ROADMAP.md) — planned milestone dependencies and exit gates.
5. [V3 Architecture](V3_ARCHITECTURE.md), [Research](V3_TECHNICAL_RESEARCH_2026.md), [Acceptance Matrix](V3_ACCEPTANCE_MATRIX.md).
6. Existing implementation and tests; resolve differences explicitly instead of silently overwriting docs.

Before any proposal or code change, check `README.md`, `AGENTS.md`, target files, current Git branch/tree state, open blockers, last merge and most recent real Windows evidence. Differentiate old 2D prototype, approved V2 and user-rejected V3.

## 2. The milestone slice contract

Every slice must be small enough to review in one PR and contain:
- **ID/name** `V3.3.0`, owner (person/agent), issue, dependency, readiness, baseline HEAD.
- **Problem/hypothesis** from user symptom or measured failure. What observation would falsify it?
- **Precisely in scope**: which modules, no cosmetic cross-system rewrite, product behavior intended.
- **Out of scope**: explicit exclusions; e.g. no independent V3 renderer, no giant water image, no new save format if not in milestone.
- **Architecture contract**: authoritative data owner, cache, region/chunk scope, thread/resource ownership, world-seed/epoch, stale-work cancellation, save/version.
- **Budget**: memory, region/chunk caps and per-frame upload/p95 objectives (as measured or explicitly provisional).
- **Failure modes**: missing GLB, stale region, corrupt save, wrong seed, rapid pan/zoom, large viewport, shutdown while work pending, negative/global coordinates.
- **Pure deterministic tests** with exact `dotnet` commands, expected PASS/FAIL; changed files and API compatibility.
- **Godot import/compile/start** with exact engine version and screenshot reference seed.
- **Physical Windows manual scenario** with exact world seed, camera, viewport, movement, F8/F9/F10 paths, user verdict.
- **Rollback**: flag OFF or restore previous accepted main; never delete local user files or reset `project.godot`.
- **Evidence**: CI run link, exact HEAD, screenshot CSV, quantitative result and known untested work.

One issue per small slice; parent issue tracks stage with checklist and acceptance blocking dependencies. Avoid large branches that conflate art redesign, streaming, AI, UI, and save redesign.

## 3. Safe local H: preflight

All intentional SDK, Godot, H:-portable `_sc_`, project build/cache/NuGet/TEMP/logs/asset downloads remain on **H:\\WorldForge-Sim**. This rule cannot control third-party Windows OS-managed C: writes; do not make untestable guarantees.

PowerShell **5.1 compatible** commands preferred. Never intentionally reset user-local modified `project.godot`:

```powershell
Set-Location 'H:\WorldForge-Sim'
git branch --show-current
git rev-parse HEAD
git status --short
git log -1 --oneline
git fetch origin
```

Do not `git pull` over unreviewed changes, `git reset --hard`, `git clean -fd`, `git checkout -- project.godot`, or automatically stash/commit their changes. If `git switch main` fails due to local state, **STOP and report**, do not force.

The actual portable environment script: `. .\scripts\enter-dev.ps1`. Approved 3D preview: `.\scripts\verify-v2-art.ps1 -WorldSeed 1315511732`. Engineering RegionMirror scenario: `.\scripts\verify-v2-region-parity.ps1`. Rejected `run-regional-world.ps1 -ExperimentalV3` is diagnostic only and must not become default.

## 4. Research → ADR → smallest implementation

For each new architectural change:

1. Audit relevant source at an **exact main SHA** and reproduce user symptom with sample seed.
2. Browse up-to-date **official Godot** documentation and relevant license/art sources; record publication/version and links. Compare at least two feasible approaches and explicit rejection of attractive but unsupported ones.
3. State one falsifiable implementation hypothesis; e.g. "native main-thread upload, not pure C# vertex generation, dominates p99 stalls" with counters to isolate both durations.
4. Add/update ADR for renderer/camera, tile authority, threading, queue/drop policy, world save format or dependency order, **before** a new incompatible architecture.
5. Add Godot-independent C# tests first where possible; do not instantiate GPU scene just to test region arithmetic or stale tokens.
6. Implement a narrow adapter behind explicit opt-in; do not replace approved art/camera or make unrequested gameplay changes.
7. Run *focused* tests and then *full* core + engine CI. Diagnose failures rather than modifying unrelated subsystems to achieve a green badge.
8. If the feature changes what the user sees or touches, obtain **actual Windows F8/F9 and user verdict before promoting to default**.

Do not assume that building successfully on Linux proves R9 M395X Compatibility features, performance, or exact screenshot quality.

## 5. Branch, PR, review, merge

Use branches such as:

```text
docs/v3-3-research-milestone-masterplan
dev/v3-3-0-golden-host-baseline
dev/v3-4-1-viewport-coverage-planner
dev/v3-4-2-immutable-mesh-jobs
fix/v3-4-chunk-revision-race
```

A PR summary must contain:
1. Exact problem/scope, user image or issue reference.
2. Key code diff and why it preserves original V2/Compatibility pipeline.
3. Deterministic tests and **actual passed** named CI run IDs.
4. H:-host steps to reproduce, with explicitly PENDING physical gates.
5. Known limits, risks, rollback and out-of-scope.
6. Updated `PROJECT_STATE.md`, roadmap issue/checkbox if changed, ADR if needed.

**CI acceptance:** both core and official Godot 4.7.2 Mono headless workflows complete with SUCCESS for the exact PR HEAD. If one is queued/in progress, do not call success or merge a breaking code change. A **documentation-only PR** may be merged after documentation/link review and applicable CI; no false physical verification needed because no game behavior changed.

**User-facing promotion:** stricter: screenshot/interaction/performance evidence on H:-Windows plus explicit approval. A PR can be merged as off-by-default engineering infrastructure without passing *product* promotion, but mark that precise distinction in PROJECT_STATE, and never flip the feature flag ON without physical approval.

## 6. Diagnostic loop and rollback

```text
Observed failure (seed / screenshot / frame CSV / camera action)
    -> exact reproducible case
    -> one bounded hypothesis + required counters
    -> smallest code/test change behind opt-in
    -> core tests + official Godot CI
    -> H: Windows exact-seed runtime A/B
    -> PASS / FAIL / UNKNOWN with evidence
    -> keep, adjust, or revert; update state and issue
```

Do not:
- "optimize" by discarding visible chunks without fallback;
- substitute a flat water shader to hide a performance issue;
- claim a peak is fixed solely from low instance count without Windows screenshots;
- diagnose GPU from average one-second HUD FPS;
- add expensive 3D effects requiring Forward+ to the older GPU baseline;
- modify art, camera, UI and region authority in one PR without a justified cross-module contract;
- call a module DONE if an expected physical gate has never run.

If user rejects output (like V3.0), immediately restore approved V2 as playable default, keep failed experiment opt-in, preserve user files, record a falsifiable postmortem and block downstream milestones.

## 7. Milestone review bundle

The proposed *future* V3.3 one-command runner must create on H: a session-scoped directory like:

```text
H:\WorldForge-Sim\artifacts\acceptance\v3-3\<session-id>\
  metadata.json
  result.md
  tests.stdout.txt
  tests.stderr.txt
  godot.log
  screenshots\mountain.png
  screenshots\coast.png
  frames\idle.csv
  frames\pan.csv
  frames\zoom.csv
  frames\paint.csv
  measurements.csv
  SHA256SUMS.txt
```

This directory layout and runner are **planned**, not currently present. Future runner must report metadata without inventing GPU/latency values; it must never publish a PASS when manual image approval is missing. Capturing user screenshots is only allowed in the project's intended H: diagnostic folder, not third-party upload by default.

For every PR's final short user report use:

```text
Stage and PR:
Changed files/behavior:
CI result and exact run:
Windows visual/UX: PASS / FAIL / PENDING
Performance numbers: measured values or "not measured"
Evidence destination:
Limitations / rollback:
Next exact command:
```

## 8. Documentation hygiene

- `PROJECT_STATE.md` is **current factual status**, with frontmatter `state_schema`, `reference_code_commit`, `last_verified_date`, `completed_through`, `active_milestone`, `active_status`, `next_gate`, `open_blockers`.
- `ROADMAP.md` defines future ordering and exit gates. A proposal is not an implementation record.
- ADRs record stable *choices*, not all low-level code detail.
- `V3_TECHNICAL_RESEARCH_2026.md` records current sources, hypotheses and rejected options; update if new Godot feature or GPU evidence changes the decision.
- `V3_ACCEPTANCE_MATRIX.md` stores reproducible scenarios and exactly recorded physical results.
- Each major milestone ends with evidence link, exact code HEAD and a short review-ready verdict; preserve prior historical state in archive, don't erase known failures.
- Update `README.md` to point to these source-of-truth documents, not duplicate stale claims.

## 9. Long-run priorities

**Engineering invariants:** gameplay fidelity and player trust > correctness/determinism > observable stability > bounded performance > polish. The user wants a premium art style **and** playable older-machine performance. No tradeoff is allowed silently; expose it as a measured choice and ask for aesthetic approval only at a real checkpoint.
