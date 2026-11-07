# ADR 0021 — Local first-generation house ground dressing without gameplay mutation

- Status: Implemented visual candidate; physical Windows acceptance pending
- Date: 2026-11-07
- Extends: ADR 0020 Gen1.1 rustic cottage model
- Preserves: ADR 0001 accepted V2 camera, renderer/water and natural art; ADR 0012 resources, ADR 0015 save formats, ADR 0016 three independent houses, ADR 0019 live painting

## Problem

The newly polished cottages looked detached when inspected under the manual launcher’s close-up camera. The adjacent natural grass still used repetitive base biome colors; there was no worn ground at the fieldstone skirt or narrow approach to the real south-side entrance. The user asked to fix the immediate area first, without redesigning the entire island.

## Decision

1. Add a dedicated `HouseYardArt3D` **presentation node** under the existing `WoodenHouseArt3D` owner. It manages one short-lived, camera-culled vertex-colored `ArrayMesh` per *existing* building (no new house types/count). It appears at Foundation, is retained on completion and vanishes when the house is removed.
2. Ground color is projected onto **actual** `VisualTerrain3D.SurfaceHeightAt` samples, with a tiny anti-z-fight lift and continuous, deterministic irregular soil-to-grass blending. It does not paint `WorldState` tiles, create buildings, update inventory or alter pathfinding. Each patch is at most a few dozen tenths of a tile around the 2×2 footprint.
3. The south doorway keeps its existing `EntranceX/Y`. A narrow, meandering compacted-soil path extends only about two tiles past it, tapering into live grass. Stone chips and wear marks share one opaque mesh/material and do not introduce colliders, shadow-casting geometry, per-frame textures or new CC0/proprietary assets.
4. Exclude only *decorative* grass/flower models from the building footprint/very short approach. This is a visual art predicate in both full and live foliage generators. Wood trees, resources, soil, walkability and actual berry deposits remain unchanged; native decorative foliage is invalidated only when a house appearance starts/stops.
5. Keep a tiny per-16×16 display-chunk edit-stamp dictionary. A yard is regenerated only when a nearby terrain/height edit or the house ID/plot changes. Far-away brush edits do not regenerate house ground. New World resets visual ownership. Any overlapping separate house footprint is excluded from yard dressing.

## Alternatives not selected

- Change `WorldState.Paint` to dirt when building: would violate the current saved world/placement terrain contract, modify paths/resources, risk demolition/replay divergence, and break V2 terrain authority.
- Add a large full-world dirt texture/shader and a new screen-space decal pipeline: unnecessary GPU/memory risk on the user's OpenGL Compatibility hardware.
- Spawn hundreds of loose stone/grass nodes around a home: excess draw calls and SceneTree overhead for a local art problem.

## Delivery evidence / limits

The source is handed to the user for an immediate Windows game test, without waiting for CI at the user's request. This is **not** a claim of compiled runtime, a GPU FPS pass or approved image fidelity. Review the foundation shadow/contact, the soft grass/earth transition, the exact door alignment, scenery after demolition and terrain sculpting, and whether close-range decorative grass disappears in the apron. Adjust only visual art if feedback warrants it.
