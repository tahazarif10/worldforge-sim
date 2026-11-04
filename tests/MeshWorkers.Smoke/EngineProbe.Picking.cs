using System;
using Godot;
using WorldForge.Camera3D;
using WorldForge.VisualLab;
using WorldSim;
using Num3 = System.Numerics.Vector3;

namespace WorldForge.WorkerTests;

public partial class EngineProbe
{
    private sealed class CountingTerrainSource(ITerrainTileSource source) : ITerrainTileSource
    {
        public int Width => source.Width;
        public int Height => source.Height;
        public int Seed => source.Seed;
        public long Reads { get; set; }
        public Terrain Tile(int x,int y) { Reads++; return source.Tile(x,y); }
        public void SynchronizeArea(int x,int y,int radius) => source.SynchronizeArea(x,y,radius);
    }

    private static bool SamePick(bool scalar,TerrainGridPicker.Hit a,
        bool batched,TerrainGridPicker.Hit b) => scalar==batched &&
        (!scalar || (a.X==b.X && a.Y==b.Y && Num3.Distance(a.Point,b.Point)<.0005f));

    private static bool SamePose(CameraReviewPose a,CameraReviewPose b) =>
        Math.Abs(a.PivotX-b.PivotX)<.0005f && Math.Abs(a.PivotZ-b.PivotZ)<.0005f &&
        Math.Abs(a.Size-b.Size)<.0005f;

