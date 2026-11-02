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
            if (!requested)
            {
                Require(terrain.StreamingPlannerName == "legacy-v2" && terrain.ActiveTerrainChunks > 0 &&
                    terrain.ActiveFallbackChunks == 0,
                    "normal scene retains legacy planner and geometry");
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

    private static void Require(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException(message);
    }
}
