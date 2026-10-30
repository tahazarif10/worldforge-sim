# V2 foliage invalidation — performance isolation

Related: [Issue #25](https://github.com/tahazarif10/worldforge-sim/issues/25) and architecture research.

## Before

`VisualTerrain3D.MarkChanged` marked specific 16x16 terrain chunks dirty, but `FlushChanges` called `RebuildVegetation()`, which visited **every cell in the whole world** and discarded/recreated the full forest even when one cell changed.

## After (prototype only)

- Foliage grouped into `FoliageChunk_cx_cy` children, each independently rebuilt.
- Changed terrain chunks and foliage chunks share a deduplicated `HashSet` of keys.
- Each batch uses the same seeded terrain and per-cell colors; `LastPropCount` updates incrementally rather than scanning whole world.
- Optional authored GLB cap is **per chunk** (two trees and one rock per 16x16 chunk), rather than global import-order cap.
- `FoliageCellsVisited` and `FoliageChunksRebuilt` expose **operation counters**; they are not GPU frame-time numbers.
- One painted location inside a single 16x16 chunk (including halo boundaries) should now process only the one or neighboring edited chunks, **not** all `W×H` foliage cells.
- This does not solve the larger architecture: all foliage and terrain are still built at world creation, with no streaming, visibility/LOD or persistent region data yet. Chunking is a necessary *local invalidation* step only.

## Acceptance

- [ ] Godot C# compile in GitHub CI plus simulation smoke.
- [ ] Windows H:-based standalone scene test, same fixed seed.
- [ ] Same forest/rock distribution with fallback original procedural trees.
- [ ] Local brush changes foliage without orphaned old GLB nodes/ghost instances.
- [ ] Check `FoliageCellsVisited` delta for one brush: 256 multiplied by touched chunks, not 10,560.
- [ ] Actual frame-time and CPU measurements on R9 M395X before performance claims.
- [ ] No changes to original `project.godot` or `Scenes/Main.tscn`, no intentional C: files.

## Review command after branch checkout

```powershell
Set-Location 'H:\WorldForge-Sim'
git status --short
git fetch origin
git switch feat/v2-local-foliage-invalidation
. .\scripts\enter-dev.ps1
& 'H:\WorldForge-Sim\Tools\dotnet\dotnet.exe' build '.\WorldForgeSim.csproj' -c Debug --disable-build-servers -p:UseSharedCompilation=false
.\scripts\run-visual-lab.ps1
```

If Git refuses to switch due to local edits, do not use `git reset --hard` or forcibly overwrite `project.godot`.
