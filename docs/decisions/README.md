# WorldForge Sim — architecture decision records

ADRs explain **why** technical choices were made, how they are verified and which failures caused earlier approaches to be rejected. A later change must reference and supersede an accepted ADR, never silently undo it.

## Statuses

- **Proposed** — draft choice; not an accepted implementation.
- **Accepted** — binding architectural constraint, with source/verification noted.
- **Rejected** — considered, not selected for defined constraints.
- **Superseded** — explicitly replaced by a later ADR, with link.

## Registry

| ID | Status | Decision |
| --- | --- | --- |
| [0001](0001-preserve-v2-renderer-and-compatibility.md) | Accepted (codifies prior source and user direction) | Preserve one approved V2 art/camera pipeline, Compatibility baseline, human visual gate; independent V3 scene is not the user-facing target |
| 0002 | Planned | Deterministic authoritative seeded biome/height region contract |
| [0003](0003-bounded-viewport-demand-and-generation-leases.md) | Accepted for V3.4.1 opt-in foundation | Finite viewport demand, bounded residency and generation leases; worker/upload extension recorded in ADR 0008 |
| [0004](0004-continuous-surface-fallback.md) | Accepted for V3.4.2 opt-in coverage | Surface-only fallback, canonical edited coast edges and replace-on-ready; distance LOD and coast-mask tiling remain planned |
| 0005 | Planned | Transactional whole-world save: compare immutable files/manifest vs SQLite |
| 0006 | Planned | Deterministic region/entity scheduling, versioned RNG and inactive-area semantics |
| [0007](0007-measurement-evidence-and-scenario-identity.md) | Accepted; capture foundation implemented, host gate pending | Monotonic measurement, exact scenario identity and fixture/harness/host acceptance order |
| [0008](0008-immutable-mesh-workers-and-publication-budgets.md) | Accepted for V3.4.3 opt-in workers | Immutable raw snapshots, bounded task/result reservations, owner-thread publication, soft time admission and finite failure handling |
| [0010](0010-staged-publication-and-frame-work-evidence.md) | Accepted for V3.4.4 slice 2 | Separate native terrain/foliage admissions, one retained hidden replacement and correctly aligned per-frame work evidence |
| [0009](0009-bounded-coast-patches-and-host-diagnostics.md) | Accepted for V3.4.4 slice 1 | Bounded coast halo/gutter, exact data parity and automatic failure-preserving host diagnostics |
| [0011](0011-bounded-expanded-visual-lab-map-and-citizen-scale.md) | Accepted for bounded preview; host acceptance pending | Keep 120×88 golden fixtures, offer 192×144 manual preview and 80% citizen scale with bounded coast worker |
| [0012](0012-deterministic-wood-and-food-resource-slice.md) | Accepted for scoped resource experiment; host approval pending | Pure WorldSim wood/berry deposits, fixed-tick regrowth and camera-only berry rendering; no early authority or save-format promotion |
| [0013](0013-bounded-fixed-tick-citizen-resource-jobs.md) | Accepted for opt-in AI pilot; host approval pending | Bounded fixed-tick citizen routing, resource reservations and independent Chop/Pick visuals |
| [0014](0014-first-autonomous-wooden-shelter.md) | Accepted for first-hut pilot; host approval pending | Autonomous real-lumber deliveries, staged cooperative construction, four residents and safe rollback |
| [0015](0015-atomic-living-world-save-and-resume.md) | Candidate; host acceptance pending | Verified whole-world saves including workers, house, resource regrowth and height sculpt under H:, with last-good backup |
| [0016](0016-bounded-three-house-village.md) | Implemented candidate; host test pending | Three-demand-driven homes, independent jobs and materials, ordered save-v2 migration while preserving V2 art and camera |
| [0017](0017-explicit-empty-new-game-and-game-menu.md) | Implemented candidate; host test pending | Empty manual new-game startup, explicit Continue, functional start/pause/settings/help menus and protected saved-game replacement |
| [0018](0018-independent-live-terrain-paint-preview.md) | Implemented candidate; physical brush acceptance pending | Immediate terrain-only held-brush preview, bounded mouse-event coalescing and deferred atomic foliage handoff |
| [0019](0019-live-native-nature-instancing.md) | Implemented candidate; physical GPU validation pending | Edit CC0 tree/rock/grass/flower MultiMesh instances live in the same frame as the ground; no per-mouse GLB recreation |
| [0020](0020-modular-first-generation-rustic-house.md) | Implemented art candidate; user Windows test pending | Single old-world Gen1 cottage from staged, shared vertex-color ArrayMesh modules, preserving all logical houses and construction stages |

Add an ADR when changing: visual backend, camera interaction contract, Godot render mode, data authority, region boundaries, thread/queue/cancellation semantics, save format, world generator or accepted milestone order.

## ADR template

```markdown
# ADR NNNN — Title
- Status: Proposed | Accepted | Rejected | Superseded
- Date: YYYY-MM-DD
- Supersedes: none
## Context
Observed problem, specific sources and constraints.
## Alternatives
At least two options and measured/researched tradeoffs.
## Decision
Exactly what will and will not change.
## Consequences
Expected gains, limitations, new risks, migration.
## Verification
CI tests, H:-Windows physical scenarios, failure/rollback gates.
## Evidence
PRs, commit SHAs, user acceptance, external sources.
```