    private void ExercisePicking(VisualLab3D lab,VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        var configured=(StrategyCameraRig)typeof(VisualLab3D).GetField("_cameraRig",Hidden)!.GetValue(lab)!;
        Require(typeof(StrategyCameraRig).GetField("_surfaceFan",Hidden)!.GetValue(configured) is not null,
            "worker playable camera is wired to the single-fan supplier");

        // Count actual adapter reads, not elapsed CI time. A vertical ray visits
        // one smooth forest cell; both paths use the same production samplers.
        var uniform=new WorldState(16,16,1315511732);
        for(int z=0;z<16;z++)for(int x=0;x<16;x++)uniform.Paint(x,z,0,Terrain.Forest);
        ITerrainTileSource source=_mirror ? new RegionMirrorTerrainSource(uniform,8) :
            new DirectWorldTerrainSource(uniform);
        var counted=new CountingTerrainSource(source);
        terrain.BindWorld(uniform,counted);
        Format(terrain,true,true,true);
        counted.Reads=0;
        bool oldHit=TerrainGridPicker.TryPick(new Num3(8.25f,20,8.75f),-Num3.UnitY,16,16,
            terrain.HeightAt,out var oldPoint,terrain.ReliefAt,terrain.SurfaceHeightAt);
        long oldReads=counted.Reads;
        counted.Reads=0;
        bool newHit=TerrainGridPicker.TryPick(new Num3(8.25f,20,8.75f),-Num3.UnitY,16,16,
            terrain.HeightAt,out var newPoint,terrain.ReliefAt,terrain.SurfaceHeightAt,terrain.SurfaceFanAt);
        Require(oldHit && SamePick(oldHit,oldPoint,newHit,newPoint) && oldReads==97 && counted.Reads==21,
            $"one production fan reduces exact biome reads from 97 to 21; observed {oldReads}/{counted.Reads}");

        int vertexCases=0,rayCases=0,cameraCases=0;
        foreach(int seed in new[]{1315511732,20261030})
        {
            var world=new WorldState(120,88,seed);
            // Boundary samples visit all four data regions. A single resident
            // forces recreation between them without changing the scalar oracle.
            var mirror=_mirror ? new RegionMirrorTerrainSource(world,maxResidentRegions:1) : null;
            terrain.BindWorld(world,mirror is null ? new DirectWorldTerrainSource(world) : mirror);
            Format(terrain,true,true,true);
            world.Paint(63,63,3,Terrain.Mountain);
            world.Paint(64,64,1,Terrain.Forest);
            // A one-cell river and neighboring shallow water remain flat even
            // if the stored lattice beneath them has a sculpt offset.
            for(int z=0;z<88;z++)
            {
                world.Paint(70,z,0,Terrain.DeepWater);
                world.Paint(71,z,0,Terrain.ShallowWater);
            }
            terrain.MarkChanged(60,44,64);
            Require(terrain.SculptElevation(63,63,0,8)>0 &&
                terrain.SculptElevation(64,64,0,-8)>0 &&
                terrain.SculptElevation(70,40,0,8)>0,
                "picker fixtures include both sculpt limits and water with stored elevation");
            var cells=new[]{(0,0),(15,15),(16,16),(63,63),(64,64),(70,40),(71,40),(119,87)};
            foreach(bool rich in new[]{false,true})
            foreach(bool facets in new[]{false,true})
            foreach(bool smooth in new[]{false,true})
            {
                Format(terrain,rich,facets,smooth);
                foreach(var (x,z) in cells)
                {
                    var fan=terrain.SurfaceFanAt(x,z);
                    float[] actual=[fan.A,fan.B,fan.C,fan.D,fan.Center];
                    (float U,float V)[] points=[(0,0),(0,1),(1,0),(1,1),(.5f,.5f)];
                    for(int i=0;i<points.Length;i++)
                    {
                        float expected=terrain.SurfaceHeightAt(x,z,points[i].U,points[i].V);
                        Require(BitConverter.SingleToInt32Bits(actual[i])==BitConverter.SingleToInt32Bits(expected),
                            "native fan inputs retain the scalar sampler's exact five floating-point heights");
                        vertexCases++;
                    }
                    foreach(float u in new[]{.000001f,.25f,.5f,.999999f})
                    {
                        var origin=new Num3(x+u,25,z+.5f);
                        bool scalar=TerrainGridPicker.TryPick(origin,-Num3.UnitY,120,88,
                            terrain.HeightAt,out var a,terrain.ReliefAt,
                            terrain.SmoothLandEnabled ? terrain.SurfaceHeightAt : null);
                        bool batched=TerrainGridPicker.TryPick(origin,-Num3.UnitY,120,88,
                            terrain.HeightAt,out var b,terrain.ReliefAt,
                            surfaceFanAt:terrain.SmoothLandEnabled ? terrain.SurfaceFanAt : null);
                        Require(SamePick(scalar,a,batched,b),
                            "actual terrain scalar/fan picks agree at river, map, chunk, region and sculpt boundaries");
                        rayCases++;
                    }
                }

                var scalarRig=new StrategyCameraRig(camera,terrain.HeightAt,120,88,
                    terrain.ReliefAt,()=>terrain.SmoothLandEnabled,terrain.SurfaceHeightAt);
                int fanCalls=0;
                var fanRig=new StrategyCameraRig(camera,terrain.HeightAt,120,88,
                    terrain.ReliefAt,()=>terrain.SmoothLandEnabled,terrain.SurfaceHeightAt,
                    (x,z)=>{fanCalls++;return terrain.SurfaceFanAt(x,z);});
                Vector2 size=camera.GetViewport().GetVisibleRect().Size;
                foreach(var pose in new[]{new CameraReviewPose(60,44,79),new CameraReviewPose(64,63,19)})
                {
                    foreach(float sx in new[]{.1f,.5f,.9f})
                    foreach(float sy in new[]{.1f,.5f,.9f})
                    {
                        Vector2 screen=new(size.X*sx,size.Y*sy);
                        scalarRig.RestoreReviewPose(pose);
                        bool scalar=scalarRig.TryPickTerrain(screen,out var a);
                        fanRig.RestoreReviewPose(pose);
                        bool batched=fanRig.TryPickTerrain(screen,out var b);
                        Require(SamePick(scalar,a,batched,b),
                            "real camera brush targets are identical under both pick suppliers");
                        rayCases++;
                    }
                    Vector2 anchor=size*.5f+new Vector2(130,40);
                    scalarRig.RestoreReviewPose(pose);
                    fanRig.RestoreReviewPose(pose);
                    foreach(float scale in new[]{.94f,1.08f,.97f})
                    {
                        // Each rig keeps its own pivot; restore the shared
                        // native camera to that rig before the next action.
                        scalarRig.RestoreReviewPose(scalarRig.ReviewPose);
                        scalarRig.ZoomAt(anchor,scale);
                        CameraReviewPose expected=scalarRig.ReviewPose;
                        fanRig.RestoreReviewPose(fanRig.ReviewPose);
                        fanRig.ZoomAt(anchor,scale);
                        Require(SamePose(expected,fanRig.ReviewPose),
                            "single-fan zoom preserves the existing world anchor and complete pose trace");
                        cameraCases++;
                    }
                    scalarRig.RestoreReviewPose(pose);
                    scalarRig.BeginDrag(anchor);scalarRig.DragTo(anchor+new Vector2(47,-29));
                    CameraReviewPose dragged=scalarRig.ReviewPose;scalarRig.EndDrag();
                    fanRig.RestoreReviewPose(pose);
                    fanRig.BeginDrag(anchor);fanRig.DragTo(anchor+new Vector2(47,-29));fanRig.EndDrag();
                    Require(SamePose(dragged,fanRig.ReviewPose),
                        "single-fan drag retains the fixed-height anchor and correction");
                    cameraCases++;
                }
                Require(terrain.SmoothLandEnabled ? fanCalls>0 : fanCalls==0,
                    "legacy flat/cliff formats do not activate the optional smooth fan path");
            }

            // Re-query after a brush removes edited land and after it restores
            // land: no persistent pick cache may retain the old surface.
            Format(terrain,true,true,true);
            foreach(Terrain next in new[]{Terrain.DeepWater,Terrain.Sand})
            {
                world.Paint(64,64,1,next);terrain.MarkChanged(64,64,1);
                var origin=new Num3(64.25f,25,64.75f);
                bool scalar=TerrainGridPicker.TryPick(origin,-Num3.UnitY,120,88,
                    terrain.HeightAt,out var a,terrain.ReliefAt,terrain.SurfaceHeightAt);
                bool batched=TerrainGridPicker.TryPick(origin,-Num3.UnitY,120,88,
                    terrain.HeightAt,out var b,terrain.ReliefAt,surfaceFanAt:terrain.SurfaceFanAt);
                Require(scalar && SamePick(scalar,a,batched,b),
                    "post-brush surface is read afresh for both direct and mirrored terrain");
                rayCases++;
            }
            Require(mirror is null || (mirror.MaxResidentRegions==1 && mirror.ResidentRegions==1),
                "boundary and post-brush picks preserve parity through the one-region mirror cache");
        }
        GD.Print($"PASS SINGLE-FAN ENGINE / {(_mirror?"region-mirror":"direct")}: " +
            $"{vertexCases} exact vertex inputs, {rayCases} scalar-oracle rays, {cameraCases} zoom/drag actions; " +
            "97-to-21 biome reads, all formats, river, sculpt limits, edits and world replacement");
    }
}
