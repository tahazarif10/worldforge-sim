# Documentation guide

**Reviewed: 2026-11-01.** Start with the current state and roadmap. Historical reviews explain earlier decisions; they do not override current release gates.

## Current documents

| Question | Canonical document |
| --- | --- |
| How does the opt-in V3.4.1 planner work and launch? | [Viewport planning and bounded residency](V3_4_VIEWPORT_STREAMING.md) |
| What is implemented, verified, blocked or still unknown? | [Project state](PROJECT_STATE.md) |
| What should be built next, in which order, and how does each stage end? | [Engineering roadmap](ROADMAP.md) |
| Where is the detailed Persian explanation? | [نقشهٔ راه فارسی](ROADMAP_FA.md), synchronized with the engineering roadmap |
| What did the latest source audit actually find? | [Repository audit — 2026-11-01](REPOSITORY_AUDIT_2026-11-01.md) |
| How are changes researched, tested, reviewed and merged? | [Engineering workflow](ENGINEERING_WORKFLOW.md), [contributor entry point](../CONTRIBUTING.md) |
| Who owns terrain, rendering, simulation and persistence? | [Target architecture](V3_ARCHITECTURE.md) |
| Which external findings support the plan? | [Technical research](V3_TECHNICAL_RESEARCH_2026.md) |
| Which exact scenarios and measurements establish acceptance? | [Acceptance matrix](V3_ACCEPTANCE_MATRIX.md) |
| Which decisions are binding, and which remain open? | [ADR registry](decisions/README.md) |
| How must automated contributors operate? | [Repository policy](../AGENTS.md) |

The roadmap and Persian explanation describe future work. The state ledger records evidence. An accepted architectural decision can still have an unimplemented mechanism; each ADR must distinguish those statuses.

## Historical evidence

| Material | How to use it |
| --- | --- |
| [Preserved development history](PROJECT_HISTORY_2026-10-30.md) | Original chronology; retain earlier failures and approvals |
| `M1_*`, [early art direction](ART_DIRECTION.md), [early acceptance checklist](ACCEPTANCE_CHECKLIST.md) | Earlier prototype reviews; obsolete 2D priorities are not the current product plan |
| [Earlier research collection](research/README.md) and its architecture/roadmap files | Historical hypotheses; use the current roadmap for ordering and current architecture for ownership |
| `V2_*_QA.md` and [V2 acceptance](V2_VISUAL_LAB_ACCEPTANCE.md) | Evidence for individual source milestones, not blanket current Windows acceptance |
| [V3 regional experiment](V3_REGIONAL_WORLD_QA.md) | Logical region/storage proof, withdrawn as a playable scene |
| [V3 rejection postmortem](V3_REJECTION_POSTMORTEM.md) | Binding lesson: preserve the approved visual and camera pipeline |
| [Region-source parity](V3_1_RENDERER_SOURCE_PARITY.md) | WorldState-backed cache proof; not independent large-world authority |
| [Summit correction](V3_2_MOUNTAIN_SUMMIT_QA.md) | Source/count evidence; fresh physical screenshot approval remains separate |

Do not delete or silently rewrite historical verdicts. When a current contract changes, update the state, relevant roadmap section, acceptance scenario and ADR together. Keep English/Persian milestone IDs and dependencies aligned.

## Evidence and contribution rules

1. Link source findings to an exact audited commit. Link CI to its run and tested head. Record actual Windows candidate SHA separately.
2. Label observations, calculations, design recommendations and unperformed tests distinctly.
3. Use `PASS / FAIL / PENDING / BLOCKED / NOT APPLICABLE` with a reason. A missing measurement is not zero.
4. Keep project material within the authorized repository and current conversation; do not create outside notes, unrelated-chat memories or external task copies.
5. Use generic technical questions for public research. Never place project identifiers, source excerpts or user evidence in public search queries.
6. Repository documentation and contributor templates are checked by [Documentation.Smoke](../tests/Documentation.Smoke/verify.py). Automated checks validate structure and links, not the truth of a physical performance claim.

## Current execution order

`#53 reference fixture` → `#54 valid measurement/parity harness` → `#53 numerical Windows acceptance` → `#52 V3.3 gate`.

The [V3.3 runner](V3_3_ACCEPTANCE_RUNNER.md) now provides monotonic scenario captures, seed/pose/phase fixtures and validation. Next: run the H: host bundle and complete manual #53 gates. F9 is a rolling monotonic diagnostic; old engine-delta files remain historical.

On 2026-11-02 the user authorized the OFF-by-default V3.4.1 engineering slice while the host baseline remains open. [ADR 0003](decisions/0003-bounded-viewport-demand-and-generation-leases.md) records that bounded exception. The next streaming implementation is coarse fallback, not a claim that the physical V3.3 gate has passed.
