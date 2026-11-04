using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using WorldForge.Camera3D;
using WorldForge.Streaming;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.WorkerTests;

public partial class EngineProbe
{
    private void CheckLimits(VisualTerrain3D t)
    {
        var d=t.StreamingDiagnostics;var w=d.Workers;
        Require(System.Environment.CurrentManagedThreadId==_owner,"owner retained after frame await");
        Require(d.MissingVisibleChunks==0 && d.MissingVisibleCellUpperBound==0,
            "real visible surface coverage survives pending work");
        Require(d.DesiredChunks<=d.Capacity && t.ActiveTerrainChunks<=d.DesiredChunks,
            "native residency follows bounded demand");
        Require(w.Active<=2 && w.Retained<=4 && w.ReservedArrayBytes<=16*1024*1024 &&
            w.ReadyPayloadBytes<=w.ReservedArrayBytes,"running and retained numeric payloads remain bounded");
        Require(w.LastUploadCount<=2 && w.LastUploadPayloadBytes<=2*1024*1024,
            "actual main-thread publication respects hard count and payload admission");
        Require(w.StagedChunks is >=0 and <=1 && w.StagedPayloadBytes<=ChunkMeshData.MaximumPayloadBytes &&
            w.LastUploadCount==w.LastTerrainStages+w.LastFoliageStages+w.LastCoastStages &&
            w.LastTerrainMilliseconds>=0 && w.LastFoliageMilliseconds>=0 && w.LastCoastMilliseconds>=0,
            "single staged ownership and phase accounting remain bounded");
        Require(double.IsFinite(w.LastUploadMilliseconds) && double.IsFinite(w.LastSnapshotMilliseconds) &&
            w.LastPumpMilliseconds>=w.LastUploadMilliseconds-.01,"upload time is separately measured within pump");
    }

