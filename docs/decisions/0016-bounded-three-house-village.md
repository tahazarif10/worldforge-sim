# ADR 0016 — A bounded, demand-driven village of three wooden houses

- Status: Implemented candidate; target Windows visual/playtest acceptance pending
- Date: 2026-11-06
- Supersedes: the deliberate single-house limit in ADR 0014 and the one-house WorldSaveState representation in ADR 0015; all other renderer/camera/backup decisions stand

## Context

The first autonomous four-bed wooden hut, actual logging/delivery, construction animation, and atomic living-world snapshot exist. The user requests the immediate next playable step: citizens autonomously add a second and third house when population needs it, with simple fast handoff rather than prolonged release acceptance. Expanding the world renderer, adding a city planner or inventing resources is out of scope.

## Alternatives

1. Three independent house models and separate per-house worker/home ownership (selected). This preserves simulation and compatibility with the first-hut artwork and current save semantics.
2. Treat the second/third huts as purely visual copies of the first (rejected). Those would invent occupied beds and lumber, make demolition inconsistent and break save/replay.

## Decision

- One active building site, up to three houses total, with four residents per completed house. Start the first after at least two citizens and some wood harvested; start each further site only if population exceeds completed capacity. Build second/third sequentially, not in parallel.
- Each site has its own stable ID, deterministic nearby grass plot, grade and collision checks, actual 12-log investment and construction stages. Builder and hauler two-person quotas are per target house, and residents keep their previous completed home when new sites appear.
- A citizen's house job carries a target house ID; only that building can receive the citizen's delivered logs, construction work or rest. Demolition cancels just that building's jobs and housing assignments; no wood is refunded.
- The visual adapter retains the original first-house node and two bounded copies sharing existing opaque mesh/material resources, with independent stage and viewport visibility. No modifications to camera, V2 terrain/water, Kenney art, brushes, raw Godot baseline or project.godot.
- WorldSaveState schema v2 persists an ordered list of homes and per-citizen house job targets. A verified schema v1 snapshot is accepted by migrating its single house and in-flight jobs. The existing SHA-256 envelope and H:-only save path remain unchanged.

## Consequences and limitations

Maximum twelve beds and three homes. This is not village-wide city planning, terrain roads, unlimited buildings, architecture diversity, food economy, regional authority or automatic villager housing beyond the small fixed cap. Missing materials can stall a build rather than generating free stock. The user's Windows runtime is the acceptance gate; no physical FPS or bug-free claim is made.

## Verification and handoff

Focused WorldSim smoke includes a completed first home, a population-triggered distinct second construction plot, real inventory conservation and two-house save/reload. Existing tests cover stage progression and demolition. Source is provided via a GitHub PR/merge for the user's interactive test. Further host visual/performance checks are intentionally deferred at user request.
