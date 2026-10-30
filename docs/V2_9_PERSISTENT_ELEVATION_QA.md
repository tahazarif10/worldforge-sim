# V2.9 — Editable and persistent regional terrain elevation (first integrated slice)

**Date:** 2026-10-30. Issue #41. **No OpenAI Work, Codex CLI, Codex Cloud or agent jobs were used.** Changes were made using the directly connected GitHub integration and GitHub Actions.

## Why this step?

The current Godot 3D visuals have continuous terrain corners, good Kenney CC0 vegetation and low-cost opaque shoreline water. However, heights are still derived from biome categories in WorldState/VisualTerrain3D. Painting mountains changes biome and the rendered shape but does **not** create an independently editable, persistent height layer.

This additive prototype introduces genuine height editing **without changing the approved existing terrain by default**. A sparse signed elevation delta is added to shared global terrain lattice vertices. Neighboring 16x16 render chunks use the **same absolute vertex** and exact ray picking, so an edit along x=16/32 cannot split adjacent geometry.

## Exact implementation

- `src/WorldSim/Regions/ElevationRegionStore.cs`: signed int64 absolute vertex keys, 64x64 regions, signed 16-bit height **deltas quantized at 1/256 world unit**. +/-8 world-unit edit limit per vertex. 12–16 resident region LRU, sparse per-edited-vertex overrides, region revisions, immutable copy-owning snapshots; unmodified regions are never allocated. Separate from WorldState biome data.
- `ElevationRegionFiles.cs`: versioned little-endian fixed-size snapshots with FNV-1a checksum (~8.2 KB per edited region). Explicit user-triggered save uses a temporary file in the **same destination folder**, a flush, then overwrite-rename; loading validates **all region files and world seed before mutating memory**. A malformed file leaves current in-memory world edits untouched. This is *not* a crash-proof multi-region journal/transaction, disk backup or cloud sync.
- `VisualTerrain3D.cs`: uses sparse offsets in the existing `SharedCornerHeightfield.Fan` (four corners plus their center average). It does **not** allocate extra triangles, create per-tile Godot nodes, change water shaders or Kenney nature. Painting height invalidates only nearby 16x16 visual chunks plus a safety halo; forests/people re-ground from the same exact interpolated surface.
- `VisualLab3D.cs`: opt-in sculpt mode **F7**: left mouse drag raises the terrain; hold **Shift+left drag** to lower it. Existing 2D/3D tools remain unchanged when F7 is off. **F11** explicitly saves edited height regions to `H:\WorldForge-Sim\artifacts\worlds\seed_<actual-seed>\elevation\`; **F12** explicitly reloads the same saved data after validation. A new world gets a new seed and a fresh edit store. **F6** still restores the old stepped 3D look (edits become visually inactive until smooth terrain is re-enabled), F4 Eco and all earlier hotkeys remain.
- `tests/ElevationRegion.Smoke`: signed/negative coordinates, four-way region edit, quantization/limits, no allocation on untouched reads, bounded resident region LRU, immutable snapshots, corruption/truncation/wrong-seed refusal, save/load on disk and huge-sparse coordinate tests. Uses `artifacts/test-elevation` within the working project, **not Windows C: TEMP**.

## Low-end hardware budget

- World visuals remain 120x88 in VisualLab; there is **no entire 8192² mesh**, world-height atlas or additional water/draw pass.
- Each render land cell keeps its existing four top-face triangles. Edited heights modify positions of those already existing vertices only.
- Most frames do not perform any height file I/O. Saving/loading occurs only after F11/F12. Even height reads for unedited regions allocate **zero** region arrays.
- Brush updates touch only an O(radius²) set of vertices and local visual chunks, not the entire visual world.
- The actual chunk builds are still CPU/main-thread work, limited in the prior V2.6 Eco/Balanced path. Continuous brush strokes may still produce frame-time spikes on older GPUs and CPUs: test before claiming 30/60 FPS.

## H-only Windows verification

Close running Godot Editor and game first. **Preserve local `project.godot` and do not force-reset Git working tree**.

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-9-persistent-elevation-regions
.\scripts\verify-v2-art.ps1
```

After this PR is merged, use `git switch main; git pull --ff-only origin main` instead of the feature branch.

### Test scenario

1. Start preview, **F7** ON. Zoom to a meadow/forest and drag left mouse: land raises subtly. Hold Shift and drag: height decreases. Trees and citizens should remain grounded; water/shore/biome colors should not disappear.
2. Edit across a 16-cell chunk boundary, pan away and back; no seam, missing batch or stale vegetation.
3. Press **F11**; observe saved `.wfh` region files under H:\WorldForge-Sim\artifacts\worlds\seed_<seed>\elevation. Press **F12** to reload. An invalid/corrupt file must log a refusal and leave the current in-memory edits untouched.
4. Press **F6** for old terraced land and again to return to continuous heightfield. The edit is not deleted by the toggle. Press **F7** OFF to return to standard paint/people tools.
5. Compare Balanced and Eco with **F4**. Take **F8** screenshots and **F9** p95/p99 frame interval samples at 1280x800 for 60 seconds camera movement, including 20 seconds of sculpting. Use F10 active chunk debug output.
6. Verify old citizen spawn, meteor, water, shadow and camera controls still behave as before.

## Scope boundary and next steps

The new region files store **only elevation offsets**, not terrain types, living-world simulation, full game saves or independently editable absolute 3D geology. The existing `WorldState` remains whole-array and hard-limited to 1024 cells per dimension. `ElevationRegionStore` supports sparse int64 world coordinates, but **that does not make 8192x8192 worlds playable yet**.

The next scalable milestone is integrating the WorldSim region store as the sole source of biomes/entities and chunk-versioned streaming, followed by read-only background CPU mesh work and bounded main-thread uploads. A multi-region atomic journal and backup format are required for production save guarantees.

## Automated QA

- Existing simulation, camera, terrain/picking, water, chunk budget and nature asset tests must stay green.
- `dotnet run --project tests/ElevationRegion.Smoke/ElevationRegion.Smoke.csproj -c Release` must pass.
- Official Godot 4.7.2 Mono Linux headless workflow imports Kenney GLBs, builds C#, and starts the opt-in alternate 3D scene. This **does not certify Windows AMD R9 M395X FPS**; real F8/F9 artifacts are still required for that claim.

Sources: [Godot procedural meshes](https://docs.godotengine.org/en/latest/tutorials/3d/procedural_geometry/index.html), [.NET same-folder File.Move overwrite](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0).
