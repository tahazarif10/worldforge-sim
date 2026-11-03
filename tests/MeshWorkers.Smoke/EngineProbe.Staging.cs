using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WorldForge.Camera3D;
using WorldForge.Streaming;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.WorkerTests;

public partial class EngineProbe
{
    private sealed record StagedCase(WorldState World,ChunkRequest Request,ChunkMeshData Data,MeshInstance3D Hidden);

    private async Task<StagedCase> BeginStagedCase(VisualTerrain3D terrain,Godot.Camera3D camera,PackedScene? importedArt=null)
    {
        var world=new WorldState(16,16,1315511732);
        for(int z=0;z<16;z++)for(int x=0;x<16;x++)world.Paint(x,z,0,Terrain.Forest);
        Bind(terrain,world);
        Rig(terrain,camera,16,16).RestoreReviewPose(new CameraReviewPose(8,8,40));
        await ReleaseCanceled(terrain,camera);
        terrain.UpdateVisibility(camera,0);
        SetField(terrain,"_coastDirty",false);
        // Bind reloads authored art when absent. Apply the controlled import
        // fixture after Bind, before immutable input capture.
        if(importedArt is not null)
        {
            SetField(terrain,"_natureArt",null);
            SetField(terrain,"_treePrefab",importedArt);SetField(terrain,"_rockPrefab",importedArt);
        }
        Require(Queue(terrain).TryBegin(new(0,0),out ChunkRequest request),"reserve staged fixture");
        var snapshot=(ChunkBuildSnapshot)Call(terrain,"CaptureWorkerChunk",request)!;
        var data=ChunkMeshBuilder.Build(snapshot);
        Require(Pool(terrain).TryStart(request,4*1024*1024,_=>VisualBuildResult.Chunk(data)),
            "submit controlled owned staged result");
        await WaitReady(Pool(terrain));
        ulong started=Time.GetTicksMsec();
        do
        {
            terrain.UpdateVisibility(camera,1);CheckLimits(terrain);
            if(terrain.WorkerDiagnostics.LastTerrainStages==1)break;
            Require(Time.GetTicksMsec()-started<5000,"terrain stage admitted within bounded fixture wait");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        } while(true);
        object staged=Field<object>(terrain,"_pendingWorkerUpload");
        var hidden=(MeshInstance3D)staged.GetType().GetField("Terrain")!.GetValue(staged)!;
        var w=terrain.WorkerDiagnostics;
        Require(w.StagedChunks==1 && w.LastTerrainStages==1 && w.LastFoliageStages==0 &&
            w.LastUploadCount==1 && w.LastUploadPayloadBytes==data.TerrainPayloadBytes,
            "one-unit update admits terrain exactly once and defers foliage");
        Require(Pool(terrain).Count==1 && Pool(terrain).ReservedBytes==4*1024*1024 &&
            w.StagedPayloadBytes==data.PayloadBytes,"staged numeric result retains its original reservation");
        Require(!hidden.IsInsideTree() && !hidden.Visible &&
            !terrain.HasViewportDetailedCoverage(0,0) && terrain.HasViewportSurfaceCoverage(0,0),
            "hidden prepared terrain never replaces current visible fallback");
        return new(world,request,data,hidden);
    }


    private async Task AdmitStagedFoliage(VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        ulong started=Time.GetTicksMsec();
        do
        {
            terrain.UpdateVisibility(camera,1);CheckLimits(terrain);
            if(terrain.WorkerDiagnostics.LastFoliageStages==1)return;
            Require(Time.GetTicksMsec()-started<5000,"foliage stage admitted within bounded fixture wait");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        } while(true);
    }

