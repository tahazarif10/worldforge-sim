# FIRST_VERTICAL_SLICE — the smallest proof that camera + real graphics + regions work

**Working title:** "Coastal Forest Diorama"  
**Type:** opt-in alternate Godot scene, not a destructive main-scene replacement.  
**Purpose:** demonstrate a compelling, artist-quality 3D environment, accurate navigation/editing and region-bounded updates with measured performance on **AMD Radeon R9 M395X** before building the giant-world renderer.

## Hypothesis and acceptance

> With a correct screen-ray camera, coherent authored nature assets and a 4-region chunked world, WorldForge Sim can look materially more professional and feel significantly better than the primitive V2 VisualLab, without losing editability on the reference GPU.

This is **unproven** until the user reviews a real Windows build and measured frame times.

### Demonstration content

- Logical world: **128×128** cells, implemented as **four 64×64 WorldStore regions** (2×2); render geometry using 32×32 pages (16 near chunks), versioned dirty updates.
- Biomes: deep/shallow water, sandy beach, flower/grassland, dense 3D forest, layered rock/cliff mountain; continuous height values separate from biome labels.
- Art: coherent curated Quaternius CC0 samples or original assets (at least 2 distinct detailed tree silhouettes, variation within species, shrubs/flowers and real rock modules). **No procedural SphereMesh crowns as user-visible final art**.
- Terrain: height/ground blending, beach-to-water shore mask, rock layering, no 1-tile height class wall everywhere.
- Water: low-cost foam/wave designed for Compatibility; no mandatory Forward+ SSR/volumetric fog.
- Lighting: one sun, bounded shadow distance and a cohesive warm/cool palette; expose F2 shadow A/B for benchmark.
- Human markers: keep current WorldSim person spawns/meteor as a functional test, use simple but readable human proxy if no authored character pack is ready.
- Controls: right- or middle-button terrain grab, wheel anchor at **actual terrain height**, WASD normalized screen pan, separate brush painting; optional orientation controls only if stable.
- UI: existing GameHud with additional camera hint, region/LOD/p99 profiling overlay.

## Scene and package boundaries

New files, not overwrites of existing defaults:

```text
Scenes/CoastalForestSlice.tscn
Scripts/Camera3D/CameraPose.cs
Scripts/Camera3D/StrategyCameraRig.cs
Scripts/Camera3D/TerrainPicker.cs
src/WorldSim/Regions/*            (new backing/adapter, no deletion of old WorldState)
Scripts/Rendering3D/WorldRegionRenderer.cs
Scripts/Rendering3D/ChunkMeshBuilder.cs
Scripts/Rendering3D/ChunkVegetationRenderer.cs
Scripts/Instrumentation/FrameTimeProbe.cs
scripts/run-coastal-slice.ps1       (H: only)
docs/research/SLICE_QA_RESULTS.md (actual evidence; not synthesized)
tests/Camera3D.Smoke/*
tests/WorldRegion.Smoke/*
```

Optional imported CC0 asset binaries stay on H: with source/version/license manifest, validated before distribution. No magic downloading into C: and no claim the Standard ZIP contains self-contained GLBs until inspected.

## Vertical-slice milestones

| Mini-stage | Implementation | Success criterion |
|---|---|---|
| VS0 — Baseline lock | Capture existing V2 seed 20261030 at specific camera pose, old HUD FPS and CPU/RAM, review user-reported camera fail | Real source images/log path recorded |
| VS1 — Camera | Pure ray-plane drag math, real hit-based zoom and camera rig; wire into existing V2 preview | 100px drag projects within 2px expected, no zoom cursor drift at mountain, 20sec live user camera approval |
| VS2 — Region model | Four 64×64 regions, commands crossing region border, deterministic snapshots and invalidation | Paint spanning 4 regions updates all and reloads identically |
| VS3 — Author-quality art | Import/normalize 2+ tree types, rock group, ground/blend + coastline into a fixed 64×64 hero area | Three fixed-camera screenshots obviously different from primitive spheres, verified source/license |
| VS4 — Performance/scale | Split scene into 32×32 near mesh chunks, own vegetation instances by chunk and update affected region only | Brush update doesn't traverse/rebuild all 128×128; 512² data-only test works without huge mesh; p95/p99 captured |
| VS5 — Review | Interactive A/B (old V2 vs new slice) on same host, fixed resolution, FPS/shadow toggles, camera trial | User approves both *looks* and *navigation*, no critical runtime regression |

## Instrumentation and review setup

