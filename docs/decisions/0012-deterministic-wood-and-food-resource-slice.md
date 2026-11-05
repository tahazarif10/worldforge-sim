# ADR 0012 — Deterministic wood and food, independent of visual residency

**Status:** Accepted for a small, user-testable WorldSim/VisualLab experiment; host art and performance acceptance pending (2026-11-05).

## Context and decision

The first living-world gameplay slice needs two resources **before** citizen AI or village building. The accepted V2 island, three-dimensional people, camera, painter and legacy Kenney foliage may not be remodeled in the process. Camera streaming must never stop offscreen resource regrowth. This scoped early gameplay experiment does **not** promote regional entity authority or a production save format; the broader V3.5/V3.6 gates remain separately pending.

- `WorldResources` is a pure .NET WorldSim-owned, bounded per-cell resource array, with seeded, coordinate-only deterministic deposits. It never reads render time, camera or Godot objects and **never consumes the WorldState movement RNG**.
- Wood is attached to forest cells that already pass the **same stable hash and 5-of-7 forest scatter gate** as existing tree visuals. Trees hold a limited amount of wood and harvesting one unit leaves their approved decorative mesh unchanged. Wood does not regrow in this prototype.
- Sparse grassland berry sites hold 2–4 food units. Harvest removes one unit and increases explicit collected stock. Missing food regrows one unit per 40 fixed `WorldState.Advance` ticks, including outside view; pausing the simulation pauses growth. Each resource has a revision and survives camera movement and chunk unloading/reloading.
- Painting a *different* biome or a meteor updates/removes incompatible resources atomically; painting the same biome **does not replenish** depleted nodes. New World restores deterministic initial nodes and clears collected stock.
- A new VisualLab **Gather** tool provides manual collection for testing, without citizen AI. Only the red-berry display is added: two opaque MultiMesh batches for small green berry bushes and red fruits in the camera view; depleted bushes retain leaves but lose visible berries. Original tree, grass and flower meshes/materials remain untouched.
- The resource data is authoritative for the duration of the live session, **not yet saved to disk**. Full save/load, production regional ownership, resource-driven AI, tree felling visuals and more resource types remain separate future changes.

## Verification and guardrails

Deterministic WorldSim smoke checks seed parity, stock conservation, resource-free water/mountains, exact regrowth tick, same-biome paint, meteor and New World reset; Godot native smoke checks Gather input, visual fruit quantity, shared meshes and regrowth on live simulation ticks. No background worker controls or camera behaviors are changed.

This experiment **does not claim FPS improvement, new production persistence or user-approved visuals** until actual Windows R9 M395X inspection. The failed unshaded grass/flower PR #96 remains reverted via PR #97.