    private async Task Drain(VisualTerrain3D t,Godot.Camera3D camera)
    {
        long start=Stopwatch.GetTimestamp();
        for(int frames=0;frames<1200;frames++)
        {
            t.UpdateVisibility(camera,2);CheckLimits(t);
            var d=t.StreamingDiagnostics;
            Require(d.Workers.FailedChunks==0 && d.Workers.CoastFailures<2,"no unexpected exhausted worker failure");
            if(d.PendingChunks==0 && d.Workers.Retained==0 && !d.Workers.CoastPending)
            {
                Require(d.MissingDetailedVisibleChunks==0 && t.ActiveFallbackChunks==0,
                    "settled matching detail retires every visible proxy");
                return;
            }
            Require(Stopwatch.GetElapsedTime(start).TotalSeconds<25,
                "bounded worker demand eventually drains: "+t.LastMeshWorkerFailure);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        throw new TimeoutException("Worker demand exceeded frame limit.");
    }

    private async Task WaitReady(BoundedWorkPool<ChunkRequest,VisualBuildResult> pool)
    {
        long start=Stopwatch.GetTimestamp();
        while(pool.Completed==0)
        {
            Require(Stopwatch.GetElapsedTime(start).TotalSeconds<5,"controlled worker completes");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ReleaseCanceled(VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        long start=Stopwatch.GetTimestamp();
        while(Pool(terrain).Count>0)
        {
            terrain.UpdateVisibility(camera,0);
            Require(Stopwatch.GetElapsedTime(start).TotalSeconds<5,"canceled reservations are eventually released");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }

    private bool _textureUpdateReadback;
    private void DetectTextureReadback()
    {
        // Godot's dummy renderer stores initial image data but texture_2d_update
        // is a no-op. Detect the actual engine capability; never claim GPU parity.
        byte[] initial=[1,2,3,255],changed=[4,5,6,255];
        using Image before=Image.CreateFromData(1,1,false,Image.Format.Rgba8,initial);
        using Image after=Image.CreateFromData(1,1,false,Image.Format.Rgba8,changed);
        using ImageTexture texture=ImageTexture.CreateFromImage(before);
        texture.Update(after);
        using Image readback=texture.GetImage();
        byte[] observed=readback.GetData();
        _textureUpdateReadback=observed.SequenceEqual(changed);
        Require(_textureUpdateReadback || observed.SequenceEqual(initial),"known headless texture contract");
        GD.Print(_textureUpdateReadback?"Coast texture update readback available":
            "Headless texture update readback unavailable: validating consumed payload and native binding; GPU gate pending");
    }

    private void CheckCoast(VisualTerrain3D terrain,WorldState world)
    {
        byte[] expected=WaterCoastMath.BuildMask(world.Width,world.Height,world.Tile,world.Seed);
        var d=terrain.WorkerDiagnostics;
        Require(d.CoastPublications>0 && !d.CoastPending &&
            d.LastCoastPayloadSha256==Convert.ToHexString(SHA256.HashData(expected)),
            "successfully consumed dirty coast payload matches original mask byte for byte");
        var texture=Field<ImageTexture>(terrain,"_coastMaskTexture");
        var water=Field<MeshInstance3D>(terrain,"_water");
        Require(texture.GetWidth()==world.Width && texture.GetHeight()==world.Height &&
            water.MaterialOverride is ShaderMaterial material &&
            material.GetShaderParameter("shore_mask").AsGodotObject()==texture,
            "current coast texture is bound to the original native water material");
        if(_textureUpdateReadback)
        {
            using Image image=texture.GetImage();
            Require(image.GetData().SequenceEqual(expected),"actual updated coast readback matches");
        }
    }

    private async Task ExercisePipeline(VisualLab3D lab,VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        var world=new WorldState(120,88,1315511732);
        Bind(terrain,world);
        var rig=Rig(terrain,camera,120,88);
        rig.RestoreReviewPose(new CameraReviewPose(60,44,79));
        terrain.UpdateVisibility(camera,2);
        Require(Pool(terrain).Count>0,"real adapter submits bounded tasks");
        ChunkRequest stale=default;
        Require(Pool(terrain).Any(r=>{if(r.Key.X<0)return false;stale=r;return true;}),
            "capture a detailed request before consumption");
        long discarded=terrain.WorkerDiagnostics.Discarded;
        int sx=stale.Key.X*16,sz=stale.Key.Z*16;
        world.Paint(sx,sz,0,Terrain.Forest);terrain.MarkChanged(sx,sz,0);
        terrain.FlushChanges();
        Require(!Queue(terrain).IsCurrent(stale),"paint invalidates in-flight lease before publication");
        await Drain(terrain,camera);
        Require(terrain.WorkerDiagnostics.Discarded>discarded &&
            terrain.WorkerDiagnostics.LastWorkerThreadId!=_owner,"stale work discarded and compute stays off owner");
        CheckCoast(terrain,world);

        // Reproduce the requested live Mountain brush: visibly raise a
        // land cell BEFORE mouse release, without replacing the surrounding
        // forest/flowers with the flat emergency fallback.
        var visibleTerrainBefore=Details(terrain)
            .Where(p=>p.Value.IsVisibleInTree()).ToDictionary(p=>p.Key,p=>p.Value);
        var visibleFoliageBefore=Foliage(terrain)
            .Where(p=>p.Value.IsVisibleInTree()).ToDictionary(p=>p.Key,p=>p.Value);
        int px=-1,pz=-1;
        (int,int) retainedKey=default;
        foreach(var key in visibleTerrainBefore.Keys)
        {
            if(!visibleFoliageBefore.TryGetValue(key,out Node3D? resident) ||
                resident.GetChildCount()==0)continue;
            for(int z=key.Item2*16+3;z<Math.Min(world.Height,key.Item2*16+13) && px<0;z++)
            for(int x=key.Item1*16+3;x<Math.Min(world.Width,key.Item1*16+13);x++)
            {
                if(world.Tile(x,z) is not (Terrain.Grass or Terrain.Forest or Terrain.Sand))
                    continue;
                bool dry=true;
                for(int dz=-1;dz<=1;dz++)
                for(int dx=-1;dx<=1;dx++)
                    if(world.Tile(x+dx,z+dz) is Terrain.DeepWater or Terrain.ShallowWater)
                        dry=false;
                if(!dry)continue;
                px=x;pz=z;retainedKey=key;break;
            }
            if(px>=0)break;
        }
        Require(px>=0,"live paint fixture has a dry detailed chunk with actual foliage");
        MeshInstance3D originalTerrain=visibleTerrainBefore[retainedKey];
        Node3D originalFoliage=visibleFoliageBefore[retainedKey];
        float MidHeight(MeshInstance3D instance)
        {
            using var arrays=instance.Mesh!.SurfaceGetArrays(0);
            foreach(Vector3 v in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                if(Math.Abs(v.X-(px+.5f))<.001f && Math.Abs(v.Z-(pz+.5f))<.001f)
                    return v.Y;
            throw new InvalidOperationException("Center of painted land tile missing from detailed mesh.");
        }
        float baselineHeight=MidHeight(originalTerrain);
        long fallbackBuildsBefore=terrain.TotalFallbackBuilds;
        terrain.BeginInteractiveEditBatch();
        world.Paint(px,pz,0,Terrain.Mountain);
        terrain.MarkChanged(px,pz,0,false);
        terrain.PreviewInteractiveEditBatch(.10);
        bool publishedWhileHeld=false;
        for(int frame=0;frame<300;frame++)
        {
            terrain.FlushChanges(); // The interactive process must not force fallback.
            terrain.UpdateVisibility(camera,2);
            Require(terrain.TotalFallbackBuilds==fallbackBuildsBefore,
                "held Mountain paint creates no flat fallback on resident detail");
            MeshInstance3D currentTerrain=Details(terrain)[retainedKey];
            Node3D currentFoliage=Foliage(terrain)[retainedKey];
            Require(currentTerrain.IsVisibleInTree() && currentFoliage.IsVisibleInTree(),
                "terrain and trees/flowers stay visible during live worker replacement");
            if(!ReferenceEquals(currentTerrain,originalTerrain))
            {
                Require(MidHeight(currentTerrain)>baselineHeight+1f,
                    "mountain height becomes visible while mouse is still held");
                publishedWhileHeld=true;
                break;
            }
            Require(originalTerrain.IsVisibleInTree() && originalFoliage.IsVisibleInTree(),
                "original detailed terrain and foliage remain visible until atomic handoff");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        Require(publishedWhileHeld,
            "live Mountain preview publishes a new detailed mesh before mouse release");
        terrain.CommitInteractiveEditBatch();
        await Drain(terrain,camera);
        Require(Foliage(terrain)[retainedKey].IsVisibleInTree() &&
            Details(terrain)[retainedKey].IsVisibleInTree() &&
            terrain.ActiveFallbackChunks==0,
            "live paint settles with detailed terrain and foliage intact");

        var poses=new[]{
            new CameraReviewPose(0,0,19),new CameraReviewPose(120,88,19),
            new CameraReviewPose(60,44,79),new CameraReviewPose(0,88,160),
            new CameraReviewPose(120,0,160),new CameraReviewPose(60,44,19)};
        for(int i=0;i<48;i++)
        {
            if(i==16 || i==32)
            {
                GetWindow().Size=i==16?new Vector2I(640,960):new Vector2I(1280,800);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            rig.RestoreReviewPose(poses[i%poses.Length]);
            if(i%11==0)
            {
                int x=15+(i*7)%88,z=12+(i*3)%60;
                world.Paint(x,z,1,i%2==0?Terrain.DeepWater:Terrain.Mountain);
                terrain.MarkChanged(x,z,1);
                terrain.SculptElevation(x,z,1,i%2==0?1.2f:-1.2f);
                terrain.FlushChanges();
            }
            terrain.UpdateVisibility(camera,2);CheckLimits(terrain);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        rig.RestoreReviewPose(new CameraReviewPose(60,44,79));
        await Drain(terrain,camera);CheckCoast(terrain,world);

        // Geometry-format changes also use the worker queue; no synchronous detail burst.
        long builds=terrain.TotalTerrainChunkBuilds;
        terrain.ToggleSmoothSlopes();terrain.FlushChanges();
        Require(terrain.TotalTerrainChunkBuilds==builds && terrain.StreamingDiagnostics.PendingChunks>0,
            "format invalidation defers detailed native rebuild");
        await Drain(terrain,camera);
        terrain.ToggleSmoothSlopes();await Drain(terrain,camera);

        // Retain reservations across NewWorld until old tasks actually stop.
        terrain.MarkChanged(60,44,40);terrain.UpdateVisibility(camera,2);
        long reserved=Pool(terrain).ReservedBytes,epoch=terrain.StreamingDiagnostics.WorldEpoch;
        Require(reserved>0,"world replacement begins with retained work");
        var small=new WorldState(33,25,1315511749);
        for(int z=0;z<25;z++)
        for(int x=0;x<33;x++)small.Paint(x,z,0,Terrain.Grass);
        Bind(terrain,small);
        Require(terrain.StreamingDiagnostics.WorldEpoch==epoch+1 &&
            Pool(terrain).ReservedBytes==reserved,"new world cancels without prematurely releasing live memory");
        rig=Rig(terrain,camera,33,25);
        rig.RestoreReviewPose(new CameraReviewPose(16,12,79));
        await ReleaseCanceled(terrain,camera);
        terrain.UpdateVisibility(camera,0);CheckLimits(terrain);

        // CI-only controlled exceptions exercise the real owner's finite retry policy.
        var key=new ChunkCoord(0,0);
        for(int attempt=0;attempt<2;attempt++)
        {
            Require(Queue(terrain).TryBegin(key,out ChunkRequest request),"reserve failing request");
            Require(Pool(terrain).TryStart(request,4*1024*1024,
                _=>throw new InvalidOperationException("CI controlled numeric build failure")),
                "inject bounded failing task");
            await WaitReady(Pool(terrain));
            terrain.UpdateVisibility(camera,0);CheckLimits(terrain);
        }
        Require(terrain.WorkerDiagnostics.FailedChunks==1 && !Queue(terrain).TryBegin(key,out _) &&
            terrain.HasViewportSurfaceCoverage(0,0),"two failures stop retries while preserving fallback");
        terrain.MarkChanged(1,1,0);
        await Drain(terrain,camera);
        Require(terrain.WorkerDiagnostics.FailedChunks==0,"a new data generation recovers exhausted chunk");

        small.Paint(16,12,3,Terrain.DeepWater);terrain.MarkChanged(16,12,3);
        terrain.FlushChanges();await Drain(terrain,camera);CheckCoast(terrain,small);

        // Validate an old immutable numeric result cannot mutate actual native nodes after a newer generation.
        terrain.MarkChanged(1,1,0);terrain.UpdateVisibility(camera,0);
        Require(Queue(terrain).TryBegin(key,out ChunkRequest old),"reserve stale native handoff");
        var snapshot=(ChunkBuildSnapshot)Call(terrain,"CaptureWorkerChunk",old)!;
        var data=ChunkMeshBuilder.Build(snapshot);
        terrain.MarkChanged(1,1,0);
        long before=terrain.TotalTerrainChunkBuilds;
        Require(!(bool)Call(terrain,"UploadWorkerChunk",old,data)! &&
            terrain.TotalTerrainChunkBuilds==before,"late handoff is rejected before native allocation");
        await Drain(terrain,camera);

        // Actual game-frame hook pumps workers even below the old 50ms cadence.
        Bind(terrain,world);
        rig=Rig(terrain,camera,120,88);rig.RestoreReviewPose(new CameraReviewPose(60,44,79));
        SetField(terrain,"_coastDirty",false);
        typeof(VisualLab3D).GetField("_visibilityRefresh",Hidden)!.SetValue(lab,0d);
        lab._Process(.001);
        Require(Pool(terrain).Count>0,"sub-50ms playable frame submits workers");
        var exitStage=await BeginStagedCase(terrain,camera);
        var pending=Pool(terrain);
        lab.Free();
        Require(!GodotObject.IsInstanceValid(exitStage.Hidden),"teardown frees hidden native replacement");
        Require(pending.Count==0 && pending.ReservedBytes==0,"scene teardown cancels and joins all retained jobs");
    }
}
