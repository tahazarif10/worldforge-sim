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
