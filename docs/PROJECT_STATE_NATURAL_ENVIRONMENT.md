# Natural environment update — project-state supplement

Date: 2026-11-07
Base reviewed: `a485d63711c89ed599ea5cdbf5abd42be1b20483` (PR #117)
Status: implementation candidate for the user's Windows playtest; no claimed C#/shader compilation or GPU acceptance.

The user rejected the latest screenshot's square/diamond ground pattern, repeated flat-looking plants and people overlapping bushes. This follow-up replaces the manual launcher's meadow presentation rather than adding more tiled yard geometry.

## Changes

- A continuous world-space meadow shader combines smoothly sampled biome weights with warped broad/fine color variation. Repetitive per-cell meadow colors and gently sloping fan-normal accents are no longer the default visible grass pattern. Terrain elevations/picking are unchanged.
- Cottage soil and entrance tracks are part of the same ground material. `HouseYardArt3D` no longer emits a rectangular grid of lifted quads.
- Two original closed-volume ground-cover meshes replace the manual game's small Grass/Flower models. Real leaves, stems, petals and buds have outward analytic normals and clockwise Godot front faces. They reuse the existing shared MultiMesh full-build/live-paint path.
- Stable noise-based patches create sparse and denser plant clusters instead of constant cell density. Decorative plants and smaller berry artwork use off-center positions inside their original logical cells, leaving cardinal tile-center walking corridors open. This is planting-layout clearance, NOT a new physics or navigation implementation.
- Actual berry/wood amounts, target site IDs, saves, house models, population rules and camera are untouched. There are no downloads or proprietary assets.

## Local numeric review actually performed

A separate Python numeric model exercised the identical hash/anchor equations at 25,600 seed/cell combinations. All anchors stayed inside their original logical tiles. The minimum anchor-to-cardinal-center-line distance was 0.3700 units. Using the generated grass model's radial bound at its maximum presentation height, the remaining geometric clearance was 0.2815 units; the narrow berry canopy clearance was 0.2600 units. These checks concern static cardinal lanes, not arbitrary diagonal movement or dynamic collisions.

The ellipsoid geometry construction was numerically checked for nondegenerate triangles, shared closed edges, outward normals and clockwise winding. The grass mesh has 192 triangles. These numeric checks are NOT Godot runtime tests, a screenshot proof, a measured frame-time result or a C# compilation pass.

## User test

Launch the existing H: script, continue the same saved world, and compare the meadow at close/far zoom. Hold Forest/Land to confirm the live preview matches the final material. Watch normal walking and gathering near plants. Check that food quantities do not change from this presentation update. Report the actual screenshot and any shader/build error before claiming acceptance.

The implementation rationale and scope are in [ADR 0022](decisions/0022-natural-meadow-and-volumetric-plants.md).
