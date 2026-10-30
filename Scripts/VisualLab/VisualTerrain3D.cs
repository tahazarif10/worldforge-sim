using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Experimental three-dimensional terrain for side-by-side design review.
/// Uses real elevation, cliffs, sunlight, water and batched trees/rocks.
/// Deliberately does not modify the existing 2D game's root scene.
/// </summary>
public partial class VisualTerrain3D : Node3D
{
    private const int ChunkSize = 16;
    private static readonly Color[] TerrainColors =
    [
        new("#28516a"), new("#54a7ba"), new("#e0c996"), new("#86ba64"),
        new("#426e51"), new("#89979f"), new("#63534c")
    ];

    private readonly Dictionary<(int X, int Y), MeshInstance3D> _chunks = new();
    private WorldState _world = null!;
    private Node3D _vegetation = null!;
    private MeshInstance3D _water = null!;
    private readonly HashSet<(int X, int Y)> _changedChunks = new();
    private bool _vegetationDirty;

    private readonly StandardMaterial3D _earthMaterial = new()
    {
        VertexColorUseAsAlbedo = true,
        Roughness = 0.92f,
        Metallic = 0f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private readonly StandardMaterial3D _propMaterial = new()
    {
        VertexColorUseAsAlbedo = true,
        AlbedoColor = Colors.White,
        Roughness = 0.93f,
        Metallic = 0f
    };

    public int LastPropCount { get; private set; }

    public void BindWorld(WorldState world)
    {
        _world = world;
        foreach (MeshInstance3D mesh in _chunks.Values) mesh.QueueFree();
        _chunks.Clear();
        _changedChunks.Clear();

        _vegetation ??= new Node3D { Name = "FoliageBatches" };
        if (_vegetation.GetParent() is null) AddChild(_vegetation);

        if (_water is null)
        {
            var shader = GD.Load<Shader>("res://Assets/Shaders/stylized_water.gdshader");
            _water = new MeshInstance3D
            {
                Name = "AnimatedWater",
                MaterialOverride = new ShaderMaterial { Shader = shader },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_water);
        }
        _water.Mesh = new PlaneMesh { Size = new Vector2(world.Width, world.Height) };
        _water.Position = new Vector3(world.Width * 0.5f, 0.12f, world.Height * 0.5f);

        for (int y = 0; y < world.Height; y += ChunkSize)
        for (int x = 0; x < world.Width; x += ChunkSize)
            BuildChunk(x / ChunkSize, y / ChunkSize);

        RebuildVegetation();
    }

    /// <summary>Defer all geometry changes until end of simulation frame.</summary>
    public void MarkChanged(int x, int y, int radius)
    {
        if (_world is null) return;
        int left = Math.Max(0, x - radius - 1);
        int top = Math.Max(0, y - radius - 1);
        int right = Math.Min(_world.Width - 1, x + radius + 1);
        int bottom = Math.Min(_world.Height - 1, y + radius + 1);
        for (int cy = top / ChunkSize; cy <= bottom / ChunkSize; cy++)
        for (int cx = left / ChunkSize; cx <= right / ChunkSize; cx++)
            _changedChunks.Add((cx, cy));
        _vegetationDirty = true;
    }

    public void FlushChanges()
    {
        if (_changedChunks.Count == 0 && !_vegetationDirty) return;
        foreach (var (x, y) in _changedChunks) BuildChunk(x, y);
        _changedChunks.Clear();
        if (_vegetationDirty)
        {
            _vegetationDirty = false;
            RebuildVegetation();
        }
    }

    public float HeightAt(int x, int y)
    {
        if (!_world.Contains(x, y)) return 0.12f;
        Terrain tile = _world.Tile(x, y);
        return tile switch
        {
            Terrain.DeepWater => 0.12f,
            Terrain.ShallowWater => 0.12f,
            Terrain.Sand => 0.29f,
            Terrain.Grass => 0.61f,
            Terrain.Forest => 0.68f,
            Terrain.Burned => 0.53f,
            Terrain.Mountain => 2.6f + (Hash(x, y, _world.Seed) % 11) * 0.17f,
            _ => 0.6f
        };
    }

    private void BuildChunk(int cx, int cy)
    {
        var key = (cx, cy);
        if (!_chunks.TryGetValue(key, out MeshInstance3D? instance))
        {
            instance = new MeshInstance3D { Name = $"TerrainChunk_{cx}_{cy}" };
            _chunks[key] = instance;
            AddChild(instance);
        }

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(_earthMaterial);
        bool any = false;
        int xMax = Math.Min(_world.Width, (cx + 1) * ChunkSize);
        int yMax = Math.Min(_world.Height, (cy + 1) * ChunkSize);

        for (int y = cy * ChunkSize; y < yMax; y++)
        for (int x = cx * ChunkSize; x < xMax; x++)
        {
            Terrain tile = _world.Tile(x, y);
            if (tile is Terrain.DeepWater or Terrain.ShallowWater) continue;
            any = true;
            float h = HeightAt(x, y);
            float variation = ((int)(Hash(x, y, _world.Seed + 57) % 17) - 8) / 95f;
            Color baseColor = TerrainColors[(int)tile];
            Color col = variation >= 0 ? baseColor.Lightened(variation) : baseColor.Darkened(-variation);
            Vector3 a = new(x, h, y), b = new(x, h, y + 1);
            Vector3 c = new(x + 1, h, y), d = new(x + 1, h, y + 1);
            AddQuad(surface, a, b, c, d, col, Vector3.Up);

            // Real vertical cliff faces, darker beneath the upper terrain.
            Color rock = tile == Terrain.Mountain ? new Color("#5a5d62") :
                         tile == Terrain.Sand ? new Color("#b89c69") : new Color("#77654e");
            float west = x > 0 ? HeightAt(x - 1, y) : 0.12f;
            float east = x < _world.Width - 1 ? HeightAt(x + 1, y) : 0.12f;
            float north = y > 0 ? HeightAt(x, y - 1) : 0.12f;
            float south = y < _world.Height - 1 ? HeightAt(x, y + 1) : 0.12f;
            if (h > west + 0.02f)
                AddQuad(surface, new(x, west, y), new(x, west, y + 1),
                    new(x, h, y), new(x, h, y + 1), rock, Vector3.Left);
            if (h > east + 0.02f)
                AddQuad(surface, new(x + 1, east, y + 1), new(x + 1, east, y),
                    new(x + 1, h, y + 1), new(x + 1, h, y), rock, Vector3.Right);
            if (h > north + 0.02f)
                AddQuad(surface, new(x + 1, north, y), new(x, north, y),
                    new(x + 1, h, y), new(x, h, y), rock, Vector3.Back);
            if (h > south + 0.02f)
                AddQuad(surface, new(x, south, y + 1), new(x + 1, south, y + 1),
                    new(x, h, y + 1), new(x + 1, h, y + 1), rock, Vector3.Forward);

            // Narrow pale tide line on the land side of the coast.
            if (x > 0 && IsWater(_world.Tile(x - 1, y)))
                AddQuad(surface,
                    new(x, h + 0.012f, y), new(x, h + 0.012f, y + 1),
                    new(x + 0.12f, h + 0.012f, y), new(x + 0.12f, h + 0.012f, y + 1),
                    new Color("#f5e4b2"), Vector3.Up);
            if (y > 0 && IsWater(_world.Tile(x, y - 1)))
                AddQuad(surface,
                    new(x, h + 0.012f, y), new(x, h + 0.012f, y + 0.12f),
                    new(x + 1, h + 0.012f, y), new(x + 1, h + 0.012f, y + 0.12f),
                    new Color("#f5e4b2"), Vector3.Up);
        }

        instance.Visible = any;
        if (any) instance.Mesh = surface.Commit();
        else instance.Mesh = null;
    }

    private static void AddQuad(SurfaceTool surface,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 normal)
    {
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
        surface.AddVertex(c);
        surface.AddVertex(b);
        surface.AddVertex(d);
    }

    private void RebuildVegetation()
    {
        foreach (Node child in _vegetation.GetChildren()) child.QueueFree();

        // Optional curated CC0 GLB models can replace limited preview props.
        // Without imported assets, all geometry stays procedural and H:-safe.
        PackedScene? treePrefab = ResourceLoader.Exists("res://Assets/Nature/Tree.glb")
            ? GD.Load<PackedScene>("res://Assets/Nature/Tree.glb") : null;
        PackedScene? rockPrefab = ResourceLoader.Exists("res://Assets/Nature/Rock.glb")
            ? GD.Load<PackedScene>("res://Assets/Nature/Rock.glb") : null;
        int importedTreeCount = 0, importedRockCount = 0;
        const int MaxImportedTrees = 140, MaxImportedRocks = 80;

        var trunks = new List<(Transform3D Transform, Color Color)>();
        var canopies = new List<(Transform3D Transform, Color Color)>();
        var upperCanopies = new List<(Transform3D Transform, Color Color)>();
        var stones = new List<(Transform3D Transform, Color Color)>();
        var shrubs = new List<(Transform3D Transform, Color Color)>();

        int count = 0;
        for (int y = 0; y < _world.Height; y++)
        for (int x = 0; x < _world.Width; x++)
        {
            Terrain kind = _world.Tile(x, y);
            uint h = Hash(x, y, _world.Seed + 119);
            float offsetX = ((h >> 10) % 100) / 310f + 0.34f;
            float offsetZ = ((h >> 18) % 100) / 310f + 0.34f;
            float ground = HeightAt(x, y);
            Vector3 p = new(x + offsetX, ground, y + offsetZ);

            if (kind == Terrain.Forest && h % 7 < 3)
            {
                if (treePrefab is not null && importedTreeCount < MaxImportedTrees)
                {
                    AddImportedModel(treePrefab, p, 2.8f, h);
                    importedTreeCount++;
                    count++;
                    continue;
                }
                float scale = 0.74f + (h % 19) * 0.023f;
                trunks.Add((MakeTransform(p + Vector3.Up * 0.55f * scale,
                    new Vector3(0.20f * scale, scale, 0.20f * scale)),
                    new Color("#836146")));
                canopies.Add((MakeTransform(p + Vector3.Up * 1.34f * scale,
                    new Vector3(0.98f * scale, 1.25f * scale, 0.98f * scale)),
                    ((h >> 5) & 1) == 0 ? new Color("#328665") : new Color("#4c9a75")));
                upperCanopies.Add((MakeTransform(p + Vector3.Up * 2.0f * scale,
                    new Vector3(0.76f * scale, 0.90f * scale, 0.76f * scale)),
                    ((h >> 6) & 1) == 0 ? new Color("#5cba81") : new Color("#2d7658")));
                count++;
            }
            else if (kind == Terrain.Mountain && h % 5 < 3)
            {
                if (rockPrefab is not null && importedRockCount < MaxImportedRocks)
                {
                    AddImportedModel(rockPrefab, p, 0.8f, h);
                    importedRockCount++;
                    count++;
                    continue;
                }
                float size = 0.35f + (h % 11) * 0.046f;
                stones.Add((MakeTransform(p + Vector3.Up * 0.17f,
                    new Vector3(size, size * 0.65f, size * 0.8f)),
                    ((h >> 6) & 1) == 0 ? new Color("#a5b1b8") : new Color("#637582")));
                count++;
            }
            else if (kind == Terrain.Grass && h % 11 == 0)
            {
                shrubs.Add((MakeTransform(p + Vector3.Up * 0.13f,
                    new Vector3(0.27f, 0.25f, 0.27f)),
                    ((h >> 4) & 1) == 0 ? new Color("#b7c967") : new Color("#d9b66b")));
                count++;
            }
        }
        var trunkMesh = new CylinderMesh { Height = 1.2f, TopRadius = 0.4f, BottomRadius = 0.5f, RadialSegments = 5 };
        var foliageMesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 7, Rings = 4 };
        var boulderMesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 6, Rings = 3 };

