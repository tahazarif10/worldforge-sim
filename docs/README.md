# Documentation guide

**Reviewed: 2026-11-04.** Start with the current state and roadmap. Historical reviews explain earlier decisions; they do not override current release gates.

## Current documents

| Question | Canonical document |
| --- | --- |
| How are native phases staged and correlated to frame stalls? | [V3.4.4 staged publication](V3_4_STAGED_PUBLICATION.md) |
| How do the new worker jobs, budgets and rollback work? | [V3.4.3 mesh workers](V3_4_MESH_WORKERS.md) |
| How do the opt-in V3.4 planner, surface fallback and launch work? | [Viewport demand, coverage and handoff](V3_4_VIEWPORT_STREAMING.md) |
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

On 2026-11-02 the user authorized the OFF-by-default V3.4.1 engineering slice while the host baseline remains open. [ADR 0003](decisions/0003-bounded-viewport-demand-and-generation-leases.md) records that bounded exception. Following positive qualitative interactive feedback and renewed authorization, V3.4.2 adds surface fallback under [ADR 0004](decisions/0004-continuous-surface-fallback.md). Renewed positive feedback and authorization on 2026-11-03 advance V3.4.3 immutable workers and bounded publication under [ADR 0008](decisions/0008-immutable-mesh-workers-and-publication-budgets.md), with a separate explicit flag. V3.4.4 distance LOD/coast seams is the next architecture slice; actual host profiling determines any prerequisite native-publication optimization. Physical V3.3 acceptance and default promotion remain open.

## Latest engineering slice

[V3.4.4 coast patches and one-click host diagnostics](V3_4_COAST_PATCHES_AND_SELF_TEST.md) describes RUN-TEST.cmd, local evidence/clipboard behavior, bounded coast data, remaining LOD work and test limits. [ADR 0009](decisions/0009-bounded-coast-patches-and-host-diagnostics.md) records the decision.

The user approved the short diagnostic milestone; [slice 2](V3_4_STAGED_PUBLICATION.md) / [ADR 0010](decisions/0010-staged-publication-and-frame-work-evidence.md) separates terrain/foliage admission and adds schema 3 work evidence. The 2026-11-04 host report is now recorded in [acceptance matrix section I](V3_ACCEPTANCE_MATRIX.md#i-received-staged-publication-host-report--2026-11-04), with eight VALID short captures and distinct paint-visibility/zoom findings. Portable tool-state hardening and the scoped picker follow-up are recorded in [PROJECT_STATE](PROJECT_STATE.md); repeated speed benefit and full physical acceptance remain open. Resident coast and distance LOD remain subsequent work.
