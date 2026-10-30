# V2 graphics research and decision — 2026-10-30

## Decision: 3D with orthographic/isometric presentation, not pure 2D pixel art

The previous M1.4 procedural 8x8 tile output was rejected by the user as visually poorer than the reference quality, and **pure 2D is not the product direction**. The selected approach uses **real 3D meshes, height variation, shadows, model assets, a directional light, stylized water, and an adjustable orthographic camera**. It aims for a premium diorama/simulation appearance, with original visual identity and no copying from WorldBox.

### Alternatives compared (qualitative)

| Rendering style | Strengths | Weaknesses | Verdict |
|---|---|---|---|
| Hand-drawn high-res 2D | Fine painterly detail / low 3D GPU cost | Pure flat visual; limited camera rotation, expensive sprite production | Rejected by user |
| 2.5D billboard sprites in isometric world | Strong silhouette; scalable LOD | Static angles, billboard overlap, hand-authored many views | Auxiliary LOD only |
| **True stylized 3D + orthographic camera** | Height, natural shadows, 3D trees/props, modular worlds and camera views; support for rich models | More renderer/asset work; strict batching and streaming needed | **Selected** |
| Photorealistic Unreal/Forward+ | Highest theoretical fidelity | Extreme assets/lighting cost; old R9 M395X unproven and likely costly | Not baseline; future optional tier |

### Hardware and renderer choice

Observed user's environment: Windows 11, Godot 4.7.2 .NET, AMD Radeon R9 M395X, Compatibility/OpenGL 3.3 renderer. **Do not assert Vulkan or Forward+ will work** without verifying on host. Compatibility runs real 3D mesh/material/directional lighting but lacks advanced HDR/postprocessing. The scene should rely on high-quality composition, meshes, material palette, and art, not expensive screen-space GI or Forward+ effects. Use one sun, batched props, and chunked terrain. Target 1280x800 initially; benchmark 1920x1080 when available. Performance target is aspirational until recorded.

### Selected art references (for style direction only — not reusable assets)

- Godus: sculpted coastline, raised layered earth.
- Preserve: richly varied forests and natural biomes.
- Islands & Trains: legible miniature buildings and terrain shapes.
All game asset art is to be created by us or obtained with valid licenses, never copied from screenshots.

### Recommended licensable asset candidates

1. **Quaternius Stylized Nature MegaKit** — original CC0 pack, 116 models (trees, plants, rocks) in glTF/FBX/OBJ; preferred cohesive nature quality. https://quaternius.com/packs/stylizednaturemegakit.html . Free kit may include only part of overall contents; source shader and Godot engine projects belong to paid Source tier. Confirm each imported file's provenance.
2. **Kenney Nature Kit** — 330 CC0 3D files, useful as lightweight fallback / mobile LOD; different triangle density and scale, so don't mix visibly without harmonizing style. https://kenney.nl/assets/nature-kit

These are *recommended sources*, **not currently downloaded or committed**. Asset import will be implemented later after checking user-host FPS of the 3D foundation. All downloads/install/cache/temp must land on H:, never intentionally on C:. No claim that procedural low-poly placeholder assets match the final target quality.

### Engine design

```text
WorldSim (existing pure C# deterministic 160x120 simulation)
     ├── Legacy 2D main scene (untouched fallback)
     └── VisualLab3D.tscn (independent experimental 3D scene)
            ├── 3D terrain mesh by chunk + vertex color
            ├── sea plane/material and foam highlights
            ├── batched MultiMesh trees/rocks/grass
            ├── batched actor meshes tied to WorldState citizen positions
            ├── orthographic elevated Camera3D + mouse ray-terrain picking
            ├── one directional sun + ambient environment
            └── existing UI GameHud on CanvasLayer
```

Each meaningful stage can be benchmarked against a fixed `Seed=20261030`, with camera zoom/window size fixed. Geometry is chunked and rebuilt only when terrain changes, not at every simulation tick. Actor instances can update independently. Runtime gameplay painting continues to use `WorldSim` commands. The old 2D game is preserved unchanged until a 3D design is **visually approved**.

### Mandatory acceptance gates

- New scene can be launched directly via `scripts/run-visual-lab.ps1` under `H:\WorldForge-Sim`; NO change to `project.godot` or default `Scenes/Main.tscn`.
- GitHub CI strict Godot C# compile and core smoke tests pass.
- Windows host: visible depth, distinct biomes, populated forest and mountain silhouette, shoreline/water visible; F5 stand-alone launch.
- Camera pan and zoom, painting/spawning/meteor controls where implemented; no crashes; collect real FPS/worst frame and screenshot from R9 M395X.
- If the visual experiment is worse than M1.4 or unstable, retain old 2D scene and iterate rather than merging it as new default.
- User approves final art direction and representative Quaternius assets *before* production switch.
- Long-term: replace placeholder procedural trees/rocks with selected original/licensed authored models, material/lighting review and chunked texture variation, not another cosmetic recolor.

## Research sources

- Godot 4.7 renderer selection and Compatibility: https://docs.godotengine.org/en/4.7/tutorials/rendering/renderers.html
- Godot 4.7 geometry generation and ArrayMesh C# API: https://docs.godotengine.org/en/4.7/tutorials/3d/procedural_geometry/arraymesh.html
- Godot 4.7 MultiMesh instancing: https://docs.godotengine.org/en/4.7/tutorials/performance/using_multimesh.html
- Godot 4.7 Camera3D picking and orthographic projection: https://docs.godotengine.org/en/4.7/classes/class_camera3d.html
- Quaternius official CC0 page: https://quaternius.com/packs/stylizednaturemegakit.html
- Kenney Nature Kit CC0: https://kenney.nl/assets/nature-kit
