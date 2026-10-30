# WorldForge Sim — Art Direction v1 (M1)

## Product goal

An **original**, visually rich 2D top-down god sandbox with recognizable tiny ecosystems, readable units at multiple zoom levels, coherent biomes, satisfying destruction/creation and legible controls. WorldBox is a genre reference, **not an asset source**. Do not copy proprietary sprites, UI, icons, animations, textures, audio, source code or trade dress.

## Baseline and constraints

- Baseline world: 160×120 logical terrain samples; initial rendering uses 8 world units per sample; fixed screenshot seed: `20261030`.
- Godot 4.7.2 .NET / net10.0; Windows compatibility renderer and AMD Radeon R9 M395X are an important reference target.
- Original artwork stored in `Assets/`. Keep source files alongside exported assets where legal and practical.
- Sampling: nearest for pixel sprites / atlases; add tile padding or extruded edges to prevent texture bleeding.
- Crisp pixel silhouettes, readable contrast, a clear silhouette before any lighting/shader treatment. Do not mistake procedural noise for quality art.
- Art and simulation must remain separate. A terrain tile is **data**; a sprite/variant/transition is its **presentation**. Visual randomness is seeded deterministically.

## Terrain palette (starting reference, refine by screenshot review)

| Terrain | Base color | Shape and detail target |
|---|---|---|
| Deep ocean | `#183E75` | broad darker blue shapes, quiet wave bands |
| Shallow water | `#3278AB` | soft clusters, ripple highlights and foam at coast |
| Sand | `#D7C17D` | granular clusters, small drift textures |
| Grass | `#6AAB55` | varied grass tufts, patches, subtle color bands |
| Forest | `#367B43` | distinctive tree crowns/trunks; isolated and connected trees |
| Mountain | `#78828C` | angular facets, ridge texture and sharp silhouette |
| Burned | `#503E35` | charred patches and ash border |

Use accent shading/highlights of each biome rather than uniform single-color fills. Minimum recommended 3 tonal steps per biome. Allow palette themes later; no mandatory day/night logic in M1.

## Asset tiers

1. **M1**: procedural texture plus hand-authored, original micro-details; terrain/shoreline transition masks; consistent UI hierarchy.
2. **M2**: coherent authored atlas and micro-decorations; physically intuitive water/land transition shapes; atlas padding and layering.
3. **M3**: distinct 4-way unit sprites with walk/idle feedback; portraits/icons only when useful.
4. **M4+**: meteor impacts, smoke, fire, ambience, UI effects, civilizations.

Prefer small independent atlases over one huge texture and keep variation within resource budget. Initial atlas cell art target: 16×16 or 24×24 source pixels with world/render scale defined separately; **do not directly change map resolution** without measuring consequences.

## Scene composition order

1. Deep/shallow water
2. Ground, slopes and transition overlays
3. Vegetation/decorations (behind actor)
4. Citizen and entity sprites, selection feedback
5. Front-layer canopy/structures
6. Weather, impacts, particles
7. UI/HUD (CanvasLayer)

No effect should make terrain class unreadable, and UI must not obscure the central play area.

## UI/UX baseline

- 1280×800 reference viewport; verify at 1920×1080.
- Toolbar within a consistent top inset; grouped tools and separate simulation controls.
- Default readable type at native screen resolution (target ~16–20 px), touch targets approximately ≥40 logical pixels.
- Active brush clearly indicated; density/radius visible; no uncontrolled overlap or clipped labels.
- World painting must not occur when clicking HUD.

## Animation and accessibility

- Water animation low-amplitude and tile-coherent; avoid all-screen blinking/sparkles.
- Provide reduced-motion/static option before broad VFX rollout.
- No reliance on color alone for tool selection; clear text/shape/outline cues.
- Keep screen-shake modest and toggleable when introduced.

## Quality gate

Screenshots must be captured **from the actual game running** at seed 20261030 on the Windows host, at 1280×800 and 1920×1080 (or documented available size); do not pass based solely on code review. Compare visual clarity, silhouette, UI legibility, land/water separation and edge consistency. Performance values are measured, never guessed.
