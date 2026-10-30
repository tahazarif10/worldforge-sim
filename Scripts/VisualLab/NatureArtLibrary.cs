using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// Owns material-preserving authored GLB mesh parts. Imported models are read
/// once and each visible chunk creates MultiMesh batches, never one GLB scene
/// node per tree. Kenney Nature Kit files are CC0; source manifest is in
/// Assets/Art/KenneyNature/README.md.
/// </summary>
public sealed class NatureArtLibrary
{
    public enum Species
    {
        Broadleaf = 0, Oak = 1, Pine = 2, TallPine = 3,
        Plateau = 4, Autumn = 5, Boulder = 6, TallRock = 7,
        Grass = 8, Flower = 9
    }

    private static readonly string[] AssetNames =
    [
        "tree-detailed", "tree-oak", "tree-pinedefaulta",
        "tree-pinetallb-detailed", "tree-plateau", "tree-default-fall",
        "rock-largea", "rock-talla", "grass-leafs", "flower-yellowb"
    ];

    private sealed class Model
    {
        public readonly List<(Mesh Mesh, Transform3D Local)> Parts = new();
        public float LowestY = float.PositiveInfinity;
        public float Height;
    }

    public readonly record struct Placement(
        Species Species, Vector3 Position, float Height, float Angle);

    private readonly Model?[] _models = new Model?[AssetNames.Length];
    public int LoadedModels { get; }

    public NatureArtLibrary()
    {
        int total = 0;
        for (int i = 0; i < AssetNames.Length; i++)
        {
            string path = $"res://Assets/Art/KenneyNature/{AssetNames[i]}.glb";
            if (!ResourceLoader.Exists(path))
            {
                GD.PushWarning($"V2 authored model not imported: {path}. Run headless editor --import on H:.");
                continue;
            }
            PackedScene? scene = GD.Load<PackedScene>(path);
            if (scene is null) continue;
            Node root = scene.Instantiate();
            try
            {
                var model = new Model();
                CollectParts(root, Transform3D.Identity, model);
                if (model.Parts.Count == 0 || !float.IsFinite(model.LowestY))
                {
                    GD.PushWarning($"V2 authored GLB contains no meshes: {path}");
                    continue;
                }
                model.Height = MathF.Max(0.05f, model.Height - model.LowestY);
                _models[i] = model;
                total++;
            }
            finally { root.Free(); }
        }
        LoadedModels = total;
        GD.Print($"V2 authored Kenney nature models loaded: {LoadedModels}/{AssetNames.Length}");
    }

    public bool Has(Species species) => _models[(int)species] is not null;

    /// <summary>Instantiates GLB geometry as spatially culled per-chunk batches.</summary>
    public int Populate(Node3D chunk, IReadOnlyList<Placement> placements)
    {
        var groups = new Dictionary<Species, List<Placement>>();
        foreach (Placement placement in placements)
        {
            if (!Has(placement.Species)) continue;
            if (!groups.TryGetValue(placement.Species, out var list))
            {
                list = new List<Placement>();
                groups.Add(placement.Species, list);
            }
            list.Add(placement);
        }

        int count = 0;
        foreach (var group in groups)
        {
            Model model = _models[(int)group.Key]!;
            count += group.Value.Count;
            for (int partIndex = 0; partIndex < model.Parts.Count; partIndex++)
            {
                var part = model.Parts[partIndex];
                var batch = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh = part.Mesh,
                    InstanceCount = group.Value.Count
                };

                for (int i = 0; i < group.Value.Count; i++)
                {
                    Placement p = group.Value[i];
                    float scale = Mathf.Clamp(p.Height / model.Height, 0.15f, 9f);
                    Basis basis = new Basis(Vector3.Up, p.Angle).Scaled(Vector3.One * scale);
                    Vector3 origin = p.Position - Vector3.Up * (model.LowestY * scale);
                    batch.SetInstanceTransform(i,
                        new Transform3D(basis, origin) * part.Local);
                }

                chunk.AddChild(new MultiMeshInstance3D
                {
                    Name = $"CC0_{group.Key}_{partIndex}",
                    Multimesh = batch,
                    CastShadow = group.Key is Species.Grass or Species.Flower
                        ? GeometryInstance3D.ShadowCastingSetting.Off
                        : GeometryInstance3D.ShadowCastingSetting.On
                });
            }
        }
        return count;
    }

    private static void CollectParts(Node node, Transform3D relative, Model model)
    {
        if (node is Node3D node3D)
            relative *= node3D.Transform;

        if (node is MeshInstance3D { Mesh: not null } instance)
        {
            Mesh mesh = instance.Mesh;
            model.Parts.Add((mesh, relative));
            Aabb bounds = mesh.GetAabb();
            // Include all eight transformed corners when normalizing GLB
            // model scale/pivot; a rotated or offset child remains grounded.
            for (int dx = 0; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
            for (int dz = 0; dz <= 1; dz++)
            {
                Vector3 point = bounds.Position + new Vector3(
                    bounds.Size.X * dx, bounds.Size.Y * dy, bounds.Size.Z * dz);
                float y = (relative * point).Y;
                model.LowestY = MathF.Min(model.LowestY, y);
                model.Height = MathF.Max(model.Height, y);
            }
        }

        foreach (Node child in node.GetChildren())
            CollectParts(child, relative, model);
    }
}
