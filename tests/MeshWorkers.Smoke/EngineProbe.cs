using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using WorldForge.Camera3D;
using WorldForge.Streaming;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.WorkerTests;

/// <summary>CI-only comparison against the retained V2 native renderer.</summary>
public partial class EngineProbe : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private int _owner;
    private bool _mirror;
    private int _parityCases;
    private long _comparedVertices;

    public override async void _Ready()
    {
        try
        {
            Require(System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true" &&
                DisplayServer.GetName()=="headless","probe is restricted to headless GitHub CI");
            _owner=System.Environment.CurrentManagedThreadId;
            DetectTextureReadback();
            var lab=GD.Load<PackedScene>("res://Scenes/VisualLab3D.tscn").Instantiate<VisualLab3D>();
            AddChild(lab);lab.SetProcess(false);
            var terrain=lab.GetNode<VisualTerrain3D>("WorldAndFoliage");
            var camera=lab.GetNode<Godot.Camera3D>("IsometricCamera");
            Require(terrain.MeshWorkersEnabled && terrain.ViewportStreamingEnabled &&
                terrain.StreamingPlannerName=="viewport-v3-workers","worker flag implies viewport coverage");
            _mirror=terrain.TerrainBackendName=="region-mirror";
            GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.Disabled;
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            ExerciseCitizenArt(lab);
            ExercisePicking(lab,terrain,camera);
            await CompareRetainedRenderer(terrain,camera);
            await ExerciseStaging(terrain,camera);
            await ExercisePipeline(lab,terrain,camera);
            GD.Print($"PASS WORKER ENGINE / {(_mirror?"region-mirror":"direct")}: " +
                $"{_parityCases} native geometry/foliage parity cases; {_comparedVertices} vertices; " +
                "bounded publication, camera reversal/resize, dirty coast, stale work, failures and teardown");
            GetTree().Quit();
        }
        catch(Exception ex)
        {
            GD.PushError("WORKER ENGINE FAILED: "+ex);
            GetTree().Quit(1);
        }
    }

    private static void ExerciseCitizenArt(VisualLab3D lab)
    {
        var world=(WorldState)typeof(VisualLab3D).GetField("_world",Hidden)!.GetValue(lab)!;
        var visual=lab.GetNode<CitizenArt3D>("Batched3DCitizens");
        Require(visual.InstancedDrawBatches==15 && visual.VisibleCitizenCount==0,
            "stylized citizens start as bounded shared draw batches, not egg-shaped spheres");

        int spawned=0;
        for(int z=3;z<world.Height-3 && spawned<3;z++)
        for(int x=3;x<world.Width-3 && spawned<3;x++)
            if(world.Spawn(x,z))spawned++;
        Require(spawned==3,"three visible-style citizen fixtures can spawn");
        typeof(VisualLab3D).GetMethod("UpdateActors",Hidden)!.Invoke(lab,null);
        visual.Render(.03,1);
        Require(visual.VisibleCitizenCount==3,"citizen rendering follows authoritative spawn");
        int total=0,bodyMeshes=0;
        foreach(Node node in visual.GetChildren())
        {
            Require(node is MultiMeshInstance3D,"each citizen piece is a batched render node");
            var batch=((MultiMeshInstance3D)node).Multimesh;
            Require(batch.Mesh is ArrayMesh && batch.Mesh.GetSurfaceCount()==1,
                "faceted original citizen art uses a single shared colored surface per piece");
            total+=batch.InstanceCount;
            if(node.Name.ToString().EndsWith("_0",StringComparison.Ordinal))
            {
                bodyMeshes++;
                Require(batch.Mesh.GetAabb().Size.Y>1.5f &&
                    batch.Mesh.GetAabb().Size.X>.5f,
                    "citizen body has a distinct large head, clothing and realistic readable silhouette");
                using var arrays=batch.Mesh.SurfaceGetArrays(0);
                Require(arrays[(int)Mesh.ArrayType.Color].AsColorArray().Length>300,
                    "hair, face, tunic, scarf and accessories have authored vertex colors");
            }
        }
        Require(total==3*5 && bodyMeshes==3,"all citizen palettes use one shared mesh per body and limb");
        // Cosmetic interpolation must not advance the simulation clock or move
        // the WorldState authority; rendering remains independent of gameplay.
        long tick=world.Tick;
        var before=world.People.Select(p=>(p.Id,p.X,p.Y)).ToArray();
        visual.Render(.12,1);
        Require(world.Tick==tick &&
            before.SequenceEqual(world.People.Select(p=>(p.Id,p.X,p.Y))),
            "idle/step visuals never mutate citizen simulation");

        for(int z=0;z<3;z++)
            world.Meteor(world.People[0].X,world.People[0].Y,1);
        typeof(VisualLab3D).GetMethod("UpdateActors",Hidden)!.Invoke(lab,null);
        Require(visual.VisibleCitizenCount==world.People.Count,
            "citizen art releases stale instances after meteor removals");
        GD.Print("PASS CITIZEN ART: 3 stylized palettes, shared body/limb meshes, safe animation and removal");
    }

    private static void Require(bool ok,string label)
    {if(!ok)throw new InvalidOperationException(label);}
    private static T Field<T>(VisualTerrain3D terrain,string name)=>
        (T)typeof(VisualTerrain3D).GetField(name,Hidden)!.GetValue(terrain)!;
    private static void SetField(VisualTerrain3D terrain,string name,object? value)=>
        typeof(VisualTerrain3D).GetField(name,Hidden)!.SetValue(terrain,value);
    private static object? Call(VisualTerrain3D terrain,string name,params object[] args)=>
        typeof(VisualTerrain3D).GetMethod(name,Hidden)!.Invoke(terrain,args);
    private static Dictionary<(int,int),MeshInstance3D> Details(VisualTerrain3D t)=>
        Field<Dictionary<(int,int),MeshInstance3D>>(t,"_chunks");
    private static Dictionary<(int,int),Node3D> Foliage(VisualTerrain3D t)=>
        Field<Dictionary<(int,int),Node3D>>(t,"_foliageChunks");
    private static ChunkResidencyQueue Queue(VisualTerrain3D t)=>Field<ChunkResidencyQueue>(t,"_viewportQueue");
    private static BoundedWorkPool<ChunkRequest,VisualBuildResult> Pool(VisualTerrain3D t)=>
        Field<BoundedWorkPool<ChunkRequest,VisualBuildResult>>(t,"_meshWorkers");
    private void Bind(VisualTerrain3D terrain,WorldState world)=>terrain.BindWorld(world,
        _mirror?new RegionMirrorTerrainSource(world,maxResidentRegions:8):new DirectWorldTerrainSource(world));
    private static StrategyCameraRig Rig(VisualTerrain3D t,Godot.Camera3D c,int width,int height)=>
        new(c,t.HeightAt,width,height,t.ReliefAt,()=>t.SmoothLandEnabled,t.SurfaceHeightAt,
            t.MeshWorkersEnabled?t.SurfaceFanAt:null);
    private static void Format(VisualTerrain3D t,bool rich,bool facets,bool smooth)
    {
        if(t.RichTerrainDetails!=rich)t.ToggleRichTerrainDetails();
        if(t.SculptedFacets!=facets)t.ToggleSculptedFacets();
        if(t.SmoothSlopes!=smooth)t.ToggleSmoothSlopes();
    }
    private static PackedScene OptionalArt()
    {
        var root=new Node3D{Name="OptionalArtFixture"};
        var mesh=new MeshInstance3D{Name="Mesh",Mesh=new BoxMesh{Size=new Vector3(.7f,2f,.8f)}};
        root.AddChild(mesh);mesh.Owner=root;
        var scene=new PackedScene();
        Require(scene.Pack(root)==Error.Ok,"optional imported-art fixture packs");
        root.Free();return scene;
    }

    private sealed record Surface(Vector3[] Positions,Vector3[] Normals,Color[] Colors);
    private static Surface ReadSurface(MeshInstance3D node)
    {
        if(node.Mesh is null)return new([],[],[]);
        Require(node.Mesh.GetSurfaceCount()==1,"terrain has one native surface");
        using var arrays=node.Mesh.SurfaceGetArrays(0);
        return new(arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
            arrays[(int)Mesh.ArrayType.Normal].AsVector3Array(),
            arrays[(int)Mesh.ArrayType.Color].AsColorArray());
    }
    private void CompareSurface(Surface a,Surface b,string label)
    {
        Require(a.Positions.Length==b.Positions.Length && a.Normals.Length==b.Normals.Length &&
            a.Colors.Length==b.Colors.Length,label+" array lengths");
        for(int i=0;i<a.Positions.Length;i++)
        {
            Require(a.Positions[i].DistanceTo(b.Positions[i])<.001f,label+" vertex "+i);
            Require(a.Normals[i].DistanceTo(b.Normals[i])<.002f,label+" normal "+i);
            Color x=a.Colors[i],y=b.Colors[i];
            Require(Math.Abs(x.R-y.R)<.005f && Math.Abs(x.G-y.G)<.005f &&
                Math.Abs(x.B-y.B)<.005f && Math.Abs(x.A-y.A)<.005f,label+" color "+i);
        }
        _comparedVertices+=a.Positions.Length;
    }

    private sealed record ArtSignature(string[] Types,float[] Values);
    private static ArtSignature ReadArt(Node root)
    {
        var types=new List<string>();var values=new List<float>();
        void V(Vector3 v){values.Add(v.X);values.Add(v.Y);values.Add(v.Z);}
        void C(Color c){values.Add(c.R);values.Add(c.G);values.Add(c.B);values.Add(c.A);}
        void T(Transform3D t){V(t.Basis.X);V(t.Basis.Y);V(t.Basis.Z);V(t.Origin);}
        void MaterialInfo(Material? m)
        {
            types.Add(m is null?"no-material":m.GetClass()+":"+m.ResourcePath);
            if(m is StandardMaterial3D s){C(s.AlbedoColor);values.Add(s.Roughness);values.Add(s.Metallic);}
        }
        void MeshInfo(Mesh mesh)
        {
            types.Add(mesh.GetClass()+":"+mesh.ResourcePath);
            Aabb a=mesh.GetAabb();V(a.Position);V(a.Size);values.Add(mesh.GetSurfaceCount());
            for(int i=0;i<mesh.GetSurfaceCount();i++)MaterialInfo(mesh.SurfaceGetMaterial(i));
        }
        void Visit(Node node)
        {
            types.Add(node.GetClass().ToString());values.Add(node.GetChildCount());
            if(node is Node3D n)T(n.Transform);
            if(node is GeometryInstance3D g){values.Add((int)g.CastShadow);MaterialInfo(g.MaterialOverride);}
            if(node is MultiMeshInstance3D batch)
            {
                types.Add(batch.Name.ToString());var mm=batch.Multimesh;
                MeshInfo(mm.Mesh);values.Add(mm.InstanceCount);values.Add(mm.UseColors?1:0);
                for(int i=0;i<mm.InstanceCount;i++)
                {T(mm.GetInstanceTransform(i));if(mm.UseColors)C(mm.GetInstanceColor(i));}
            }
            if(node is MeshInstance3D {Mesh:not null} instance)MeshInfo(instance.Mesh);
            foreach(Node child in node.GetChildren())Visit(child);
        }
        Visit(root);return new(types.ToArray(),values.ToArray());
    }
    private static void CompareArt(ArtSignature a,ArtSignature b,string label)
    {
        Require(a.Types.SequenceEqual(b.Types),label+" native foliage types, meshes and materials");
        Require(a.Values.Length==b.Values.Length,label+" foliage numeric lengths");
        for(int i=0;i<a.Values.Length;i++)
            Require(float.IsFinite(b.Values[i]) && Math.Abs(a.Values[i]-b.Values[i])<.002f,
                label+" foliage transform/color "+i);
    }

    private async Task CompareRetainedRenderer(VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        var world=new WorldState(33,25,1315511732);
        for(int z=0;z<25;z++)
        for(int x=0;x<33;x++)world.Paint(x,z,0,(Terrain)((x*3+z*5)%7));
        Bind(terrain,world);
        Rig(terrain,camera,33,25).RestoreReviewPose(new CameraReviewPose(16,12,79));
        terrain.UpdateVisibility(camera,0);
        Require(terrain.SculptElevation(16,12,2,3)>0 && terrain.SculptElevation(32,24,1,-3)>0,
            "parity includes sculpted chunk/coast boundaries");
        NatureArtLibrary art=Field<NatureArtLibrary>(terrain,"_natureArt");
        PackedScene? originalTree=Field<PackedScene?>(terrain,"_treePrefab");
        PackedScene? originalRock=Field<PackedScene?>(terrain,"_rockPrefab");
        using PackedScene optional=OptionalArt();
        foreach(int profile in new[]{0,1,2})
        foreach(bool rich in new[]{false,true})
        foreach(bool facets in new[]{false,true})
        foreach(bool smooth in new[]{false,true})
        {
            SetField(terrain,"_natureArt",profile==1?null:art);
            SetField(terrain,"_treePrefab",profile==2?optional:null);
            SetField(terrain,"_rockPrefab",profile==2?optional:null);
            Format(terrain,rich,facets,smooth);
            terrain.MarkChanged(16,12,40);terrain.UpdateVisibility(camera,0);
            foreach(var key in new[]{(0,0),(1,0),(2,1)})
            {
                Require(Queue(terrain).TryBegin(new(key.Item1,key.Item2),out ChunkRequest request),
                    "reserve current parity generation");
                var snapshot=(ChunkBuildSnapshot)Call(terrain,"CaptureWorkerChunk",request)!;
                Call(terrain,"BuildChunk",key.Item1,key.Item2);
                Call(terrain,"RebuildVegetationChunk",key.Item1,key.Item2);
                Surface expected=ReadSurface(Details(terrain)[key]);
                ArtSignature expectedArt=ReadArt(Foliage(terrain)[key]);
                int expectedProps=Field<Dictionary<(int,int),int>>(terrain,"_propCounts")[key];
                var job=Task.Run(()=>(Data:ChunkMeshBuilder.Build(snapshot),
                    Thread:System.Environment.CurrentManagedThreadId));
                while(!job.IsCompleted)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var result=job.GetAwaiter().GetResult();
                Require(result.Thread!=_owner && System.Environment.CurrentManagedThreadId==_owner,
                    "numeric work is background and native publication is owner-thread");
                Require((bool)Call(terrain,"UploadWorkerChunk",request,result.Data)!,"current native handoff succeeds");
                string label=$"profile {profile}, rich {rich}, facets {facets}, smooth {smooth}, {key}";
                CompareSurface(expected,ReadSurface(Details(terrain)[key]),label);
                CompareArt(expectedArt,ReadArt(Foliage(terrain)[key]),label);
                Require(result.Data.PropCount==expectedProps && terrain.HasViewportDetailedCoverage(key.Item1,key.Item2),
                    label+" logical prop count and published coverage");
                _parityCases++;
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        SetField(terrain,"_natureArt",art);
        SetField(terrain,"_treePrefab",originalTree);SetField(terrain,"_rockPrefab",originalRock);
        Format(terrain,true,true,true);
    }
}
