# CURRENT_STATE_AUDIT — WorldForge Sim

**Audit date:** 2026-10-30. **Evidence:** remote GitHub \`main\` at merge commit \`9d5ba51017a1b5c0ed633f66eee64eea33f346f5\` (PR #18). **Scope:** actual tracked source; no direct access to the user's Windows H: host, GPU profiler, screenshots of the latest V2 build or locally modified \`project.godot\`.

## Executive finding

The main failure is **not merely an ugly texture**. The V2 preview is a tiny synchronous, whole-world prototype with a camera whose screen/world motion is not geometrically correct for an elevated view. Preserving this structure while adding high-detail art will make both interaction and scalability worse. Keep \`WorldSim\` as an independent deterministic reference, retain the 2D main scene as rollback, **replace** the V2 camera and presentation paths incrementally, and introduce chunk-owned data/resources before increasing world size.

### Source and PR map

- \`Scenes/Main.tscn:1–6\` is the original 2D default. \`project.godot:4–20\` still selects that scene and OpenGL Compatibility at 1280×800.
- \`Scenes/VisualLab3D.tscn:1–6\` is opt-in 3D through \`scripts/run-visual-lab.ps1:18–27\`; this script runs the portable H:-installed Godot with an explicit scene argument and H:-based log.
- \`Scripts/GameRoot.cs\` + \`Scripts/Rendering/TerrainRenderer.cs\`, \`EntityRenderer.cs\`, \`TerrainPixelArt.cs\`: the 2D reference pipeline, **not** the actual V2 renderer. M1.4 output was rejected by the user.
- \`Scripts/VisualLab/VisualLab3D.cs\`: world orchestration, camera, user input, actors and 3D HUD all in one 348-line class.
- \`Scripts/VisualLab/VisualTerrain3D.cs\`: terrain chunks, full-world vegetation, water/shader, optional imported GLB assets.
- \`src/WorldSim/WorldState.cs\`: Godot-independent, deterministic seeded terrain, basic walking citizens and brush mutations.
- \`Scripts/UI/GameHud.cs\`: shared Control-based toolbar, button callbacks and per-second FPS/worst-delta label.
- \`Assets/Shaders/stylized_water.gdshader:1–21\`: shallow animated color ripples with no depth-aware shore foam, reflection or physical waves.
- \`.github/workflows/core-ci.yml:1–25\`: WorldSim / CameraMath / DirtyTileRegion / TerrainPixelArt smoke checks and C# compile; **no 3D launch, shader import, performance, movement feel or screenshots**.
- PR chronology (merged): #1 foundational simulation, #10 art direction, #11 HUD, #13 diagnostics, #14 2D camera, #15 separated 2D rendering, #16 flat procedural terrain, #18 experimental 3D. Open issues include #12 (Godot **editor** hang), #17 (3D redesign) and #19 (this research).

## Architecture: actual, not aspirational

\`\`\`mermaid
flowchart TD
  G[project.godot] --> MAIN[Main.tscn / GameRoot 2D]
  CLI[run-visual-lab.ps1] --> LAB[VisualLab3D.tscn]
  MAIN --> WS[WorldState: whole terrain array + List Citizen]
  LAB --> WS
  LAB --> CAM[Camera3D + ad hoc drag/zoom/picking]
  LAB --> AR[UpdateActors: new MultiMesh per tick]
  LAB --> VT[VisualTerrain3D]
  VT --> CHUNK[16x16 mesh chunks, all created]
  VT --> VEG[RebuildVegetation: entire world]
  VT --> SEA[PlaneMesh + water shader]
  MAIN --> TWO[2D TerrainRenderer / EntityRenderer]
  MAIN --> HUD[GameHud]
  LAB --> HUD
\`\`\`

The 2D \`CameraMath\` and its tests do not validate the 3D camera. WorldSim currently contains no 3D elevation or chunk streaming primitives.

## Component review and decision

| Component | What works | Evidence | Weakness / severity | Decision |
|---|---|---|---|---|
| WorldSim | Seeded independent C# generation, simple tools; tests exist | \`WorldState.cs:16–44,46–90,107–155\` | Max axis 1024; full \`byte[]\` and whole-map Generate; \`List<Citizen>\` scan each tick; population limit 600 default/450 V2. **Critical for future scale** | Retain interfaces/tests; introduce chunk-based V3 world store + migration adaptor |
| Camera V2 | Functional ortho Camera3D, zoom/keyboard/drag | \`VisualLab3D.cs:59–73,218–308\` | Incorrect screen-to-ground drag basis, sea-plane zoom anchor, simplistic bounds and range tied to zoom. **Critical UX** | Replace with tested pivot-ground projection architecture |
| Terrain V2 | Actual geometry, cliff sides, chunk build | \`VisualTerrain3D.cs:15,71–75,121–201\` | **Every** chunk built initially; 16x16 tiles, tier-height steps, vertex colors, few features; no chunk visibility/LOD/streaming. **High/critical** | Reuse concept; replace mesh generator with terrain/elevation/material pipeline |
| Editing V2 | Paint, meteor, spawn routed into WorldSim | \`VisualLab3D.cs:310–347\` | Per-input raymarch \`for t<420 step=.22\` ~1,909 samples worst case. Approximate height detection, possible miss/high-altitude issues. **High** | Replace with indexed ray-terrain intersection and brush transaction |
| Vegetation V2 | MultiMesh primitive stems/canopies, optional GLBs | \`VisualTerrain3D.cs:203–329\` | Whole-world loop and \`QueueFree\` recreation after **each** edit; optional 140+80 individual model nodes with no LOD. **Critical future scale** | Per-visible-chunk vegetation cache/instance batches, stable asset library and LOD |
| Citizens V2 | Same WorldSim People rendered in 3D | \`VisualLab3D.cs:115–137,151–162\` | New SphereMesh + MultiMesh each tick and after brush; no persistent instance buffers/interpolation or spatial indexing. **High** | Persistent per-region entity presentation with changed IDs/transforms only |
| Water | No advanced GPU dependencies; moves | \`stylized_water.gdshader:1–21\` | Single sea plane and sine/color bands; not shoreline-aware; limited material variation. **Medium visual** | New material after terrain data/shoreline masks are ready |
| HUD | Existing usable toolbar, input consuming | \`GameHud.cs:32–124\` | Text-heavy; fixed widths, screen coverage, no scale-aware camera instructions/quality menu. **Medium** | Refactor later, preserve behavior now |
| Assets | Opt-in CC0 GLB pipeline | \`import-nature-pack.ps1:1–85\` | Only chooses 1 tree/1 rock by filename; no style bake, unified bounds, texture QA or scalable instancing; no assets bundled. **High visual** | Add separate licensed art ingestion/validation pipeline |
| CI/diagnostics | Compilation and deterministic smoke suites | \`core-ci.yml:1–25\` | Does not start game or compile shader scene at runtime; no GPU metrics or motion verification | Add isolated headless scene import and Windows benchmark evidence; don't claim GPU QA from Linux |
| Persistence | Deterministic initial seed | \`WorldState.cs\` | No save/load, edits journal, chunk versions or world streaming | Design versioned region storage before huge maps |

## Concrete confirmed risks, ordered by impact

**Critical:**
1. **Wrong drag mapping**: \`VisualLab3D.cs:235–241,274–281\` takes \`Camera3D\` camera-space X/Y vectors then zeroes their Y components. The projection of screen-up onto ground shrinks by a tilt-dependent factor; horizontal and vertical drag cannot have equal expected screen sensitivity. This explains a likely contributor to reported feel, but exact device symptoms need validation.
2. **Whole-world vegetation rebuild**: \`VisualTerrain3D.cs:79–102,203–286\`. Every terrain brush flags vegetation dirty, then scans all \`Width×Height\` and frees/creates instancing nodes. The work is **O(world cells)** instead of O(changed visible chunks). Deterministically a scaling blocker.
3. **Whole-world WorldSim**: \`WorldState.cs:23–34,69–90\` imposes 1024×1024 per-axis limit and scans all citizens every tick; expanding the map requires a new storage/update architecture.
4. **No real terrain LOD or streaming**: \`VisualTerrain3D.cs:71–75,121–188\` constructs all terrain chunks on each world creation; MultiMesh is not per-instance-cullable. Memory and initialization cost increase with world area.
5. **Non-production visuals**: \`HeightAt\` chooses terrain-class-dependent constant elevations (\`:104–119\`); \`BuildChunk\` uses a few flat top faces / vertical walls; tree crowns are spheres (\`:277–285\`). Authored high-quality models, smooth terrain silhouettes, materials, consistent art direction and refined shadows are absent.

**High:**
6. **Zoom anchor at constant sea height**: \`VisualLab3D.cs:260–308\`. Cursor anchor necessarily drifts when pointing at elevated forest, cliffs or mountains; works only on the sea reference plane.
7. **Brush raymarch**: \`VisualLab3D.cs:315–330\` performs up to ~1,909 position samples per pointer event; misses can occur due to step length / column geometry / first-hit logic. This is an algorithmic concern; CPU cost must be measured.
8. **UpdateActors allocation**: \`VisualLab3D.cs:115–137,151–162\` creates a Mesh and MultiMesh per simulation tick; repeated managed/native resource churn and possible garbage collection spikes.
9. **Chunk boundary**: current mesh faces only emit when a neighbor is lower; seams and mesh AABB/culling must be examined in actual scene; no mesh-stitch/LOD policy. Avoid claiming visible seam artifacts until captured.
10. **Asset import**: adding individually instantiated \`PackedScene\` GLBs before proper chunk batching is a temporary bridge, not viable for forest-scale data.

## Evidence levels

- **Verified from code**: implementation and complexity/allocations/size caps mentioned above.
- **Verified from CI history**: merged PR #18 C# build and existing smoke suites passed. Does **not** imply shader compilation, scene import or FPS target success.
- **User-observed**: the V2 visual lab is an improvement over 2D, but remains below aesthetic target; navigation is judged very poor. Previous Godot *editor* hangs were explicitly distinguished from the standalone game.
- **Not measured**: FPS p95/p99, actual draw calls, GPU frame timing, memory residency, asset import fidelity, input latency, VRAM budget and the cause of editor hangs.

## Immediate retention/rollback policy

- **Retain**: world seed, deterministic smoke tests, existing M0 tools, 2D main scene, H:-portable scripts, C# separation and usable HUD.
- **Refactor**: V2 Camera controller, brush picking, current terrain chunk builder, actor presentation.
- **Replace**: global vegetation rebuild, whole-world visualization, primitive tree art as production output, flat constant-class heights, monolithic terrain store for future huge worlds.
- **Never delete or overwrite** user's local \`project.godot\`, assets or H:-based caches without explicit data review. Research PR changes **documents only**.

## References

- Godot official [MultiMesh instancing limitation](https://docs.godotengine.org/en/latest/tutorials/performance/using_multimesh.html).
- Godot official [LOD/culling and 3D optimization](https://docs.godotengine.org/en/latest/tutorials/performance/optimizing_3d_performance.html).
- Godot official [3D ray picking](https://docs.godotengine.org/en/latest/tutorials/physics/ray-casting.html).
