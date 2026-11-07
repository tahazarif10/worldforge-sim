using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Shared authored trees/rocks and volumetric manual-game ground cover.
/// Full chunk builds and live painting use this same placement/material path.
/// </summary>
public sealed class NatureArtLibrary
{
    public enum Species
    {
        Broadleaf=0,Oak=1,Pine=2,TallPine=3,Plateau=4,Autumn=5,
        Boulder=6,TallRock=7,Grass=8,Flower=9
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
    }
    public readonly record struct Placement(Species Species,Vector3 Position,float Height,float Angle);
    private readonly Model?[] _models=new Model?[AssetNames.Length];
    private WorldState? _naturalWorld;
    private VisualTerrain3D? _naturalTerrain;
    public int LoadedModels { get; }

    public NatureArtLibrary()
    {
        int total=0;
        for(int i=0;i<AssetNames.Length;i++)
        {
            if(NaturalGroundCover.Enabled && i>=8)
            {
                var generated=NaturalGroundCover.CreateModel(i==9);
                Aabb bounds=generated.Mesh.GetAabb();
                var model=new Model
                {
                    LowestY=bounds.Position.Y,
                    Height=MathF.Max(.05f,bounds.Size.Y)
                };
                model.Parts.Add((generated.Mesh,Transform3D.Identity,generated.Material));
                _models[i]=model;
                total++;
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
                _models[i]=model;
                total++;
            }
            finally { root.Free(); }
        }
        LoadedModels=total;
        GD.Print(NaturalGroundCover.Enabled
            ? $"V2 nature meshes ready: {LoadedModels}/{AssetNames.Length}; layered meadow tufts enabled"
            : $"V2 authored Kenney nature models loaded: {LoadedModels}/{AssetNames.Length}");
    }

    internal void ConfigureNaturalGround(WorldState world,VisualTerrain3D terrain)
    {
        _naturalWorld=world;
        _naturalTerrain=terrain;
    }

    public bool Has(Species species)=>_models[(int)species] is not null;
    public int PartCount(Species species)=>_models[(int)species]?.Parts.Count??0;

    public (Mesh Mesh,Material? Override) Part(Species species,int index)
    {
        Model model=_models[(int)species]??
            throw new InvalidOperationException("Nature model was not imported.");
        var part=model.Parts[index];
        return (part.Mesh,part.Override);
    }

    /// <summary>
    /// Chunk and held-paint instances use identical clustering, dimensions
    /// and clear walking-lane anchors. No per-frame randomization or popping.
    /// </summary>
    private bool ResolvePlacement(Placement source,out Placement result)
    {
        result=source;
        if(!NaturalGroundCover.Enabled || source.Species is not (Species.Grass or Species.Flower))
            return true;
        int x=Mathf.FloorToInt(source.Position.X),z=Mathf.FloorToInt(source.Position.Z);
        int seed=_naturalWorld?.Seed??0;
        if(!NaturalGroundCover.IsClusterSite(x,z,seed))return false;
        if(_naturalTerrain is not null && !_naturalTerrain.NaturalPlantAllowedAt(x,z))return false;
        Vector2 anchor=NaturalGroundCover.Anchor(x,z,seed);
        uint hash=EnvironmentArtMath.Hash(x,z,unchecked(seed+551));
        float variation=((hash>>16)&255u)/255f;
        // Slightly taller, fuller groundcover and clearly readable
        // wildflowers. The authored anchor and tile-center walking lanes
        // remain unchanged; only Grass/Flower visuals change.
        float height=source.Species==Species.Grass
            ? .235f+variation*.085f
            : .380f+variation*.110f;
        float ground=_naturalTerrain is null?source.Position.Y:
            _naturalTerrain.SurfaceHeightAt(x,z,anchor.X-x,anchor.Y-z);
        result=new Placement(source.Species,new Vector3(anchor.X,ground,anchor.Y),
            height,(hash&65535u)/65535f*Mathf.Tau);
        return true;
    }

    public Transform3D PlacementTransform(Placement placement,int partIndex)
    {
        Model model=_models[(int)placement.Species]??
            throw new InvalidOperationException("Nature model was not imported.");
        if(!ResolvePlacement(placement,out Placement resolved))
            return new Transform3D(Basis.Identity.Scaled(Vector3.One*.00001f),
                placement.Position-Vector3.Up*4f);
        return MakePlacementTransform(model,resolved,model.Parts[partIndex].Local);
    }

    private static Transform3D MakePlacementTransform(Model model,Placement placement,Transform3D partLocal)
    {
        float scale=Mathf.Clamp(placement.Height/model.Height,.15f,9f);
        Basis basis=new Basis(Vector3.Up,placement.Angle).Scaled(Vector3.One*scale);
        Vector3 origin=placement.Position-Vector3.Up*(model.LowestY*scale);
        return new Transform3D(basis,origin)*partLocal;
    }

    public int Populate(Node3D chunk,IReadOnlyList<Placement> placements)
    {
        var groups=new Dictionary<Species,List<Placement>>();
        foreach(Placement original in placements)
        {
            if(!Has(original.Species) || !ResolvePlacement(original,out Placement p))continue;
            if(!groups.TryGetValue(p.Species,out var list))
            {
                list=new List<Placement>();groups.Add(p.Species,list);
            }
            list.Add(p);
        }
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
                var node=forestTree
                    ? new TreeResourceBatch{Sites=sites,RestTransforms=originals,Pivots=pivots}
                    : new MultiMeshInstance3D();
                node.Name=$"CC0_{group.Key}_{partIndex}";
                node.Multimesh=batch;node.MaterialOverride=part.Override;
                node.CastShadow=group.Key is Species.Grass or Species.Flower
                    ? GeometryInstance3D.ShadowCastingSetting.Off
                    : GeometryInstance3D.ShadowCastingSetting.On;
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
                model.LowestY=MathF.Min(model.LowestY,y);
                model.Height=MathF.Max(model.Height,y);
            }
        }
        foreach(Node child in node.GetChildren())CollectParts(child,relative,model);
    }
}
