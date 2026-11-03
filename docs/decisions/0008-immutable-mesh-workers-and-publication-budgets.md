# ADR 0008 — Immutable numeric workers and bounded native publication

- Status: Accepted for the OFF-by-default V3.4.3 engineering path; physical promotion pending
- Date: 2026-11-03
- Extends: [0003](0003-bounded-viewport-demand-and-generation-leases.md), [0004](0004-continuous-surface-fallback.md)
- Supersedes: their synchronous-detail limitation only when the new worker flag is selected
- Implementation: [#67](https://github.com/tahazarif10/worldforge-sim/issues/67), [PR #68](https://github.com/tahazarif10/worldforge-sim/pull/68)

## Context

Base main is `6a52ca82aa86b7332b9585df2c99610b2b6ff932`. V3.4.2 provides current-generation surface coverage, but detailed mesh/scatter work and dirty shore-mask computation are synchronous. Count-only scheduling does not bound a native operation's duration. The user again reported positive interactive feedback and authorized the next engineering slice; the exact Windows SHA, screenshots and isolated GPU measurements were not supplied.

The [Godot 4.7 thread-safety reference](https://docs.godotengine.org/en/4.7/tutorials/performance/thread_safe_apis.html) identifies active scene-tree and rendering-resource risks. The [ArrayMesh guide](https://docs.godotengine.org/en/4.7/tutorials/3d/procedural_geometry/arraymesh.html) supports uploading aligned numeric attributes. These sources support an ownership boundary; they do not establish throughput on the target GPU.

## Alternatives

| Option | Benefit | Reason selected or deferred |
| --- | --- | --- |
| Retain synchronous detail with count cap | Smallest code change, exact existing renderer | Preserved as rollback; numerical generation still blocks the game frame |
| Build Godot nodes/resources in background | Could move more adapter work | Rejected here: shared imported resources and rendering synchronization complicate safety |
| Pure snapshots plus bounded owner-managed tasks | No live-world/native access off thread; explicit completion ownership | Selected for the current two-worker/four-result workload |
| Long-lived channel consumers | Natural producer/consumer backpressure | Viable later; requires separate queue, result and cancellation accounting while current demand is already coalesced |
| Indexed or simplified mesh rewrite at the same time | Could reduce payload or vertices | Deferred to separate measured work; this slice preserves V2 triangle order and art formulas |

## Decision

### Immutable input and pure result

On the owner thread, capture at most a 16x16 display chunk plus one-cell biome halo (18x18) and its owned corner lattice (17x17 offsets), seed, bounds, art-availability flags and generation/lease. Private owned arrays outlive neither their retained job nor its result references. No live delegates, WorldState, RegionStore, Godot objects or shared material state enter a worker.

The pure builder returns V2-compatible positions, normals, colors and numeric foliage placements. Native transforms, ArrayMesh, MultiMesh, optional model instances, ImageTexture and scene-tree mutation remain on the owner thread. The existing source is still WorldState or its validated RegionMirror; this is not the V3.5 authority migration.

A dirty coast job owns a tile snapshot and uses the unchanged coast kernel. This preview explicitly rejects worlds above **16,384 cells** before binding; the normal world remains 120x88. The initial/NewWorld mask is built synchronously to make the original ocean available immediately. Large-world tiled masks and three-cell/filter borders remain V3.4.4 work.

### Backpressure and memory accounting

| Bound | Value and unit |
| --- | --- |
| Active tasks, including tasks awaiting a thread-pool worker | 2 |
| Retained running/completed entries combined | 4 |
| Total reserved managed array payload | 16 MiB |
| Reservation per detailed chunk, including snapshot/scratch/result allowance | 4 MiB |
| Reservation per small-world dirty coast job | 256 KiB |
| Maximum detailed numeric vertices | 27,648 |
| Maximum numeric foliage records | 768 |
| Maximum retained chunk-result payload | 1,155,072 bytes: vertices x 40 + prop records x 64 |

The reservation is conservative array accounting, not an allocator, an OS working-set cap or a VRAM guarantee. Task/object headers, GC timing, imported shared meshes, conversion copies, scene nodes, proxies, driver storage and native resources are additional costs. Canceled work keeps its reservation until its task has actually completed and the owner releases it. There is no unbounded task backlog.

### Publication budget and measurements

Every worker-enabled game frame pumps completed work. Admission has hard limits of **2 units in Eco / 4 otherwise** (also limited by the caller) and **2 MiB numeric result payload** per update. A unit is a detailed chunk with foliage, or one dirty coast texture result. These bytes describe result arrays; they are not measured PCIe transfer bytes or full imported-model size.

A monotonic **4 ms soft admission window** covers worker-pump scheduling, capture and publication. Before each publication/capture the owner checks elapsed time; one native upload or snapshot cannot be preempted. If it overruns, record the overrun and admit no further work in that update. Count/bytes remain hard; milliseconds are explicitly soft. Record snapshot, pure compute, actual native publication and total pump durations separately. Immediate coverage proxies are outside this detail budget and remain synchronous.

### Validity, failure and teardown

Check current world epoch, data revision, format/LOD and unique lease before native allocation and again before handoff. Camera motion retains still-wanted current work; removed demand cancels or discards it. World replacement advances epoch, cancels old jobs and retains their reservations until completion.

Stage terrain and foliage before replacing dictionary ownership. Successful matching publication makes detail visible and retires its proxy in the same owner update. Failed construction preserves fallback. Retry a failed current generation once; after two failed attempts mark it Failed instead of spinning. A new data generation or re-entry permits retry. Dirty coast attempts also stop after two failures until a new edit. Expose failures and pending coast, rather than reporting false completion.

On scene exit, cancel and join the bounded pure tasks before disposing owner metadata. Builders never wait for the main thread. Cooperative cancellation is checked at bounded row intervals and before returning; see [.NET cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads). Cancellation requests are not proof that work has stopped.

### Rollout

Use a new explicit `--mesh-workers` / `-MeshWorkers` flag. It implies viewport coverage and records planner identity `viewport-v3-workers`. The original `-ViewportStreaming` retains `viewport-v2-fallback` and synchronous detail. Both remain OFF by default. No camera, world-size, approved shader, asset or default-scene migration is bundled.

## Verification

- Pure tests: snapshot isolation from subsequent edits and live owner-only reads, output bounds/all eight geometry flags, finite normals, coast byte parity, worker/count/byte limits, late cancellation, failure, old-world leases, join, and fake-clock publication admission.
- Actual Godot probe: compare native mesh positions/normals/colors and foliage mesh/material/transform/color output against the retained renderer across all eight formats, three art profiles and partial/sculpted chunks, on direct and mirrored terrain.
- Dirty-coast oracle records the hash of successfully consumed bytes and verifies the current native binding. The probe detects whether texture updates can be read back; Godot headless dummy storage does not implement them, so no GPU pixel claim is inferred.
- Adapter lifecycle: camera reversal/resize, zero-budget coverage, edits/coast, format changes, old-world work, finite failure retries, stale native handoff, per-frame hook and real scene teardown.
- Existing viewport/fallback, baseline capture and native Windows PowerShell suites remain required. Exact-head results belong to PR #68/#67; listed tests are not proof of execution.
- Windows R9 M395X: exact-seed F8 images, normal camera/brush/new-world behavior, diagnostic F9 data and complete matched scenarios before default promotion. Headless parity does not prove final rendered pixels or speed.

## Consequences and next work

The numeric worker path intentionally duplicates the retained detailed formulas under native parity tests so rollback remains independent. Any future art-formula change must update both paths and keep that comparison. Task scheduling, snapshots and extra arrays may cost more than they save on a small world; measurement decides benefit.

V3.4.4 evaluates distance LOD and tiled coast seams only after preserving these contracts. If a single native unit is too expensive on the host, split publication work or reduce independently approved far detail in a later measured change; do not relabel the soft budget as a hard deadline.

## V3.4.4 slice 2 extension

[ADR 0010](0010-staged-publication-and-frame-work-evidence.md) supersedes the single combined native terrain/foliage admission unit. It retains worker ownership and limits while admitting terrain preparation and foliage/final handoff separately, with at most one retained hidden replacement. Per-frame work evidence is aligned to the completed callback. Other constraints here continue to apply.
