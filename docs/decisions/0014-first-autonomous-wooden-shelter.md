# ADR 0014 — First autonomous wooden shelter, real lumber and residents

**Status:** Small-world V4.0 pilot; physical Windows R9 M395X visual/FPS acceptance pending (2026-11-06).

## Context

The user approved moving from autonomous berry/wood gathering (PRs #98–#100) to a genuinely constructed wooden house. The first house needs a site, real lumber delivered by villagers, staged work, cooperative builders, occupancy and rest. The accepted 192×144/manual VisualLab island, 80% skinned citizens, camera, terrain/forest art and brush remain unchanged.

## Decision

- **WorldSim owns authority.** \`WorldHouseConstruction.cs\` adds one opt-in \`WoodenHouse\` per live small world with a stable ID, 2×2 grass footprint, adjacent walkable door, stage, invested wood, cumulative work, unique contributing villagers and up to four assigned residents. No Godot objects, camera observations or wall-clock time affect construction. The single-house cap is intentional for the first playable loop; this is not the general village system.
- **Construction is never free.** Site placement is deterministic near an existing citizen after the community has gathered at least two logs; the site itself costs zero. Delivered wood is removed from an individual citizen's \`WoodCarried\` only after reaching the door and performing a fixed-tick delivery job. Stage thresholds: planned→foundation at 3 logs and 4 work units; foundation→frame at 8 logs and 12 work units; frame→roof at 12 logs and 20 work units; roof→complete at 12 logs and 28 work units. Several citizens can travel to the same site, add work and deliver their real logs. Total wood gathered equals wood carried + wood invested + wood lost to destroyed houses during this small-world experiment.
- **House and citizen jobs stay bounded.** The preexisting 1,200-cell / 22-tile logical BFS and six new searches per simulation tick remain the shared ceiling. Home travel uses the existing citizen logical route and fixed tick. The first completed house assigns up to four existing citizens as residents; tired residents walk to the door and rest, recovering energy. No houses are fabricated from mere inventory counters.
- **Terrain edits remain authoritative.** Repainting the same biome leaves the home and materials intact. Changing a footprint tile, blocking the entrance or a meteor can destroy the house; occupants are unassigned and invested wood is tracked as lost, never refunded or duplicated. After a short delay another site may be found. New World resets all home and inventory state.
- **Visual-only adapter.** A small low-poly timber cabin is built once in \`WoodenHouseArt3D\` from opaque reusable Godot box meshes. Survey stakes, floor, posts, pitched roof, walls/door/windows appear incrementally when authoritative stages change. The shared citizen rig retains all approved Idle/Walk/Pick/Chop clips, adding a separate Build hammering animation, a small carried-log prop and tiny hammer, visible only when relevant. One camera-cull check every ~150ms; no scene creation or render calls in \`WorldState.Advance\`.
- **Compatibility/rollback.** Godot direct-scene and engine fixtures keep housing **off** by default. The existing H: manual launcher supplies \`--citizen-housing\` with \`--citizen-ai\` and offers \`-NoCitizenAI\` as a safe rollback. No changes to \`project.godot\`, camera, Paint/Brush, Kenney materials, water, workers, or the original town-less preview.

## Evidence and deliberate limits

Pure WorldSim smoke checks seed parity, all stages, distinct builder cooperation, walkability, lumber conservation, four residents, actual rest, cancellation by terrain destruction and New World reset. Official Godot smoke checks staged meshes and build/wood-carry props. A headless test cannot approve actual GPU output, camera feel or frame timing: the user's Windows screenshot/F9 performance verdict remains a separate gate.

This does **not** add production save/reload, warehouse logistics, multi-house town planning, land ownership, roads, architecture varieties, shelters for 450 citizens, or weather. Those are separate V4.0/V4.2 or V3.5/3.6 follow-up milestones. Existing material inventories and homes are session-local.
