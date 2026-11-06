# ADR 0020 — Generation 1 rustic village cottage as shared staged modules

- Status: Implemented art candidate; physical Windows approval pending
- Date: 2026-11-06
- Extends: ADR 0014 visible cooperative hut, ADR 0016 three logically separate homes
- Preserves: ADR 0001 V2 approved camera/environment/Compatibility rendering

## Context

The user asked to establish a single attractive, old-fashioned first-generation home. Later, citizens might independently upgrade homes and a village may support several house types; those simulation features are not requested now. The former model was largely solid box primitives with a very simple roof/door and little character. The goal is stronger visual quality without sacrificing low-end Radeon support or changing how citizens deliver wood and complete building stages.

## Decision

1. **Only one Gen1 architectural archetype:** a humble handcrafted timber-and-wattle house with an uneven fieldstone base, visible oak framing and diagonal braces, aged clay wall infill, a hand-layered thatch/shingle roof, small warm glass windows, shutter/door framing, simple hearth flue and modest wood/storage props. Keep the present 2×2 logical building footprint; all protrusions are decorative and do not modify pathfinding, entrance or terrain.
2. **Art is modular and phase-aware:** draw static named groups for Planned survey marks, Foundation stones/timber floor, Frame oak posts/braces/rafters, Roof weathered layers and eaves, and Complete walls/doors/windows/homestead. Every module has a `HouseStage From/Through` visibility rule. Only the stage switches; no per-tick remeshing or floating-point-driven animation.
3. **Batched native geometry rather than many nodes:** each module is a single `ArrayMesh` assembled once from boxes/triangles, per-face normals, subdued vertex colors, and the same shared opaque `StandardMaterial3D`. No imported proprietary assets, new shaders, procedural textures, extra point lights, transparency or external download.
4. **Reuse the single prototype for all current homes:** preserve the original `WoodenHouseArt3D`, `HouseModel`, `RenderedHouseId`, `RenderedStage` and `ActivePartCount` contracts, then duplicate the native resource-referencing module hierarchy up to the existing WorldSim cap. Screen culling remains every ~150 ms and stage changes only update already-created module visibility.
5. **Upgrade-ready boundaries:** keep distinct foundation, timber structure, wall infill, roof, entrance/windows and small props as individually addressable reusable modules. Do not create upgrade tiers, architectural variants, new population rules, storage schema, construction materials, job routing or village plans in this slice.

## Verification and limits

No CI is requested or checked for this change. The user will run the H:-based existing VisualLab on Windows and inspect the staged house silhouette, hand-worked roof, front entrance alignment, readability near/far, and performance with the current up-to-three homes. There is **no claim of passed compilation, FPS, physical visual approval or guaranteed seamless performance**. Existing focused native checks can be run later if the user requests. The current implementation is a Gen1 presentation candidate only.

## Gen 1.1 visual refinement — 2026-11-06

The user accepted the first rustic home candidate but requested another substantial visual polish pass, **without changing simulation or waiting for CI**. The same first-generation archetype is retained. The roof now consists of overlapping, deterministically uneven tapered straw bundles with a broken fringe and ridge, merged into its existing native roof geometry batch; the top-face winding is explicitly outward for correct natural sunlight shading. Fieldstones vary in tilt and course spacing. A small new complete-stage gable oak module strengthens the triangular silhouette; old earth plaster gets sparse wear marks, door hardware and latches become legible, window shutters gain ledges/pegs, the stone chimney has separate courses, and firewood/chest/stepping stones are placed where the outside camera can see them.

**Performance boundary:** one additional native static module per house; all shape details are still built only in `_Ready`, use the one shared opaque vertex-color material, and are reused across the existing maximum of three logical houses. No new art imports, GPU effects, point lights, collision geometry or dynamic redraw. The approved original construction stage contracts, logical footprint and save format remain untouched. Physical Windows/Radeon appearance and FPS acceptance are pending the user's hands-on run; no CI check was requested or claimed.