        AddBatched("TreeTrunks", trunkMesh, trunks);
        AddBatched("TreeCrownsLower", foliageMesh, canopies);
        AddBatched("TreeCrownsUpper", foliageMesh, upperCanopies);
        AddBatched("Rocks", boulderMesh, stones);
        AddBatched("FlowersShrubs", foliageMesh, shrubs);
        LastPropCount = count;
    }

    /// <summary>Use actual CC0 imported GLB materials, with approximate model height normalization.</summary>
    private void AddImportedModel(PackedScene scene, Vector3 ground, float desiredHeight, uint variant)
    {
        Node3D model = scene.Instantiate<Node3D>();
        float sourceHeight = Math.Max(0.05f, EstimateMeshHeight(model));
        float size = Mathf.Clamp(desiredHeight / sourceHeight, 0.08f, 5f);
        model.Scale = Vector3.One * size;
        model.Position = ground;
        model.Rotation = new Vector3(0, (variant % 8) * Mathf.Pi / 4, 0);
        model.Name = "LicensedNatureModel";
        _vegetation.AddChild(model);
    }

    private static float EstimateMeshHeight(Node node)
    {
        float height = 0;
        if (node is MeshInstance3D { Mesh: not null } mesh)
            height = mesh.Mesh.GetAabb().Size.Y * Math.Abs(mesh.Scale.Y);
        foreach (Node child in node.GetChildren())
            height = Math.Max(height, EstimateMeshHeight(child));
        return height;
    }

    private void AddBatched(string name, Mesh mesh,
        List<(Transform3D Transform, Color Color)> transforms)
    {
        if (transforms.Count == 0) return;
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = transforms.Count
        };
        for (int i = 0; i < transforms.Count; i++)
        {
            multi.SetInstanceTransform(i, transforms[i].Transform);
            multi.SetInstanceColor(i, transforms[i].Color);
        }
        _vegetation.AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            MaterialOverride = _propMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        });
    }

    private static Transform3D MakeTransform(Vector3 at, Vector3 scale) =>
        new(Basis.Identity.Scaled(scale), at);

    private static bool IsWater(Terrain tile) =>
        tile is Terrain.DeepWater or Terrain.ShallowWater;

    private static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed ^ ((uint)x * 0x9e3779b9u) ^ ((uint)y * 0x85ebca6bu);
            h ^= h >> 16; h *= 0x7feb352du;
            h ^= h >> 15; h *= 0x846ca68bu;
            return h ^ (h >> 16);
        }
    }
}