    private async Task ExerciseStaging(VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        NatureArtLibrary? art=Field<NatureArtLibrary?>(terrain,"_natureArt");
        PackedScene? tree=Field<PackedScene?>(terrain,"_treePrefab");
        PackedScene? rock=Field<PackedScene?>(terrain,"_rockPrefab");
        using PackedScene optional=OptionalArt();
        try
        {
            var first=await BeginStagedCase(terrain,camera);
            terrain.UpdateVisibility(camera,0);CheckLimits(terrain);
            Require(GodotObject.IsInstanceValid(first.Hidden) && !first.Hidden.IsInsideTree() &&
                terrain.WorkerDiagnostics.StagedChunks==1 && terrain.WorkerDiagnostics.LastUploadCount==0 &&
                Pool(terrain).ReservedBytes==4*1024*1024,
                "zero-budget update retains hidden stage and its reservation without publishing");
            await AdmitStagedFoliage(terrain,camera);
            var w=terrain.WorkerDiagnostics;
            Require(w.StagedChunks==0 && w.LastTerrainStages==0 && w.LastFoliageStages==1 &&
                w.LastUploadCount==1 && w.LastUploadPayloadBytes==first.Data.FoliagePayloadBytes &&
                Pool(terrain).Count==0 && Pool(terrain).ReservedBytes==0,
                "foliage phase charges only its own payload and releases completed reservation");
            Require(ReferenceEquals(Details(terrain)[(0,0)],first.Hidden) &&
                first.Hidden.IsInsideTree() && terrain.HasViewportDetailedCoverage(0,0) &&
                Foliage(terrain)[(0,0)].IsVisibleInTree() && terrain.ActiveFallbackChunks==0,
                "matching terrain and foliage become visible together before fallback retirement");

            var edit=await BeginStagedCase(terrain,camera);
            edit.World.Paint(1,1,0,Terrain.DeepWater);terrain.MarkChanged(1,1,0);terrain.FlushChanges();
            terrain.UpdateVisibility(camera,0);CheckLimits(terrain);
            Require(!GodotObject.IsInstanceValid(edit.Hidden) && terrain.WorkerDiagnostics.StagedChunks==0 &&
                Pool(terrain).Count==0 && terrain.HasViewportSurfaceCoverage(0,0),
                "edit between phases disposes hidden native resources and refuses the stale generation");
            await Drain(terrain,camera);

            var format=await BeginStagedCase(terrain,camera);
            terrain.ToggleSmoothSlopes();terrain.UpdateVisibility(camera,0);CheckLimits(terrain);
            Require(!GodotObject.IsInstanceValid(format.Hidden) && terrain.WorkerDiagnostics.StagedChunks==0,
                "format invalidation drops staged geometry");
            terrain.ToggleSmoothSlopes();await Drain(terrain,camera);

            var reset=await BeginStagedCase(terrain,camera);
            long reserved=Pool(terrain).ReservedBytes;
            Bind(terrain,new WorldState(16,16,1315511749));
            Require(!GodotObject.IsInstanceValid(reset.Hidden) &&
                terrain.WorkerDiagnostics.StagedChunks==0 && Pool(terrain).ReservedBytes==reserved,
                "world reset frees hidden natives while preserving completed-entry cleanup accounting");
            await ReleaseCanceled(terrain,camera);
            Require(Pool(terrain).ReservedBytes==0,"old world staged numeric reservation eventually released");

            var fail=await BeginStagedCase(terrain,camera,optional);
            Require(fail.Data.Props.ToArray().Any(p=>p.Kind==NumericPropKind.ImportedTree),
                "failure fixture contains captured imported foliage");
            SetField(terrain,"_treePrefab",null);SetField(terrain,"_rockPrefab",null);
            await AdmitStagedFoliage(terrain,camera);
            Require(!GodotObject.IsInstanceValid(fail.Hidden) &&
                terrain.LastMeshWorkerFailure?.Contains("Captured imported art is unavailable")==true &&
                !Pool(terrain).Any(r=>r==fail.Request) && terrain.HasViewportSurfaceCoverage(0,0),
                "foliage failure releases the staged result without exposing partial replacement");
        }
        finally
        {
            SetField(terrain,"_natureArt",art);
            SetField(terrain,"_treePrefab",tree);SetField(terrain,"_rockPrefab",rock);
        }
        terrain.MarkChanged(1,1,0);terrain.FlushChanges();
        await Drain(terrain,camera);
        Require(terrain.WorkerDiagnostics.StagedChunks==0 &&
            terrain.WorkerDiagnostics.FailedChunks==0 && terrain.ActiveFallbackChunks==0,
            "restored art and a fresh generation recover after staged foliage failure");
        GD.Print("PASS STAGED PUBLICATION: separate admissions and payloads, zero-budget wait, atomic handoff, edit/format/reset invalidation and native failure cleanup");
    }
}