**Reference run:** seed 20261030, 1280×800 window, AMD R9 M395X with Godot 4.7.2 Compatibility/OpenGL, actual GPU/driver string in log, *no editor* (standalone). Compare:
1. **Overview**: coastline and forest cluster readable.
2. **Mid**: cliff/ground transitions and natural tree variety.
3. **Close**: bark/foliage silhouette/rock and material cohesion (not sphere/cylinder).
4. **Editing**: continuous forest and sea brush for 20 s across region boundary.
5. **Navigation**: 20 s of drag/zoom, including mountains; record positional anchors/subjective feel.
6. **Entities**: 100 active humans, 30 s, check movement/brush/pause.

Record frame-time p95/p99 and max via the proposed frame-ring instrumentation (not just one-second HUD FPS); process CPU/RAM via existing H:-only sampler; GPU draw calls and GPU memory **only if exposed by Godot/driver**, else N/A. Compare warmed scene, not cold shader/asset import. The user judges style against their requested quality; we cannot substitute code review for aesthetic approval.

### Camera concrete acceptance

- Using mouse drag to move terrain 100px horizontally/vertically preserves original grabbed object at new screen coordinate (<=2px error in open map).
- Zooming over mountain/shore leaves pointed surface within <=1 tile, and ideally <=1 screen pixel of original location (excluding world-bound constraints).
- Terrain painting under pointer at 1× / 2× / strategic zoom across region boundaries.
- Keyboard movement normalized against screen pixel speed and elapsed real time.
- No camera jump on changing yaw/pitch or after a long teleport into another loaded region.

These are initial QA goals; record measured errors and adjust with evidence.

### Huge-world extensibility acceptance

The slice is only 128², but must already prove:
- New world cells do not require one Godot node each.
- Terrain/props are per **visible** chunk, not all global; scene updates follow region revisions.
- A local brush invalidates at most touched chunks + seam halo (rather than O(world size) vegetation).
- Region 64×64 can be loaded/evicted and regenerated without changing game behavior; tiny cross-border paint test.
- Camera pose does not encode world location solely in one enormous Vector3; logic coordinates survive the demo.
- Switching to 512×512 data-only world consumes storage proportional to resident regions and does not instantiate millions of triangles.

Do not attempt 8192² visual world in this first slice; prove active-window architecture and then scale by verified steps.

## Acceptance artifacts and test matrix

| Artifact | Path | Required |
|---|---|---|
| Source branch / CI PR | `feat/v2-camera-ray-anchored` + `feat/v2-world-region-contract` + `feat/v2-authored-nature-slice` or small integration PR | Yes |
| Camera pure math unit logs | `artifacts/acceptance/camera-*.txt` (H: local) | Yes |
| World region determinism test | `tests/WorldRegion.Smoke` | Yes |
| Three real game screenshots per variant | `artifacts/visual-review/` (H: local) | Yes |
| Authored model license manifest | `Assets/Nature/SOURCE_NOTES.md` (H: local) | Yes |
| 30/60 FPS evidence and p95/p99 | `artifacts/performance/` (H: local) | Yes, measured |
| Error / rendering method log | `artifacts/visual-lab/` | Yes |
| User evaluation | GitHub PR or conversation | Yes |
| Unreal/Unity comparison | Only if Godot slice fails with specific evidence | Conditional |

## Risks to call out before implementation

- Quaternius `glTF` archive may not be `*.glb`; importer should preserve texture/buffer files or convert via H-based Blender. License text must be recorded.
- Complex glTF models with many surfaces/materials can erase instancing benefits; split/collapse materials or batch by mesh/material type.
- On Compatibility, excessive dynamic shadows can be expensive; tune cascade/shadowed instance radius and provide toggles.
- Different levels of detail may change perceived shape of coast or forests; use hysteresis and tests.
- Higher elevation/terrain editing requires an exact height/picking contract. Camera cannot be tested correctly against a sea-plane fallback.
- Windows editor hang (#12) is a separate blocker for *editor workflow*, not evidence of a runtime hang. Use stand-alone game to evaluate.

## Decision after demonstration

- **PASS:** high-quality visual screenshot, user satisfied with navigation, repeated GPU-host tests acceptable, region edits deterministic and correctly localized → proceed to V2.3 terrain system and V2.6 large-world streaming.
- **VISUAL FAIL:** revise art/materials without throwing away the verified camera/regions.
- **CAMERA FAIL:** revise projection/input math without rewriting world data.
- **PERFORMANCE FAIL:** profile shadows, model materials, MultiMesh spatial coverage, render chunk size and uploads; only then consider alternative engine proof.
- **UNMEASURED:** remain Draft, do not label PASS or force the new scene to become default.

**No changes to existing main scene or locally modified project.godot during this research task.**
