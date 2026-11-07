using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Batched harvestable berries and stumps. Manual-game berries use subdued
/// foliage, a narrow volumetric silhouette, and an off-center gathering spot.
/// Resource site IDs, amounts, AI goals and persistence are unchanged.
/// </summary>
public sealed partial class ResourceArt3D : Node3D
{
    private readonly List<(ResourceSite Site,Vector3 Ground)> _visible=new();
    private readonly List<Vector3> _stumpOrigins=new();
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private Godot.Camera3D? _camera;
    private MultiMesh _leaves=null!,_berries=null!,_stumps=null!,_stumpTops=null!;
    private double _refreshElapsed;
    private bool _brushResourcesDirty;
    private const float MarginPixels=96f;
    private static readonly Color[] LegacyLeaves=[new("#3d8051"),new("#549b61"),new("#6ba96a")];
    private static readonly Color[] LegacyFruit=[new("#b93c48"),new("#d94e52"),new("#a52b42"),new("#e47d5b")];
    private static readonly Color[] NaturalLeaves=[new("#345D34"),new("#4D7540"),new("#668849")];
    private static readonly Color[] NaturalFruit=[new("#8F3543"),new("#AD4551"),new("#73313E"),new("#B76959")];
    private static readonly Vector3[] LeafOffsets=
    [new(-.13f,.23f,.015f),new(.13f,.27f,-.04f),new(.015f,.35f,.12f)];
    private static readonly Vector3[] FruitOffsets=
    [new(-.14f,.33f,.18f),new(.14f,.40f,.095f),new(.08f,.41f,-.17f),new(-.07f,.49f,-.045f)];

    public int VisibleBushCount=>_visible.Count;
    public int VisibleBerryCount { get; private set; }
    public int VisibleStumpCount=>_stumpOrigins.Count;

