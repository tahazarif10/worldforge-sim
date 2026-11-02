# ADR 0003 — Bounded viewport demand and generation leases

- Status: **Accepted for the V3.4.1 opt-in engineering slice**
- Date: 2026-11-02
- Issue: [#63](https://github.com/tahazarif10/worldforge-sim/issues/63), parent [#55](https://github.com/tahazarif10/worldforge-sim/issues/55)
- Supersedes: the roadmap prohibition on starting any V3.4 engineering before host acceptance, only for this explicitly authorized OFF-by-default slice
- Preserves: [ADR 0001](0001-preserve-v2-renderer-and-compatibility.md) visual contract and [ADR 0007](0007-measurement-evidence-and-scenario-identity.md) physical evidence requirements

## Context and authorization

At audited main `1bedd907b154a5789cab685a693c604a308b03ae`, the visibility adapter intersects viewport corner rays with Y=0.12 and Y=5. The supported elevation overlay permits ±8m. The fixed planes and implicit margin are not a complete declared height/near/far coverage contract. No missing-land measurement on the user's GPU is asserted.

The user explicitly requested advancing the next implementation stage on 2026-11-02. V3.3 capture was manually closed before its ten-second warm-up completed. That is not physical acceptance. Engineering may proceed behind a flag; product promotion still requires the outstanding host evidence.

## Alternatives

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Retain fixed sea/mountain planes | Existing default, low implementation cost | Does not express sculpt bounds or finite near/far clipping |
| Project a finite camera volume through a full height slab | Handles steep, shallow and inside-slab views; pure CPU tests | Conservative axis-aligned footprint over-requests rotated corners |
| Exact polygon/cell intersection | Tighter demand | More geometry predicates and numerical edge cases; defer until extra demand is measured |
| Cap a sorted list and drop the rest | Small list | Rejected: silently loses required geometry |
| Cancel every request on camera movement | Simple invalidation | Rejected: continuous movement can starve still-useful work |

## Decision

1. Obtain near/far points for the four actual viewport corners using Godot 4.7 `Camera3D.ProjectPosition`. The pure planner receives only eight numeric vectors, world dimensions, chunk size and a height slab.
2. Clip the finite frustum against the two horizontal height planes. Include original corners inside the slab and original-edge intersections with the slab planes. Bound their X/Z projection conservatively. Clip to world extents and add a small numerical boundary padding.
3. Current terrain envelope is [-8,13] metres: supported ±8m offsets plus the current plateau/relief upper bound below 5m. The real-engine test samples edited surface heights against this contract. Changed height algorithms must update this envelope and tests.
4. Track visible bounding-rectangle demand separately from a one-chunk margin. Display chunks remain 16×16; data regions remain 64×64. The planner does not create authoritative region data.
5. Refuse an over-capacity complete footprint before enumerating it or evicting current geometry. Never return an accepted truncated subset. The adapter's 2,048-entry ceiling is an engineering metadata guard, not a measured GPU budget.
6. Queue states are Unloaded, Queued, Generating, Ready, Stale and Disposed. Only current demand has retained metadata. A ready mesh may remain while its replacement is stale/queued.
7. Publication validates world epoch, data revision, LOD, geometry format and a unique request ticket. Tickets are not reset on world replacement. A removed/re-added key cannot accept an old result even if its data stamp happens to match.
8. Camera movement updates demand and priority only. Still-wanted work retains its ticket and first-demand cohort. Visible requests precede margin requests; older cohorts precede new ones, then distance and coordinates break ties deterministically.
9. All queue ownership and Godot work remain on the main thread. This slice is synchronous. Pure workers, bytes/time upload budgeting and coarse visual fallback remain V3.4.2–V3.4.4 work.
10. Integration uses the existing V2 scene behind `--viewport-streaming` / PowerShell `-ViewportStreaming`. Default scene, renderer, camera algorithm, water, art and project configuration are preserved.
11. Record missing planned visible terrain chunks by membership. A cell count is explicitly an **upper bound** across those missing chunks, not a measured empty-pixel count. Until fallback exists, pending demand can still mean temporarily missing detailed terrain.

## Verification

- Pure tests: chunk/world boundaries, finite clip planes, raised/lowered terrain, shallow view, large logical dimensions, capacity refusal and independent interior-frustum samples.
- Queue tests: continuous camera motion, fairness, edit/LOD/format/world mismatch, duplicate/late results, remove/re-add ABA, failure retry, capacity refusal and disposal.
- Real Godot test: independent `IsPositionInFrustum` oracle, actual viewport resize, camera poses/zoom, two-build cap, edit/format invalidation and world replacement; both terrain sources and default-OFF behavior.
- Existing V3.3 captures continue on the legacy default. Fixture and F9 metadata identify the active planner so different modes cannot be treated as the same fixture.
- Exact-head CI evidence belongs to the implementation PR. Windows visual/input/frame-time evidence remains **PENDING**.

## Consequences and rollback

This is a tested foundation for streaming, not a claim of complete no-hole rendering or better FPS. The bounds can load unnecessary corner chunks; future optimization must preserve coverage. Dirty mesh rebuilds and coast texture work remain synchronous and are not yet governed by a byte/time budget.

Rollback for a session is to omit `-ViewportStreaming`. Keep local modified configuration, companion files and existing evidence. Do not reset/clean the user's checkout.

## Primary sources

Checked 2026-11-02:

- [Godot 4.7 Camera3D projection and frustum APIs](https://docs.godotengine.org/en/4.7/classes/class_camera3d.html)
- [Godot 4.7 thread-safe API limits](https://docs.godotengine.org/en/4.7/tutorials/performance/thread_safe_apis.html)

The clipping and queue choices are project design decisions. The engine CI verifies the actual 4.7.2 Mono API surface.
