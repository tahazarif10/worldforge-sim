# WorldForge Sim — release roadmap

## Vision

An original, top-down living-world god sandbox: readable premium pixel-art presentation plus deterministic autonomous agents, settlements and kingdoms. Initially genre-comparable to WorldBox in *features and interaction patterns*, not a clone of proprietary game assets or implementation. The product expands into custom user-designed mechanics after the core is stable.

## Track A: Visual quality (current priority)

| Phase | Outcome | Entry gate |
|---|---|---|
| M0 — Playable bootstrap | Procedural land, basic humans, god brushes, tick control, H:-based toolchain | .NET C# build + 7 simulation smoke tests + Godot F5 evidence (obtained); finish full acceptance script |
| **M1 — Visual Foundation** | [#2](https://github.com/tahazarif10/worldforge-sim/issues/2): art direction, UI scale, camera polish, renderer separation, tile transitions, water polish, visual QA | M0 PR #1 accepted |
| M2 — Terrain Art Upgrade | Authored tile atlas, rich biomes, dense decoration, natural cliffs/coastline, layered shadows | M1 renderer contract stable |
| M3 — Character Visual Upgrade | Animated, directional citizens/animals, sprite architecture, selection feedback | M1 layers stable |
| M4 — VFX / God Powers | Meteor, smoke, fire, impact wave, forest growth, water splashes | M1 + effect architecture |
| M5 — Advanced UI/UX | Category toolbars, info panels, tooltips, notification system, accessibility | M1 HUD baseline |
| M6 — Living World Feel | Ambient motion, soundscape, performance-conscious lighting, optional day/night | M2/M3 |
| M7 — Civilization Presentation | Original houses/roads/faction visuals and settlement growth animations | Gameplay track settlements/kingdoms available |

## Track B: Simulation systems (not postponed indefinitely)

| Workstream | Outcome | Dependency |
|---|---|---|
| G1 — Ecology | Walkability/pathing, needs, aging, animals, resource scarcity | M0 core, M1 rendering |
| G2 — Settlements | Autonomous villages, structures, food gathering, construction jobs | G1 |
| G3 — Kingdoms | Political groups, borders, leaders, war, alliances | G2 |
| G4 — World persistence | Versioned save/load, deterministic command replay | Core stability |
| G5 — Custom mechanics | Unique user-designed features beyond WorldBox genre baseline | Stable extension APIs |

The release order between graphic and simulation workstreams may overlap, but no implementation PR should block active gameplay. Maintain separate performance budgets and test suites.

## Workflow

- A branch per scoped task, an Issue per task, a reviewable PR per task.
- User's Windows/H: host is final runtime acceptance authority.
- Test original art asset provenance/license and screenshots.
- Update docs/PROJECT_STATE.md after evidence. Never mark unrun checks PASS.
- Canonical local project: `H:\WorldForge-Sim`. All intentional project downloads/installs/builds/cache on H:, not C:.
