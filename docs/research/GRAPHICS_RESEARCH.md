# GRAPHICS_RESEARCH — a production visual direction, not primitive 3D placeholders

**Goal**: visually rich miniature/living-world 3D presentation, physically understandable terrain and terrain edits, attractive forests and mountains, cohesive art direction, accessible on the user's AMD Radeon R9 M395X Compatibility GPU (which is not yet benchmarked). **User rejected flat 2D/pixel style** and the current V2 spheres/cylinders.

## Why the present V2 3D still looks low-quality

1. **Shape language:** `VisualTerrain3D.HeightAt:104–119` gives whole tiles a handful of categorical heights. `BuildChunk:138–187` creates horizontal flat squares with vertical cliff walls, without continuous ridge curves, strata, slopes, erosion or sculpting. This creates artificial platforms, not convincing terrain.
2. **Asset quality:** default trees use 1 cylinder and 2 low-poly spherical crowns; rocks use ellipsoids (`:233–285`). These are **debug geometry**, not final tree silhouettes. A better shader cannot turn a 7-segment sphere into convincing foliage.
3. **Environment composition:** one large flat sea plane, simple sinusoids/color (`Assets/Shaders/stylized_water.gdshader:1–21`), no gradient/masks integrated with terrain depth, foam at limited two cardinal sides only (`VisualTerrain3D:172–182`), no cliffs/beaches blending.
4. **Lighting:** one sun and ambient color (`VisualLab3D.cs:75–99`); no validated exposure or color grading profile, terrain AO or readable canopy shadows on R9 M395X. We should tune physical composition before adding expensive post-effects.
5. **Camera/scale:** the default `Camera3D.Size=79` and high angled distance reduce the readability of small details. Need at least three art evaluations: regional overview, village distance and close-up.
6. **Cohesion:** optional GLB import (only a tree and rock) plus primitive filler mixes incompatible material, silhouette and resolution families. Art style cannot be judged as professional until a consistent licensed authored asset set and material rules are used.

The recommendation is an **art-directed vertical slice**: one 64×64-cell biome segment with an attractive forest, mountain/cliff and beach, and 2–3 coherent authored nature species, **before** attempting a giant 3D map.

## Style comparison, including the user constraint

| Option | Pros | Cons | Use |
|---|---|---|---|
| High-res 2D painted atlas | Sharp art on old GPU | Explicitly rejected pure 2D; expensive asset angles | Do not select |
| Hybrid isometric 2D billboards + 3D ground | Good performance/distant LOD | Sprite/view changes, shadow consistency; lower close-up freedom | Use for far view only |
| **Art-directed stylized true 3D** | Actual depth, believable trees/buildings/shadows and zoom/tilt | Requires proper sculpted terrain, real assets, batching and LOD | **Primary** |
| Photorealistic PBR cinematic 3D | Potentially highly impressive | Large art budget and GPU costs, old GPU uncertain | Optional modern-GPU preset only |

### Visual reference analysis (inspiration, not source art)

