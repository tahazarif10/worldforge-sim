# ADR 0022 — Continuous meadow shading and volumetric, walking-lane-aware plants

- Date: 2026-11-07
- Status: Implementation candidate; Windows visual and frame-time acceptance pending.
- Base: a485d63711c89ed599ea5cdbf5abd42be1b20483 (PR #117).
- Supersedes: ADR 0021's tessellated yard overlay and the manual game's decorative Grass/Flower mesh presentation.
- Preserves: V2 scene, camera, live painting, WorldSim, resources, house construction, tree/rock assets and save schema.

## User evidence

After PR #117 the user rejected the visible square/diamond ground pattern, repeated isolated plants, flat/black-looking leaves and citizens walking through vegetation. A small courtyard overlay did not address the global meadow surface and made the geometric patches more conspicuous. This change addresses the complete visible meadow rather than adding another square patch.

## Implementation

### One continuous ground surface

`natural_meadow.gdshader` shades green terrain in world coordinates using smooth, warped, multi-scale value noise. A small linearly sampled biome-data texture supplies continuous Grass/Forest/Sand weights; it is not a repeating ground texture. Homogeneous grass no longer uses alternating per-tile corner/center colors. Lighting normals are softened on gently sloped green tops only; actual terrain elevations, ray picking, coast geometry and non-green cliff/rock shading are retained.

Worn soil around up to three existing foundations and tapered entrance tracks are mixed into this same ground shader. The old `HouseYardArt3D` quad/decal mesh is removed. Its replacement only coordinates house visibility and decorative-foliage invalidation. There is no floating rectangular overlay, no extra ground draw call, no collision mesh, and no WorldState paint operation.

`VisualTerrain3D.NaturalLandscape.cs` installs this material on the established final, fallback and held-brush terrain meshes. Biome bytes are reused and scanned only after the existing surface-edit stamp changes; the texture is uploaded only if biome data actually changed. House uniforms update only when their values change. World replacement resets this presentation context.

### Real three-dimensional ground cover

The manual game replaces only the small Grass/Flower meshes with original closed-volume leaf, stem, petal and bud geometry, built once and reused by the existing per-chunk MultiMesh batches. Vertices have analytic outward normals and Godot's clockwise front-face winding; there are no transparent crossed cards. Existing tree, rock and building artwork is unchanged.

Full foliage and the live painter share `NatureArtLibrary.ResolvePlacement` and the same cached mesh parts. The normal chunk builder skips sparse cluster rejections; live slots preserve their allocation and hide rejected decorative instances. A stable spatial noise envelope produces dense patches and open areas rather than constant per-cell density. No additional simulation RNG is consumed.

### Vegetation and people

The current simulation walks between tile centers. Small plants and harvestable berry artwork are moved within their own logical tiles to seeded off-center positions, leaving the cardinal center corridors open. Decorative plants are shorter/narrower, and resource bushes keep a narrow X/Z footprint and subdued greens instead of the previous large bright balls. Their terrain height is sampled at the actual new render position.

This is **vegetation layout clearance, not a new physics/navigation system**. Resource site IDs, food amounts, gathering targets, worker logic and save state do not move. WorldSim continues to own movement. Dynamic agent-agent avoidance, large-tree collision, or arbitrary diagonal continuous navigation are not introduced or claimed by this patch.

## Scope and activation

The manual launcher (`--game-menu`) enables the presentation. Raw V2 reference scenes retain the original vegetation assets/materials. No `project.godot`, Windows configuration, camera controls, citizen code, house costs or saved-game formats are edited. New plants are original procedural art; no external asset download or proprietary art is included.

## Verification and honest limits

Source/API review and the accompanying numeric placement/closed-mesh checks are useful, but do not certify shader compilation, screenshots or smoothness on the user's Windows Radeon GPU. No CI wait is requested. The user should inspect the meadow at near and far zoom, hold Forest/Land while painting, watch walkers pass berry/flower sites, and reload an existing saved world. Specific frame-rate, zero clipping and visual approval remain unverified until that run.

Technical references: official Godot Spatial Shaders reference (world/view matrices, COLOR/NORMAL, opaque materials); official ArrayMesh reference (clockwise front faces); official MultiMesh optimization documentation (shared batched instances).
