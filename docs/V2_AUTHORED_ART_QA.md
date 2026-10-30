# V2 authored nature art — Windows visual acceptance

**Issue:** [#30](https://github.com/tahazarif10/worldforge-sim/issues/30)  
**Source:** 10 real Kenney Nature Kit CC0 glTF2 model binaries bundled at `Assets/Art/KenneyNature/`, not generated placeholders. Provenance: [asset README](../Assets/Art/KenneyNature/README.md).

## What will look different

The visible 3D visual lab now chooses among **six authored tree species**, two authored rock variants, grass and flowers by deterministic seed/cell. Packed GLB materials are retained. Each model is extracted into mesh parts and represented by per-visible-chunk `MultiMesh` instances, rather than hundreds of individual `PackedScene` nodes. Models use mesh-AABB-based height normalization; rotations and tree heights vary deterministically. The remaining fallback procedural meshes should only show when a GLB fails to load.

**This is the first included authored-art tier.** Kenney Nature Kit is a clean low-poly style and will be an improvement over spheres, but it is **not** an assertion of finished high-fidelity art. For the long-term premium direction, inspect and import higher-density coherent Quaternius Stylized Nature MegaKit models; avoid mixing art resolutions without planned LOD.

Coastline strips now cover all four cardinal sides rather than west and north only. `WorldSim` population rules and `project.godot` are unchanged. The existing 2D main scene remains available.

## How to test on the H: Windows workstation

**Close game and Godot Editor first.** Leave any `M project.godot` modification intact. Don't run `git reset --hard`, `git clean -fd` or force branch switch.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-authored-nature-art
.\scripts\verify-v2-art.ps1
```

The script imports the ten actual `.glb` files into the portable Godot cache via `--headless --editor --import` (on H:), runs NatureScatter smoke, compiles the C# Godot project and launches the **standalone 3D** scene.

**If switch fails due to uncommitted project.godot**, stop rather than forcing it. The tracked asset binaries are in GitHub, so there is no manual asset download.

## Manual review checklist

- [ ] The startup log contains `V2 authored Kenney nature models loaded: 10/10`. If fewer than 10, inspect `artifacts\visual-lab\godot-nature-import.log`; the renderer will fall back to primitive placeholders for missing types.
- [ ] Zoom close to forest; see distinct authored broadleaf, oak, pine, plateau, autumn trees, **not only spheres/cylinders**.
- [ ] Zoom mountain; see both authored boulder variants. Scan grassland for leaves and yellow flowers.
- [ ] Pan across chunk boundaries: no duplicate/ghost trees or missing patches. Compare with merged PR #29 with the same world seed, resolution, camera.
- [ ] Use Forest/Ocean/Mountain brush next to coast: edited vegetation persists after moving away/back; four-side coastal highlight updates.
- [ ] Check people, meteor, pause, new world and camera zoom/drag remain correct.
- [ ] Press F10 for active chunk counts. After 30 sec idle/20 sec panning/20 sec painting, press F9 to create `H:\WorldForge-Sim\artifacts\performance\v2-frames-*.csv`.
- [ ] Compare p95/p99 and worst frame with previous build on AMD Radeon R9 M395X. Use F2 (sun shadows) to test GPU cost.
- [ ] Take screenshot at overview and close zoom. User must assess art style; automated tests cannot prove aesthetic quality.

## Risks/limits

- Actual Godot GLB import, scene runtime appearance and Windows GPU performance are **NOT validated by Linux GitHub Actions**. CI validates GLB header bytes, original determinism and C# compilation.
- Current terrain remains flattened, elevation per biome cell rather than an artist-sculpted mesh. That still limits overall quality dramatically.
- Kenney authored low-poly nature art (~hundreds of triangles/tree) is a substantial step beyond primitive spheres but below textured Quaternius density. Production art pass needs better terrain/material blending, rock/cliff modules, texture/normal shading and curated tree species.
- F9 records frame **intervals** in `_Process`, not a true GPU timer.
- Long-term scale still requires `WorldSim.Regions` authoritative migration plus async chunk generation, not just camera-view residency.

## Production-next acceptance gate

Get actual screenshots + host FPS and selection feel; then improve one geographically coherent **64×64 hero coastal forest** with Quaternius or custom models, blended terrain and shoreline and compare before switching production default. Keep all project resources on H:.
