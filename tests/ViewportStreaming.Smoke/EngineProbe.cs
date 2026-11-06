using System;
using System.Collections.Generic;
using Godot;
using WorldForge.Camera3D;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.Tests;

/// <summary>CI-only real-camera oracle; never installed in the playable scene.</summary>
public partial class EngineProbe : Node
{
    public override async void _Ready()
    {
        try
        {
            Require(System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
                DisplayServer.GetName() == "headless", "probe is restricted to headless GitHub CI");
            var lab = GD.Load<PackedScene>("res://Scenes/VisualLab3D.tscn").Instantiate<VisualLab3D>();
            AddChild(lab);
            var terrain = lab.GetNode<VisualTerrain3D>("WorldAndFoliage");
            var camera = lab.GetNode<Godot.Camera3D>("IsometricCamera");
            bool requested = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--viewport-streaming");
            Require(terrain.ViewportStreamingEnabled == requested, "explicit opt-in selection");
            var configured=(StrategyCameraRig)typeof(VisualLab3D).GetField("_cameraRig",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(lab)!;
            Require(typeof(StrategyCameraRig).GetField("_surfaceFan",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(configured) is null,
                "ordinary and viewport-only play retain the scalar camera pick supplier");
            if (!requested)
            {
                Require(terrain.StreamingPlannerName == "legacy-v2" && terrain.ActiveTerrainChunks > 0 &&
                    terrain.ActiveFallbackChunks == 0,
                    "normal scene retains legacy planner and geometry");
                // Focused native check for the user's actual ordinary H:
                // gameplay path (NO viewport workers): painted terrain must
                // be visible BEFORE mouse release, without building foliage.
                lab.SetProcess(false);
                ExerciseLivePaintPreview(lab,terrain);
                GD.Print("PASS VIEWPORT DEFAULT OFF / " + terrain.TerrainBackendName);
                GetTree().Quit();
                return;
            }
            lab.SetProcess(false); // Test owns camera/cadence deterministically.
            var rig = new StrategyCameraRig(camera, terrain.HeightAt, 120, 88,
                terrain.ReliefAt, () => terrain.SmoothLandEnabled, terrain.SurfaceHeightAt);
            var poses = new[]
            {
                new CameraReviewPose(0,0,19), new CameraReviewPose(120,88,19),
                new CameraReviewPose(60,44,79), new CameraReviewPose(0,88,160),
                new CameraReviewPose(120,0,160), new CameraReviewPose(60,44,19)
            };
            // Exercise actual viewport aspect changes, not letterboxed canvas scaling.
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            long samples = 0;
            bool sawPending = false;
            var viewports = new HashSet<Vector2>();
            foreach (Vector2I resolution in new[] { new Vector2I(1280,800), new Vector2I(640,960) })
            {
                GetWindow().Size = resolution;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                viewports.Add(camera.GetViewport().GetVisibleRect().Size);
                foreach (CameraReviewPose pose in poses)
                {
                    rig.RestoreReviewPose(pose);
                    terrain.UpdateVisibility(camera, 0);
                    ViewportStreamingDiagnostics before = terrain.StreamingDiagnostics;
                    Require(before.DesiredChunks <= before.Capacity &&
                        terrain.ActiveTerrainChunks <= before.DesiredChunks, "bounded actual residency");
                    sawPending |= before.MissingDetailedVisibleChunks > 0;
                    Require(before.MissingVisibleChunks == 0 && before.MissingVisibleCellUpperBound == 0,
                        "surface coverage exists with zero detail budget");
                    Require(before.FallbackChunks == before.MissingDetailedVisibleChunks &&
                        before.FallbackChunks <= before.VisibleChunks, "bounded fallback coverage");
                    InspectFallbackMeshes(terrain);
                    foreach (float height in new[] { -8f, .12f, 5f, 13f })
                    for (int z=0; z<88; z+=2)
                    for (int x=0; x<120; x+=2)
                    {
                        // Engine plane oracle is independent of planner edge clipping.
                        var point = new Vector3(x+.25f, height, z+.25f);
                        if (!camera.IsPositionInFrustum(point)) continue;
                        Require(terrain.IsViewportChunkRequested(x/16,z/16),
                            "engine-visible point absent from demand");
                        samples++;
                    }
                    int iterations = 0;
                    while (terrain.StreamingDiagnostics.PendingChunks > 0)
                    {
                        long builds = terrain.TotalTerrainChunkBuilds;
                        terrain.UpdateVisibility(camera, 2);
                        Require(terrain.TotalTerrainChunkBuilds-builds <= 2, "two-build update limit");
                        Require(++iterations <= before.DesiredChunks+1, "demand eventually drains");
                        Require(terrain.StreamingDiagnostics.MissingVisibleChunks == 0,
                            "replacement preserves continuous coverage");
                    }
                    Require(terrain.StreamingDiagnostics.MissingVisibleChunks == 0 &&
                        terrain.ActiveFallbackChunks == 0 &&
                        terrain.StreamingDiagnostics.MissingDetailedVisibleChunks == 0,
                        "ready detail replaces all proxies without overlap");
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
            Require(viewports.Count >= 2, "actual viewport resize was exercised");
            Require(samples > 1000 && sawPending, "real camera coverage and pending diagnostics exercised");
            rig.RestoreReviewPose(new CameraReviewPose(0,0,19));
            typeof(VisualLab3D).GetField("_visibilityRefresh",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(lab,0d);
            long detailBuildsBeforeFrame=terrain.TotalTerrainChunkBuilds;
            lab._Process(.001);
            Require(terrain.TotalTerrainChunkBuilds==detailBuildsBeforeFrame &&
                terrain.StreamingDiagnostics.MissingVisibleChunks==0,
                "sub-50ms game frame refreshes coverage without building detail");
            for(int z=0;z<88;z+=2)
            for(int x=0;x<120;x+=2)
                if(camera.IsPositionInFrustum(new Vector3(x+.25f,.12f,z+.25f)))
                    Require(terrain.IsViewportChunkRequested(x/16,z/16),
                        "actual game-frame demand follows the moved camera immediately");

            rig.RestoreReviewPose(new CameraReviewPose(60,44,19));
            terrain.UpdateVisibility(camera,64);
            Require(terrain.SculptElevation(60,44,2,8f)>0, "positive sculpt edit");
            Require(terrain.StreamingDiagnostics.PendingChunks>0, "edit invalidates resident generation");
            terrain.FlushChanges();
            Require(terrain.StreamingDiagnostics.PendingChunks==0, "synchronous edit acknowledges rebuilt generation");
            for(int z=0;z<88;z++)
            for(int x=0;x<120;x++)
            foreach(float u in new[]{0f,.5f,1f})
            {
                float h=terrain.SurfaceHeightAt(x,z,u,u);
                Require(h>=VisualTerrain3D.ViewportMinimumY && h<=VisualTerrain3D.ViewportMaximumY,
                    "actual edited terrain lies in supported height envelope");
            }
            terrain.ToggleSmoothSlopes();
            terrain.ToggleSmoothSlopes();
            Require(terrain.StreamingDiagnostics.PendingChunks==0, "geometry format toggles synchronize residency");

            long priorEpoch=terrain.StreamingDiagnostics.WorldEpoch;
            string backend = terrain.TerrainBackendName;
            var replacement = new WorldState(120,88,1315511733);
            terrain.BindWorld(replacement, backend == "region-mirror"
                ? new RegionMirrorTerrainSource(replacement, maxResidentRegions:8)
                : new DirectWorldTerrainSource(replacement));
            Require(terrain.StreamingDiagnostics.WorldEpoch==priorEpoch+1 &&
                terrain.StreamingDiagnostics.DesiredChunks==0, "new world clears demand and advances identity");
            terrain.UpdateVisibility(camera,64);
            Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0, "new world builds from fresh data");
            RunFallbackLifecycle(terrain, camera);
            GD.Print($"PASS VIEWPORT ENGINE / {terrain.TerrainBackendName}: {samples} camera-plane samples; " +
                $"{viewports.Count} viewports; pan/zoom/edges/sculpt/format/world replacement; " +
                "zero-budget surface coverage, mesh oracle, water-only, edits and failed handoff");
            GetTree().Quit();
        }
        catch(Exception ex)
        {
            GD.PushError("VIEWPORT ENGINE FAILED: " + ex);
            GetTree().Quit(1);
        }
    }

    private static void ExerciseLivePaintPreview(VisualLab3D lab,VisualTerrain3D terrain)
    {
        var world=(WorldState)typeof(VisualLab3D).GetField("_world",
            System.Reflection.BindingFlags.Instance|
            System.Reflection.BindingFlags.NonPublic)!.GetValue(lab)!;
        int x=-1,z=-1;
        // Choose a real, already resident land cell, not a synthetic flat mock.
        foreach(var key in Details(terrain).Keys)
        {
            for(int iy=key.Item2*16;iy<Math.Min(world.Height,(key.Item2+1)*16) && x<0;iy++)
            for(int ix=key.Item1*16;ix<Math.Min(world.Width,(key.Item1+1)*16);ix++)
                if(world.Tile(ix,iy) is Terrain.Grass or Terrain.Forest)
                {
                    x=ix;z=iy;break;
                }
            if(x>=0)break;
        }
        Require(x>=0,"live brush fixture finds a visible terrestrial tile");
        var key2=(x/16,z/16);
        MeshInstance3D old=Details(terrain)[key2];
        Terrain target=world.Tile(x,z)==Terrain.Grass?Terrain.Forest:Terrain.Grass;
        terrain.BeginInteractiveEditBatch();
        world.Paint(x,z,0,target);
        terrain.MarkChanged(x,z,0,coastlineChanged:false);
        terrain.PresentLiveBrushPreview(3);
        MeshInstance3D overlay=terrain.GetNodeOrNull<MeshInstance3D>(
            $"LiveBrush_{key2.Item1}_{key2.Item2}") ??
            throw new InvalidOperationException("Live terrain overlay is missing.");
        Require(overlay.Visible &&
            overlay.Mesh is not null && !old.Visible &&
            world.Tile(x,z)==target,
            "native new-biome surface is visible while button is held");
        terrain.CommitInteractiveEditBatch();
        Require(overlay.Visible && !overlay.IsQueuedForDeletion(),
            "mouse release does not expose stale underlying terrain");
        terrain.FlushChanges();
        Require(overlay.IsQueuedForDeletion() && old.Visible &&
            world.Tile(x,z)==target,
            "authoritative terrain/foliage publication retires the live overlay");

        // A second, forest-specific exercise proves REAL Kenney foliage is
        // instanced while the mouse remains held, not merely the land color.
        int treeX=-1,treeZ=-1;
        foreach(var chunkKey in Details(terrain).Keys)
        {
            for(int iy=chunkKey.Item2*16;iy<Math.Min(world.Height,(chunkKey.Item2+1)*16) && treeX<0;iy++)
            for(int ix=chunkKey.Item1*16;ix<Math.Min(world.Width,(chunkKey.Item1+1)*16);ix++)
            {
                if(world.Tile(ix,iy)!=Terrain.Grass)continue;
                uint hash=EnvironmentArtMath.Hash(ix,iy,world.Seed+119);
                if(NatureScatter.Pick(Terrain.Forest,hash)<0)continue;
                treeX=ix;treeZ=iy;
                break;
            }
            if(treeX>=0)break;
        }
        Require(treeX>=0,"resident land provides a deterministic visible tree placement");
        var treeKey=(treeX/16,treeZ/16);
        var initialFoliage=Private<Dictionary<(int,int),Node3D>>(
            terrain,"_foliageChunks")[treeKey];
        long built=terrain.FoliageChunksRebuilt;
        uint treeHash=EnvironmentArtMath.Hash(treeX,treeZ,world.Seed+119);
        int expectedForest=NatureScatter.Pick(Terrain.Forest,treeHash);
        int expectedGrass=NatureScatter.Pick(Terrain.Grass,treeHash);
        terrain.BeginInteractiveEditBatch();
        world.Paint(treeX,treeZ,0,Terrain.Forest);
        terrain.MarkChanged(treeX,treeZ,0,coastlineChanged:false);
        terrain.PresentLiveBrushPreview(3);
        var liveRoot=terrain.LiveNatureNodeAt(treeX,treeZ) ??
            throw new InvalidOperationException("Native live Kenney foliage node missing.");
        bool hasNativeBatch=liveRoot.GetChildren().OfType<MultiMeshInstance3D>()
            .Any(node=>node.Name.ToString().StartsWith("LiveCC0_") &&
                node.Multimesh.VisibleInstanceCount>0);
        GD.Print($"LIVE NATURE CHECK: site={treeX},{treeZ}; " +
            $"liveVisible={liveRoot.Visible}, previousVisible={initialFoliage.Visible}, " +
            $"species={terrain.LiveNatureSpeciesAt(treeX,treeZ)}, expected={expectedForest}, " +
            $"batch={hasNativeBatch}, built={terrain.FoliageChunksRebuilt}, before={built}; " +
            $"parts={string.Join(",",liveRoot.GetChildren().OfType<MultiMeshInstance3D>().Select(node=>node.Name+":"+node.Multimesh.VisibleInstanceCount))}");
        Require(liveRoot.Visible && !initialFoliage.Visible &&
            terrain.LiveNatureSpeciesAt(treeX,treeZ)==expectedForest &&
            hasNativeBatch && terrain.FoliageChunksRebuilt==built,
            "new Kenney tree is GPU-instanced BEFORE release, without rebuilding a full foliage chunk");

        world.Paint(treeX,treeZ,0,Terrain.Grass);
        terrain.MarkChanged(treeX,treeZ,0,coastlineChanged:false);
        terrain.PresentLiveBrushPreview(3);
        Require(liveRoot.Visible && !initialFoliage.Visible &&
            terrain.LiveNatureSpeciesAt(treeX,treeZ)==expectedGrass &&
            terrain.FoliageChunksRebuilt==built,
            "painting over a forest immediately removes the old tree instance");

        world.Paint(treeX,treeZ,0,Terrain.Forest);
        terrain.MarkChanged(treeX,treeZ,0,coastlineChanged:false);
        terrain.PresentLiveBrushPreview(3);
        Require(terrain.LiveNatureSpeciesAt(treeX,treeZ)==expectedForest &&
            terrain.FoliageChunksRebuilt==built,
            "repeated live forest edits reuse the same native batches");
        terrain.CommitInteractiveEditBatch();
        Require(liveRoot.Visible && !liveRoot.IsQueuedForDeletion(),
            "live foliage remains through mouse-up while the original tree rebuilds");
        terrain.FlushChanges();
        Require(liveRoot.IsQueuedForDeletion() &&
            Private<Dictionary<(int,int),Node3D>>(
                terrain,"_foliageChunks")[treeKey].Visible,
            "the full original Kenney foliage replaces preview with no stale overlay");
    }

    private static void Require(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException(message);
    }
}
