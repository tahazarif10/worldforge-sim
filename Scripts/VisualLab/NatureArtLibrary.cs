using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Original tree/rock imports plus four shared understory models. One
/// deterministic placement function serves full chunks and held painting.
/// </summary>
public sealed class NatureArtLibrary
{
    // Existing authored IDs stay stable; only decorative families are added.
    public enum Species
    {
        Broadleaf=0,Oak=1,Pine=2,TallPine=3,Plateau=4,Autumn=5,
        Boulder=6,TallRock=7,Grass=8,Flower=9,Shrub=10,FlowerAccent=11
    }
    private static readonly string[] AssetNames=
    [
        "tree-detailed","tree-oak","tree-pinedefaulta",
        "tree-pinetallb-detailed","tree-plateau","tree-default-fall",
        "rock-largea","rock-talla","grass-leafs","flower-yellowb"
    ];
    private sealed class Model
    {
        public readonly List<(Mesh Mesh,Transform3D Local,Material? Override)> Parts=new();
        public float LowestY=float.PositiveInfinity;
        public float Height;
        public float Radius;
    }
    // Radius=0 preserves every pre-existing authored-tree/rock caller.
    public readonly record struct Placement(Species Species,Vector3 Position,float Height,
        float Angle,float Radius=0f);
    private readonly Model?[] _models=new Model?[12];
    private WorldState? _naturalWorld;
    private VisualTerrain3D? _naturalTerrain;
    public int LoadedModels { get; }

