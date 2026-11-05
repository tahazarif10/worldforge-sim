# ADR 0013 — Bounded fixed-tick citizen resource jobs

**Status:** Accepted as an opt-in small-world gameplay pilot; Windows host art/performance approval pending (2026-11-05).

## Decision

The user requested that **people—not mouse clicks—gather food and chop individual trees**. In PR #99, exhausted trees became visually felled and persistent stumps. This slice adds the next behavior while preserving the already-approved V2 camera, brush, land, forest art, 80% rigged person, 192×144 preview and viewport-only animation.

- Pure `WorldState` owns the authoritative fixed-tick AI, citizen hunger, carried and eaten food, carried wood, target and activity. No Godot resource, camera position, animation clock or UI button advances the world simulation.
- One stable `WorldResources.SiteId` is reserved to exactly one citizen at a time. Reservations are released on depletion, terrain edits, invalid routes, and citizen removal. A successful work cycle takes multiple fixed simulation ticks and gathers **exactly one** unit; chopping repeats until a tree is depleted or hunger takes precedence. Gather remains a manual debug tool, not the autonomous driver.
- Deterministic cardinal BFS navigates `Walkable` tiles, not detailed terrain meshes, within at most **1,200 visited cells / 22 Manhattan tiles per search**, with **six new path searches per world tick**. Navigation is a bounded pilot only, not hierarchical cross-region routing; agents can retry unreachable/contended resources later.
- Food hunger is reduced by consuming an actual gathered unit; it is accounted as `FoodEaten`, not simultaneously available carried inventory. World resource collection totals are cumulative, separate from citizen-held stock.
- Autonomous AI is **opt-in** in the raw Godot scene via `--citizen-ai` and on by default in the project's safe `H:\WorldForge-Sim\scripts\run-visual-lab.ps1` manual launcher; `-NoCitizenAI` restores previous behavior. This preserves the deterministic legacy movement fixtures until actual user Windows approval.
- The existing 13-bone `AnimationTree` retains the approved Idle/Walk, adds separate Pick and Chop clips and a tiny low-poly axe shown **only while chopping**. Only visible citizen rigs animate; all citizens simulate offscreen at the same fixed tick rate. No additional resource AI job queues run on mesh workers.

## Tests / limitations

- Deterministic pure-C# tests compare two equal-seed worlds, verify food/wood resource conservation, reservations, bounded worker count, safe walkable paths and actual gathering. A focused native Godot probe checks the new animation clips and attached tool. Compile and imported-scene tests are source checks, not artistic approval.
- Citizens may temporarily wait when nearby resources are inaccessible. This small-world pilot does **not** add houses, storage transfer, family behavior, full saves, path caching across regional worlds or claims about thousands of entities. The previously approved simulation/visual baseline stays available via the launcher rollback flag.