    public override void _Ready()
    {
        Name="BerryResources";
        var plantMaterial=new StandardMaterial3D
        {
            VertexColorUseAsAlbedo=true,AlbedoColor=Colors.White,Roughness=.94f,Metallic=0f
        };
        var bushMesh=new SphereMesh{Radius=.5f,Height=1f,RadialSegments=7,Rings=3};
        var fruitMesh=new SphereMesh{Radius=.5f,Height=1f,RadialSegments=6,Rings=3};
        _leaves=new MultiMesh
        {
            TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            UseColors=true,Mesh=bushMesh,InstanceCount=0
        };
        _berries=new MultiMesh
        {
            TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            UseColors=true,Mesh=fruitMesh,InstanceCount=0
        };
        _stumps=new MultiMesh
        {
            TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            Mesh=new CylinderMesh{Height=.23f,TopRadius=.14f,BottomRadius=.19f,RadialSegments=7},
            InstanceCount=0
        };
        _stumpTops=new MultiMesh
        {
            TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            Mesh=new CylinderMesh{Height=.013f,TopRadius=.143f,BottomRadius=.143f,RadialSegments=7},
            InstanceCount=0
        };
        AddChild(new MultiMeshInstance3D
        {
            Name="BerryBushLeaves",Multimesh=_leaves,MaterialOverride=plantMaterial,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
        });
        AddChild(new MultiMeshInstance3D
        {
            Name="BerryFruit",Multimesh=_berries,MaterialOverride=plantMaterial,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
        });
        AddChild(new MultiMeshInstance3D
        {
            Name="HarvestedTreeStumps",Multimesh=_stumps,
            MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("#754831"),Roughness=.96f},
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
        });
        AddChild(new MultiMeshInstance3D
        {
            Name="StumpCutFaces",Multimesh=_stumpTops,
            MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("#c49d70"),Roughness=.96f},
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    public void BindWorld(WorldState world,VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        _world=world;_terrain=terrain;_camera=camera;
        _refreshElapsed=0;_brushResourcesDirty=false;
        RefreshNow();
    }
    public void RequestInteractiveBiomeRefresh()=>_brushResourcesDirty=true;
    public void Tick(double delta)
    {
        _refreshElapsed+=Math.Clamp(delta,0,.1);
        if(_refreshElapsed<(_brushResourcesDirty?.05:.15))return;
        _refreshElapsed=0;_brushResourcesDirty=false;RefreshNow();
    }

    public void RefreshNow()
    {
        if(_world is null || _terrain is null || _camera is null)return;
        Vector2 size=_camera.GetViewport().GetVisibleRect().Size;
        if(size.X<1f || size.Y<1f)return;
        _visible.Clear();_stumpOrigins.Clear();
        foreach(ResourceSite stump in _world.Resources.StumpSites())
        {
            uint hash=EnvironmentArtMath.Hash(stump.X,stump.Y,unchecked(_world.Seed+119));
            float ox=((hash>>10)%100)/310f+.34f,oz=((hash>>18)%100)/310f+.34f;
            float x=stump.X+ox,z=stump.Y+oz;
            if(!InView(new Vector3(x,.8f,z),size))continue;
            float height=_terrain.SurfaceHeightAt(stump.X,stump.Y,ox,oz);
            _stumpOrigins.Add(new Vector3(x,height,z));
        }
        if(_stumps.InstanceCount!=_stumpOrigins.Count)_stumps.InstanceCount=_stumpOrigins.Count;
        if(_stumpTops.InstanceCount!=_stumpOrigins.Count)_stumpTops.InstanceCount=_stumpOrigins.Count;
        for(int i=0;i<_stumpOrigins.Count;i++)
        {
            Vector3 p=_stumpOrigins[i];
            _stumps.SetInstanceTransform(i,new Transform3D(Basis.Identity,p+Vector3.Up*.115f));
            _stumpTops.SetInstanceTransform(i,new Transform3D(Basis.Identity,p+Vector3.Up*.235f));
        }

        bool natural=NaturalGroundCover.Enabled;
        foreach(ResourceSite site in _world.Resources.FoodSites())
        {
            Vector2 anchor=natural?NaturalGroundCover.Anchor(site.X,site.Y,_world.Seed):
                new Vector2(site.X+.5f,site.Y+.5f);
            if(!InView(new Vector3(anchor.X,.9f,anchor.Y),size))continue;
            float ground=_terrain.SurfaceHeightAt(site.X,site.Y,anchor.X-site.X,anchor.Y-site.Y);
            _visible.Add((site,new Vector3(anchor.X,ground,anchor.Y)));
        }
        int leafCount=checked(_visible.Count*3),fruitCount=0;
        foreach(var item in _visible)fruitCount+=Math.Min(item.Site.Remaining,4);
        if(_leaves.InstanceCount!=leafCount)_leaves.InstanceCount=leafCount;
        if(_berries.InstanceCount!=fruitCount)_berries.InstanceCount=fruitCount;
        VisibleBerryCount=fruitCount;

        Color[] leaves=natural?NaturalLeaves:LegacyLeaves;
        Color[] fruit=natural?NaturalFruit:LegacyFruit;
        // Narrow X/Z footprint reserves center walking lanes. Keep enough
        // vertical silhouette to read as a bush, not a flat marker.
        Vector3 shape=natural?new Vector3(.40f,.72f,.40f):Vector3.One;
        int leafIndex=0,berryIndex=0;
        foreach(var item in _visible)
        {
            for(int i=0;i<3;i++)
            {
                Vector3 scale=new Vector3(.29f,.26f+i*.02f,.29f)*shape;
                _leaves.SetInstanceTransform(leafIndex,
                    new Transform3D(Basis.Identity.Scaled(scale),item.Ground+LeafOffsets[i]*shape));
                _leaves.SetInstanceColor(leafIndex,leaves[i]);leafIndex++;
            }
            for(int i=0;i<Math.Min(item.Site.Remaining,4);i++)
            {
                Vector3 fruitSize=natural?Vector3.One*.048f:Vector3.One*.11f;
                _berries.SetInstanceTransform(berryIndex,
                    new Transform3D(Basis.Identity.Scaled(fruitSize),item.Ground+FruitOffsets[i]*shape));
                _berries.SetInstanceColor(berryIndex,fruit[i]);berryIndex++;
            }
        }
    }

    private bool InView(Vector3 point,Vector2 size)
    {
        if(_camera is null || _camera.IsPositionBehind(point))return false;
        Vector2 projected=_camera.UnprojectPosition(point);
        return projected.X>=-MarginPixels && projected.Y>=-MarginPixels &&
            projected.X<=size.X+MarginPixels && projected.Y<=size.Y+MarginPixels;
    }
}
