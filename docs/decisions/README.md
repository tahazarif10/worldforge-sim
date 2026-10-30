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
| 0003 | Planned | Bounded latest-camera-wins worker pipeline and main-thread upload |
| 0004 | Planned | LOD seam stitching, nonempty fallback visual coverage and coast halo |
| 0005 | Planned | Crash-atomic multi-region save manifest and replay |
| 0006 | Planned | Deterministic region/entity scheduling and inactive-area semantics |

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
