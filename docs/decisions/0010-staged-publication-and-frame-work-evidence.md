# ADR 0010 — staged native publication and aligned frame-work evidence

- Date: 2026-11-03
- Status: accepted for V3.4.4 slice 2; existing worker flag remains OFF by default
- Scope: [#74](https://github.com/tahazarif10/worldforge-sim/issues/74), parent #55
- Extends: [0008](0008-immutable-mesh-workers-and-publication-budgets.md), [0007](0007-measurement-evidence-and-scenario-identity.md)
- Supersedes: ADR 0008's single combined terrain/foliage publication unit, only in worker mode

## Context

The user approved the preceding diagnostic milestone and authorized continuation. The received short R9 M395X report has paint publication maxima around 7 ms and 11/23 soft budget overruns. Raw captures/images were not supplied; those maxima cannot identify the expensive phase or prove a backend/worker regression.

Source inspection shows a detail unit combines terrain ArrayMesh conversion/upload and foliage/native-instance creation before its final handoff. An admitted unit can cross the soft 4 ms budget without another admission boundary.

## Alternatives

| Approach | Decision |
| --- | --- |
| Retain one combined detail unit | Preserves current latency; retained via the preceding revision, but no admission boundary between terrain and foliage |
| Lower visual quality or increase the soft budget | Not selected; neither diagnoses the observed tail |
| Move native resource creation to numerical workers | Not selected; keep imported shared resources and active scene mutation on their current owner |
| Stage one hidden terrain replacement, then admit foliage and final handoff separately | Selected: preserves geometry/art, bounds staged ownership and creates a falsifiable admission boundary |

## Decision

A detail result has two admitted phases: terrain preparation and foliage plus final matching-generation handoff. Each consumes one native unit; charge terrain arrays (40 bytes/vertex) and numeric prop records (64 bytes/prop) separately, exactly once. The 2 Eco / 4 normal units, 2 MiB numeric payload and 4 ms soft admission window retain their values. One native phase remains non-preemptible.

Keep at most one hidden staged replacement, with its original completed pool entry and conservative array reservation retained until handoff/discard. Staged native objects remain off the active tree until complete. Recheck the exact generation/lease before each phase and handoff. Existing fallback/current valid coverage stays visible; terrain and foliage become current together.

On invalidation, failure, world reset, eviction or teardown, free the staged native resources on the owner thread. Reset may leave the completed numeric entry reserved until the next cleanup pass; canceled live work always retains its reservation until it finishes. Native memory and driver storage are not claimed to be covered by the managed-array reservation.

Record bounded work rows aligned to the preceding completed _Process callback, not the new action after the interval has already ended. Keep action, visibility, flush and nested worker snapshot/terrain/foliage/coast/pump durations separate. Include the slowest interval rows in the copied report, and full rows in work.csv. Phase metrics are CPU wall durations and overlap by containment; do not sum nested phases or label unmeasured interval time as GPU execution.

Capture schema 3 requires work trace alignment and preserves schema 2 reading for historical bundles. Fixture metadata records the publication policy. Ordinary interaction does not allocate the acceptance trace.

## Verification and limits

Force native one-unit and zero-budget boundaries, then test hidden staging, exact art/geometry parity, reservation retention, edit/format/world invalidation, failure, recovery and teardown. Fake-clock work rows test long stalls, off-by-one joins, future/negative/nonfinite times, missing/overflow rows and invariant CSV formatting. Validate actual engine work rows against frame sequences and elapsed intervals.

Required exact-head core, Windows scripts/real engine preflight and official engine checks remain. Physical gains and the mirror zoom cause remain pending until matched target-host data/images. This slice does not implement resident coast textures, LOD, larger worlds, regional authority or default promotion.

## Official sources

- [Godot 4.7 thread-safe APIs](https://docs.godotengine.org/en/4.7/tutorials/performance/thread_safe_apis.html): keep active tree and rendering operations on the owner under the existing model.
- [Godot 4.7 ArrayMesh](https://docs.godotengine.org/en/4.7/classes/class_arraymesh.html): array surface creation can precede adding its node to the scene.
- [Godot 4.7 Node](https://docs.godotengine.org/en/4.7/classes/class_node.html): explicit node lifetime and child cleanup.

These references establish API/lifetime behavior, not R9 M395X throughput.