- [Preserve](https://store.steampowered.com/app/2109270/Preserve/): dense tree silhouette diversity, flowers/ground micro-variation, restrained color cohesion and miniature elevation. The key is **environment assembly and authored forms**, not a sphere-and-cylinder shader.
- [Townscaper](https://store.steampowered.com/app/1291340/Townscaper/): coherent architecture palette, hand-crafted waterfront silhouettes, readable small buildings, calm stylized water. Not necessarily the right scale or gameplay model.
- **Production goal**: retain world-map readability at strategic zoom while providing richer near view. No intellectual property copied from game screenshots, assets or branding.

## Recommended material and asset language

**Terrain:** continuous 16-bit heightfield *or* a hybrid heightfield/cliff mesh (depending on terrain edit semantics), 3–5 blended material layers (soil/grass/sand/rock/snow later), macro color variation and vertex-painted splat weights. Cross-biome blends and shore normals must match visible elevation; tile classes become **simulation labels**, not the entire visual description. Low-cost Compatibility path: baked color ramps, few texture samples, limited dynamic lights and shadows. Fine path: richer normal maps, decals where supported, volumetric/post-processing only in renderer capable of them.

**Trees:** 3–6 coherent species for first slice, nonuniform crowns with hand-designed leaves and trunks, meaningful silhouette variation (not purely random scale), stable base pivots, 3 LOD tiers, matching color palette, single or few materials per species, shared wind shader optional. On old GPU cap shadows to near foliage, disable at distance, prefer alpha-scissor or solid foliage over expensive sorted transparent billboards.

**Rocks/cliffs:** layered meshes or sculpted ridge field plus selected rock modules, rotated/scaled to fit slope. Tessellation/hardware-heavy procedural cliffs are not a baseline requirement.

**Water:** low-frequency coherent motion plus shore intersection based on a depth/shoreline map, foam masks and reflections appropriate to renderer. No compulsory screen-space reflections; Compatibility path should be fast and attractive by design.

**Ambient:** limited directional light, baked or hand-authored contact shading where needed, subtle mist gradients if renderer supports cheaply. Do not set a false requirement for volumetric fog/SSR/SDFGI on Compatibility.

**Camera:** evaluate at fixed seed, consistent 30–50° pitch, 45° yaw (tunable), screen sizes, exposure and viewpoints; the same assets must remain readable at village and strategic views.

## Art-production and source provenance

| Candidate | Publisher evidence | Formats / rights | Strength / caveat |
|---|---|---|---|
| [Quaternius Stylized Nature MegaKit](https://quaternius.com/packs/stylizednaturemegakit.html) | 116 models, 40 trees, 35 plants/flowers, 27 rocks, textured and glTF/FBX/OBJ/Blend, CC0 per publisher | CC0; official glTF (may be .gltf + textures, not necessarily standalone .glb) | **First choice for coherent near-view nature**. Free *Standard* offers a portion of models; paid *Source* includes engine-ready shaders/projects. Do not claim full authored Godot package is free |
| [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) | Secondary catalog confirms ~330 CC0 models, glTF/FBX/Blend; official page inaccessible in current web environment | Reverify exact downloadable ZIP/license with publisher before import | Good low-poly distant/placeholder LOD; less detail, different scale/style. Not interchangeable with Quaternius close-up |
| Blender-authored originals | Our own art, local H: workflow | Original art; maintain source and licenses for dependencies | Required for cohesive terrain/cliffs, unique settlements, biome accents |
| Commercial Unity/Unreal-only nature packs | Vary by listing | Different license / shader dependencies | Only evaluate with verified Godot-compatible export and commercial redistribution terms |

**Important importer issue:** `scripts/import-nature-pack.ps1` only recognizes `*.glb` and picks one tree/rock by file name; the official Quaternius pack lists **glTF** but does not guarantee that the free Standard archive contains self-contained `.glb`. Thus the current import path may fail on a legal pack. Inspect archive first; implement glTF + external textures/buffers with dependency-preserving import or convert on H: using a reproducible Blender export script. Never rename `.gltf` to `.glb`.

Asset metadata: source URL, download version, content SHA256, per-file original path, exact license text, modifications and export settings; keep third-party pack ZIP and model cache on H:. Do not copy copyrighted reference game art.

## Renderer technology and engine comparison

**Godot Compatibility** (current, verified user renderer): OpenGL, capable of real 3D lighting and meshes but lower precision/post effects. No Forward+-only SDFGI, SSIL, SSR, volumetric fog or decals. Supports stylized art if models/materials are good. Source: [Godot renderers table](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html).

**Godot Mobile/Forward+**: available on RenderingDevice platforms (Vulkan/D3D12/Metal); richer effects. Do **not** assume R9 M395X supports these features without a safe H:-based launch test. A renderer change is a *graphics tier*, not gameplay architecture rewrite. Shader feature matrix must be tested.

**Unity URP**: has a mature visual artist ecosystem and supports DirectX 11/OpenGLCore among other APIs ([Unity URP compatibility](https://docs.unity.com/en-us/engine/6000.7/manual/render-pipelines/universal-render-pipeline/requirements)). An engine migration would discard Godot scenes/input/render code but could reuse pure C# WorldSim with adaptation. Test a one-scene quality/perf bake-off only if Godot cannot meet visual goals despite actual authored assets.

**Unreal Engine 5**: rich visual pipeline but much heavier target for current hardware and C# core integration. Epic lists recommended 32GB RAM, >=8GB GPU VRAM and DirectX 12-class GPU for the editor ([UE5 hardware](https://dev.epicgames.com/documentation/unreal-engine/hardware-and-software-specifications-for-unreal-engine)). Not the baseline recommendation for this machine.

## Asset pipeline proposal (original + vetted licensed)

```text
Publisher/Blender source -> H: secure asset staging + license manifest
 -> validate dimensions/units, pivot, UV, normals, material count, tri count
 -> convert/export GLB on H: (preserve textures)
 -> import Godot (source files retained)
 -> unified material palette + texture budgets
 -> create near/mid/far LOD + impostor in asset-build step
 -> batch by chunk/material/LOD
 -> fixed-camera screenshot + benchmark
```

For the first vertical slice, deliver **one curated near-view scene** with at least 2 tree species, 1 rock cluster, 2 ground material blends, coherent water edge and one warm light. Compare with the same seed/camera before changing the default runtime. Quality must be accepted by the user; code compilation is not art approval.

## Review checklist

- [ ] Reference collage / three distance screenshots with explicitly labeled inspirational sources, no copied art.
- [ ] Quaternius Standard contents and source texture dependencies actually inspected, exact license recorded.
- [ ] Terrain and cliff continuity validated visually.
- [ ] Model scale/axis/pivot and palette unified (no giant/small mixed trees).
- [ ] Compatibility shader compilation and actual AMD FPS/VRAM recorded.
- [ ] At least low/high graphics tiers differ in **detail/LOD/shadow distance**, not world simulation semantics.
- [ ] Artists/technical art can swap model sets without editing `WorldSim`.
- [ ] High-end optional path can add improved lighting without blocking old-GPU tier.

**Source references:** [Godot renderer feature matrix](https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html), [Godot 3D LOD and culling](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html), [Quaternius official listing](https://quaternius.com/packs/stylizednaturemegakit.html), [Quaternius Standard Godot store note](https://store.godotengine.org/asset/quaternius/stylized-nature-megakit/), [Kenney secondary catalog](https://tmhsdigital.github.io/Free-Game-Dev-Assets/entry/kenney-nature-kit/).