    public NatureArtLibrary()
    {
        int total=0;
        Material? groundMaterial=NaturalGroundCover.Enabled?NaturalGroundCover.CreateMaterial():null;
        int modelCount=NaturalGroundCover.Enabled?_models.Length:AssetNames.Length;
        for(int i=0;i<modelCount;i++)
        {
            if(NaturalGroundCover.Enabled && i>=8)
            {
                var generated=NaturalGroundCover.CreateModel((NaturalGroundCover.Family)(i-8),groundMaterial);
                Aabb bounds=generated.Mesh.GetAabb();
                var model=new Model
                {
                    LowestY=bounds.Position.Y,Height=MathF.Max(.05f,bounds.Size.Y),
                    Radius=MathF.Max(.01f,generated.Radius)
                };
                model.Parts.Add((generated.Mesh,Transform3D.Identity,generated.Material));
                _models[i]=model;total++;
                continue;
            }
            string path=$"res://Assets/Art/KenneyNature/{AssetNames[i]}.glb";
            if(!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"V2 authored model not imported: {path}. Run editor --import on H:.");
                continue;
            }
            PackedScene? scene=GD.Load<PackedScene>(path);
            if(scene is null)continue;
            Node root=scene.Instantiate();
            try
            {
                var model=new Model();
                CollectParts(root,Transform3D.Identity,model);
                if(model.Parts.Count==0 || !float.IsFinite(model.LowestY))
                {
                    GD.PushWarning($"V2 authored GLB contains no meshes: {path}");
                    continue;
                }
                model.Height=MathF.Max(.05f,model.Height-model.LowestY);
                _models[i]=model;total++;
            }
            finally { root.Free(); }
        }
        LoadedModels=total;
        GD.Print(NaturalGroundCover.Enabled
            ? $"V2 nature meshes ready: {LoadedModels}/{modelCount}; four clustered understory families"
            : $"V2 authored Kenney nature models loaded: {LoadedModels}/{AssetNames.Length}");
    }

    internal void ConfigureNaturalGround(WorldState world,VisualTerrain3D terrain)
    {
        _naturalWorld=world;_naturalTerrain=terrain;
    }

    public static bool IsGroundCover(Species species)=>species is Species.Grass or
        Species.Flower or Species.Shrub or Species.FlowerAccent;
    public static bool IsSmallGroundCover(Species species)=>species is Species.Grass or
        Species.Flower or Species.FlowerAccent;
    public bool Has(Species species)=>(uint)species<(uint)_models.Length && _models[(int)species] is not null;
    public int PartCount(Species species)=>Has(species)?_models[(int)species]!.Parts.Count:0;
    public (Mesh Mesh,Material? Override) Part(Species species,int index)
    {
        Model model=_models[(int)species]??throw new InvalidOperationException("Nature model was not imported.");
        var part=model.Parts[index];
        return (part.Mesh,part.Override);
    }

    /// <summary>
    /// One stable clump per accepted cell. Evaluate every eligible cell once,
    /// rather than filtering the old 25%-density decorative scatter again.
    /// Forest gaps use this path too, but never replace a tree or stump.
    /// </summary>
    internal bool TryGroundCoverPlacement(int x,int z,out Placement result)
    {
        result=default;
        WorldState? world=_naturalWorld;
        VisualTerrain3D? terrain=_naturalTerrain;
        if(!NaturalGroundCover.Enabled || world is null || terrain is null ||
            !world.Contains(x,z))return false;
        Terrain kind=world.Tile(x,z);
        if(kind is not (Terrain.Grass or Terrain.Forest) ||
            !terrain.NaturalPlantAllowedAt(x,z))return false;
        uint treeHash=EnvironmentArtMath.Hash(x,z,unchecked(world.Seed+119));
        if(kind==Terrain.Forest && (NatureScatter.Pick(kind,treeHash)>=0 ||
            world.Resources.HasStump(x,z)))return false;

        int neighboringForest=0;
        for(int dz=-1;dz<=1;dz++)
        for(int dx=-1;dx<=1;dx++)
            if((dx!=0 || dz!=0) && world.Contains(x+dx,z+dz) &&
                world.Tile(x+dx,z+dz)==Terrain.Forest)neighboringForest++;
        float edge=kind==Terrain.Forest?1f:MathF.Min(1f,neighboringForest*.25f);
        if(!NaturalGroundCover.TryChoose(x,z,world.Seed,edge,out var chosen))return false;
        Species species=(Species)(8+(int)chosen.Family);
        if(!Has(species))return false;

        for(int attempt=0;attempt<4;attempt++)
        {
            Vector2 anchor=NaturalGroundCover.PlantAnchor(x,z,world.Seed,attempt);
            // Bound actual mesh radius independently of height. A taller
            // shrub must not silently grow back across center-to-center
            // walking lanes. Every rotation fits this measured radial bound.
            float distance=MathF.Min(MathF.Abs(anchor.X-(x+.5f)),MathF.Abs(anchor.Y-(z+.5f)));
            float radius=MathF.Min(chosen.Radius,distance-.235f-.010f);
            if(radius<.12f)continue;
            if(!HasGroundClearance(anchor,radius,world,terrain,out float ground))continue;
            result=new Placement(species,new Vector3(anchor.X,ground,anchor.Y),
                chosen.Height,chosen.Angle,radius);
            return true;
        }
        return false;
    }

    private static bool HasGroundClearance(Vector2 anchor,float radius,WorldState world,
        VisualTerrain3D terrain,out float height)
    {
        height=0f;
        int x=(int)MathF.Floor(anchor.X),z=(int)MathF.Floor(anchor.Y);
        height=terrain.SurfaceHeightAt(x,z,anchor.X-x,anchor.Y-z);
        // Canopy extends beyond its owner tile. Reject cliff/water/house and
        // berry intersections at that actual extent, not just the center.
        for(int dz=-1;dz<=1;dz++)
        for(int dx=-1;dx<=1;dx++)
        {
            float px=anchor.X+dx*radius,pz=anchor.Y+dz*radius;
            int tx=(int)MathF.Floor(px),tz=(int)MathF.Floor(pz);
            if(!world.Contains(tx,tz) || world.Tile(tx,tz) is not
                (Terrain.Grass or Terrain.Forest) || !terrain.NaturalPlantAllowedAt(tx,tz))return false;
            if(MathF.Abs(terrain.SurfaceHeightAt(tx,tz,px-tx,pz-tz)-height)>.22f)return false;
        }
        // Keep mature tree trunks visible; woodland understory belongs in
        // nearby gaps. This reads original deterministic tree positions only.
        for(int dz=-1;dz<=1;dz++)
        for(int dx=-1;dx<=1;dx++)
        {
            int tx=x+dx,tz=z+dz;
            if(!world.Contains(tx,tz) || world.Tile(tx,tz)!=Terrain.Forest ||
                world.Resources.HasStump(tx,tz))continue;
            uint h=EnvironmentArtMath.Hash(tx,tz,unchecked(world.Seed+119));
            if(NatureScatter.Pick(Terrain.Forest,h)<0)continue;
            float cx=tx+((h>>10)%100)/310f+.34f;
            float cz=tz+((h>>18)%100)/310f+.34f;
            float ax=anchor.X-cx,az=anchor.Y-cz;
            float clearance=radius+.12f;
            if(ax*ax+az*az<clearance*clearance)return false;
        }
        return true;
    }

    private bool ResolvePlacement(Placement source,out Placement result)
    {
        result=source;
        if(!NaturalGroundCover.Enabled || !IsGroundCover(source.Species))return true;
        // Canonical placements already carry a measured radius. Do not
        // resample their shifted coordinates or apply the size twice.
        if(source.Radius>0f)return true;
        return TryGroundCoverPlacement(Mathf.FloorToInt(source.Position.X),
            Mathf.FloorToInt(source.Position.Z),out result);
    }

    public Transform3D PlacementTransform(Placement placement,int partIndex)
    {
        if(!ResolvePlacement(placement,out Placement resolved))
            return new Transform3D(Basis.Identity.Scaled(Vector3.One*.00001f),placement.Position-Vector3.Up*4f);
        Model model=_models[(int)resolved.Species]??throw new InvalidOperationException("Nature model was not imported.");
        return MakePlacementTransform(model,resolved,model.Parts[partIndex].Local);
    }

    private static Transform3D MakePlacementTransform(Model model,Placement p,Transform3D local)
    {
        float vertical=Mathf.Clamp(p.Height/model.Height,.15f,9f);
        float horizontal=p.Radius>0f && model.Radius>0f?p.Radius/model.Radius:vertical;
        Basis basis=new Basis(Vector3.Up,p.Angle).Scaled(new Vector3(horizontal,vertical,horizontal));
        Vector3 origin=p.Position-Vector3.Up*(model.LowestY*vertical);
        return new Transform3D(basis,origin)*local;
    }

    public int Populate(Node3D chunk,IReadOnlyList<Placement> placements)
    {
        var groups=new Dictionary<Species,List<Placement>>();
        void Add(Placement p)
        {
            if(!groups.TryGetValue(p.Species,out var list))
            {
                list=new List<Placement>();groups.Add(p.Species,list);
            }
            list.Add(p);
        }
        Rect2I bounds=default;
        bool canonical=NaturalGroundCover.Enabled && _naturalTerrain is not null &&
            _naturalTerrain.TryGetUndergrowthBounds(chunk,placements,out bounds);
        foreach(Placement original in placements)
        {
            if(canonical && IsGroundCover(original.Species))continue;
            if(Has(original.Species) && ResolvePlacement(original,out Placement p))Add(p);
        }
        if(canonical)
            for(int z=bounds.Position.Y;z<bounds.End.Y;z++)
            for(int x=bounds.Position.X;x<bounds.End.X;x++)
                if(TryGroundCoverPlacement(x,z,out Placement p))Add(p);

        int count=0;
        foreach(var group in groups)
        {
            Model model=_models[(int)group.Key]!;
            count+=group.Value.Count;
            for(int partIndex=0;partIndex<model.Parts.Count;partIndex++)
            {
                var part=model.Parts[partIndex];
                var batch=new MultiMesh
                {
                    TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh=part.Mesh,InstanceCount=group.Value.Count
                };
                bool forestTree=(int)group.Key<=(int)Species.Autumn;
                var sites=forestTree?new Vector2I[group.Value.Count]:[];
                var originals=forestTree?new Transform3D[group.Value.Count]:[];
                var pivots=forestTree?new Vector3[group.Value.Count]:[];
                for(int i=0;i<group.Value.Count;i++)
                {
                    Placement p=group.Value[i];
                    Transform3D transform=MakePlacementTransform(model,p,part.Local);
                    batch.SetInstanceTransform(i,transform);
                    if(forestTree)
                    {
                        sites[i]=new Vector2I(Mathf.FloorToInt(p.Position.X),Mathf.FloorToInt(p.Position.Z));
                        originals[i]=transform;pivots[i]=p.Position;
                    }
                }
                var node=forestTree?new TreeResourceBatch{Sites=sites,RestTransforms=originals,Pivots=pivots}
                    :new MultiMeshInstance3D();
                node.Name=$"CC0_{group.Key}_{partIndex}";
                node.Multimesh=batch;node.MaterialOverride=part.Override;
                node.CastShadow=IsSmallGroundCover(group.Key)?GeometryInstance3D.ShadowCastingSetting.Off:
                    GeometryInstance3D.ShadowCastingSetting.On;
                chunk.AddChild(node);
            }
        }
        return count;
    }

    private static void CollectParts(Node node,Transform3D relative,Model model)
    {
        if(node is Node3D node3D)relative*=node3D.Transform;
        if(node is MeshInstance3D{Mesh:not null} instance)
        {
            Mesh mesh=instance.Mesh;
            model.Parts.Add((mesh,relative,instance.MaterialOverride));
            Aabb bounds=mesh.GetAabb();
            for(int dx=0;dx<=1;dx++)
            for(int dy=0;dy<=1;dy++)
            for(int dz=0;dz<=1;dz++)
            {
                Vector3 point=bounds.Position+new Vector3(bounds.Size.X*dx,bounds.Size.Y*dy,bounds.Size.Z*dz);
                float y=(relative*point).Y;
                model.LowestY=MathF.Min(model.LowestY,y);model.Height=MathF.Max(model.Height,y);
            }
        }
        foreach(Node child in node.GetChildren())CollectParts(child,relative,model);
    }
}
