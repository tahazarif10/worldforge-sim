using System;
using System.Collections.Generic;
using Godot;
using WorldSim;
using WorldSim.Regions;
using WorldForge.Streaming;

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
    private ITerrainTileSource _tiles = null!;
    public string TerrainBackendName => _tiles is RegionMirrorTerrainSource ? "region-mirror" : "direct";
    private ElevationRegionStore _elevation = null!;
    public int ModifiedElevationRegions => _elevation?.ModifiedRegions ?? 0;
    private Node3D _vegetation = null!;
    private MeshInstance3D _water = null!;
    private ImageTexture? _coastMaskTexture;
    private bool _coastDirty;
    private bool _ecoMode;
    public bool EcoMode => _ecoMode;
    private readonly HashSet<(int X, int Y)> _changedChunks = new();
    private readonly Dictionary<(int X, int Y), Node3D> _foliageChunks = new();
    private readonly Dictionary<(int X, int Y), int> _propCounts = new();
    private PackedScene? _treePrefab;
    private PackedScene? _rockPrefab;
    private NatureArtLibrary? _natureArt;
    public long FoliageCellsVisited { get; private set; }
    public long FoliageChunksRebuilt { get; private set; }
    public long TotalTerrainChunkBuilds { get; private set; }
    public int ActiveTerrainChunks => _chunks.Count;
    public int LastRequestedChunks { get; private set; }
    public int PendingVisibleChunks => Math.Max(0, LastRequestedChunks - _chunks.Count);
    public bool RichTerrainDetails { get; private set; } = true;
    public bool SculptedFacets { get; private set; } = true;
    public bool SmoothSlopes { get; private set; } = true;
    public bool SmoothLandEnabled => RichTerrainDetails && SmoothSlopes;

    private SharedCornerHeightfield.Fan EditableFan(int x, int y)
    {
        SharedCornerHeightfield.Fan baseline = SharedCornerHeightfield.Tile(
            x, y, _world.Width, _world.Height, HeightAt, ReliefAt(x, y));
        float a = _elevation.Offset(new CellCoord(x, y));
        float b = _elevation.Offset(new CellCoord(x, y + 1));
        float c = _elevation.Offset(new CellCoord(x + 1, y));
        float d = _elevation.Offset(new CellCoord(x + 1, y + 1));
        return new SharedCornerHeightfield.Fan(
            baseline.A + a, baseline.B + b, baseline.C + c,
            baseline.D + d, baseline.Center + (a + b + c + d) * .25f);
    }

    /// <summary>
    /// Add height offsets only at affected absolute lattice vertices.
    /// Existing simulation biome, water, assets and four-triangle topology
    /// remain untouched; only the camera-resident 16x16 chunks rebuild.
    /// </summary>
    public int SculptElevation(int centerX, int centerY, int radius, float metres)
    {
        if (!SmoothLandEnabled || radius is < 0 or > 12) return 0;
        int changed = 0;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int vx = centerX + dx, vz = centerY + dy;
            if (vx < 0 || vz < 0 || vx > _world.Width || vz > _world.Height)
                continue;
            float attenuation = radius == 0 ? 1f :
                1f - .70f * (dx * dx + dy * dy) / (float)(radius * radius);
            if (_elevation.AddOffset(new CellCoord(vx,vz), metres * attenuation))
                changed++;
        }
        if (changed > 0) MarkChanged(centerX, centerY, radius + 2);
        return changed;
    }

    public int SaveElevation(string directory) =>
        ElevationRegionFiles.SaveModified(_elevation, directory);

    public int LoadElevation(string directory)
    {
        int count = ElevationRegionFiles.LoadAll(_elevation, directory);
        if (count > 0) RebuildResidentGeometry();
        return count;
    }

    public void ToggleSmoothSlopes()
    {
        SmoothSlopes = !SmoothSlopes;
        RebuildResidentGeometry();
        GD.Print("V2 shared-corner continuous slopes: " +
            (SmoothLandEnabled ? "ON" : "OFF (legacy terraced terrain)"));
    }

    private void RebuildResidentGeometry()
    {
        // Debug quality toggle only: the usual view-driven loading remains
        // bounded. Snapshot dictionary keys before replacing foliage batches.
        foreach (var key in new List<(int X, int Y)>(_chunks.Keys))
            BuildChunk(key.X, key.Y);
        foreach (var key in new List<(int X, int Y)>(_foliageChunks.Keys))
            RebuildVegetationChunk(key.X, key.Y);
    }

    public float ReliefAt(int x, int y)
    {
        if (!RichTerrainDetails || !SculptedFacets || !_world.Contains(x, y))
            return 0f;
        return TerrainReliefMath.CenterLift(_tiles.Tile(x, y), x, y, _world.Seed);
    }

    public float SurfaceHeightAt(int x, int y, float localX, float localY)
    {
        if (!_world.Contains(x, y)) return 0.12f;
        if (SmoothLandEnabled &&
            _tiles.Tile(x,y) is not (Terrain.DeepWater or Terrain.ShallowWater))
            return EditableFan(x,y).Sample(localX,localY);
        return TerrainReliefMath.SurfaceHeight(
            HeightAt(x,y), ReliefAt(x,y), localX,localY);
    }

    public void ToggleSculptedFacets()
    {
        SculptedFacets = !SculptedFacets;
        // The existing four triangles keep the same topology; only their
        // center vertex changes. Rebuild only resident terrain/foliage.
        RebuildResidentGeometry();
        GD.Print("V2 sculpted terrain facets: " + (SculptedFacets ? "ON" : "OFF"));
    }

    public void ToggleRichTerrainDetails()
    {
        RichTerrainDetails = !RichTerrainDetails;
        // Compare visual style and frame intervals with exactly the same
        // WorldSim state and camera pose. Rebuild resident meshes only on
        // deliberate F3 interaction, never on every simulation frame.
        RebuildResidentGeometry();
        GD.Print("V2 rich terrain surface/cliff shading: " +
            (RichTerrainDetails ? "ON" : "OFF"));
    }
    public int ActiveFoliageChunks => _foliageChunks.Count;

    /// <summary>
    /// Eco keeps all authored meshes and ground colors; only cuts optional
    /// costly canopy shadows and secondary shader glints.
    /// Existing scene nodes are updated without rebuilding their geometry.
    /// </summary>
    public void SetEcoMode(bool eco)
    {
        _ecoMode = eco;
        if (_water?.MaterialOverride is ShaderMaterial water)
            water.SetShaderParameter("water_detail", eco ? 0f : 1f);
        foreach (Node3D foliage in _foliageChunks.Values)
            ApplyShadowTier(foliage);
    }

    private void ApplyShadowTier(Node3D chunk)
    {
        foreach (Node node in chunk.GetChildren())
        {
            if (node is not MultiMeshInstance3D mesh) continue;
            bool smallFoliage = mesh.Name.ToString().StartsWith("CC0_Grass") ||
                                mesh.Name.ToString().StartsWith("CC0_Flower") ||
                                mesh.Name.ToString() == "FlowersShrubs";
            mesh.CastShadow = (_ecoMode || smallFoliage)
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : GeometryInstance3D.ShadowCastingSetting.On;
        }
    }

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

    public void BindWorld(WorldState world, ITerrainTileSource? terrainSource = null)
    {
        _world = world;
        // Keep the original direct WorldState path as the default. Region
        // parity mode uses the same shader, water, foliage and camera code.
        _tiles = terrainSource ?? new DirectWorldTerrainSource(world);
        if (_tiles.Width != world.Width || _tiles.Height != world.Height ||
            _tiles.Seed != world.Seed)
            throw new ArgumentException("Terrain source must exactly match current WorldState.");
        _elevation = new ElevationRegionStore(world.Seed, maxResidentRegions:12);
        foreach (MeshInstance3D mesh in _chunks.Values) mesh.QueueFree();
        _chunks.Clear();
        _changedChunks.Clear();
        foreach (Node3D chunk in _foliageChunks.Values) chunk.QueueFree();
        _foliageChunks.Clear();
        _propCounts.Clear();
        LastPropCount = 0;

        _vegetation ??= new Node3D { Name = "FoliageBatches" };
        if (_vegetation.GetParent() is null) AddChild(_vegetation);
        _treePrefab = ResourceLoader.Exists("res://Assets/Nature/Tree.glb")
            ? GD.Load<PackedScene>("res://Assets/Nature/Tree.glb") : null;
        _rockPrefab = ResourceLoader.Exists("res://Assets/Nature/Rock.glb")
            ? GD.Load<PackedScene>("res://Assets/Nature/Rock.glb") : null;
        _natureArt ??= new NatureArtLibrary();

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
        _coastMaskTexture = null;
        _coastDirty = true;
        RefreshCoastMask();
        SetEcoMode(_ecoMode);

        // No eager global mesh construction here. Camera projection determines
        // which chunks should exist after the camera rig is initialized.
    }

    /// <summary>
    /// Build and retain only camera-visible terrain and foliage batches.
    /// Orthographic ray intersections with sea level and mountain maximum
    /// provide a conservative X/Z footprint; one chunk of safety margin
    /// prevents edge popping as the camera moves.
    /// </summary>
    public void UpdateVisibility(Godot.Camera3D camera, int maxNewChunks = 4)
    {
        if (_world is null) return;
        Vector2 viewport = camera.GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        float minX = float.PositiveInfinity, minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
        Span<Vector2> corners =
        [
            Vector2.Zero, new Vector2(viewport.X, 0),
            new Vector2(0, viewport.Y), viewport
        ];
        foreach (Vector2 screen in corners)
        {
            Vector3 origin = camera.ProjectRayOrigin(screen);
            Vector3 direction = camera.ProjectRayNormal(screen);
            if (Mathf.Abs(direction.Y) < 0.00001f) continue;
            foreach (float elevation in new float[] { 0.12f, 5f })
            {
                float t = (elevation - origin.Y) / direction.Y;
                if (!float.IsFinite(t) || t < 0) continue;
                Vector3 hit = origin + direction * t;
                minX = MathF.Min(minX, hit.X);
                maxX = MathF.Max(maxX, hit.X);
                minZ = MathF.Min(minZ, hit.Z);
                maxZ = MathF.Max(maxZ, hit.Z);
            }
        }
        if (!float.IsFinite(minX)) return;

        HashSet<(int X, int Y)> desired = VisibleChunkPlanner.Plan(
            _world.Width, _world.Height, ChunkSize, minX, minZ, maxX, maxZ);
        LastRequestedChunks = desired.Count;

        foreach (var key in new List<(int X, int Y)>(_chunks.Keys))
        {
            if (desired.Contains(key)) continue;
            _chunks[key].QueueFree();
            _chunks.Remove(key);
            if (_foliageChunks.Remove(key, out Node3D? foliage))
                foliage.QueueFree();
            if (_propCounts.Remove(key, out int oldCount))
                LastPropCount -= oldCount;
        }

        // A fast drag or zoom must NOT build dozens of detailed GLB batches
        // in a single _Process frame. Nearest missing chunks are shown first;
        // an extra chunk margin conceals most in-motion loading.
        var resident = new HashSet<(int X, int Y)>(_chunks.Keys);
        var toBuild = ChunkLoadBudget.Choose(desired, resident,
            (minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f,
            ChunkSize, maxNewChunks);
        foreach (var key in toBuild)
        {
            BuildChunk(key.X, key.Y);
            RebuildVegetationChunk(key.X, key.Y);
        }
    }

    /// <summary>Defer all geometry changes until end of simulation frame.</summary>
    public void MarkChanged(int x, int y, int radius)
    {
        if (_world is null) return;
        // Apply changed simulation cells to the region mirror BEFORE any
        // coastline mask, geometry, foliage or picker queries.
        _tiles.SynchronizeArea(x,y,radius);
        int left = Math.Max(0, x - radius - 1);
        int top = Math.Max(0, y - radius - 1);
        int right = Math.Min(_world.Width - 1, x + radius + 1);
        int bottom = Math.Min(_world.Height - 1, y + radius + 1);
        for (int cy = top / ChunkSize; cy <= bottom / ChunkSize; cy++)
        for (int cx = left / ChunkSize; cx <= right / ChunkSize; cx++)
            _changedChunks.Add((cx, cy));
        _coastDirty = true;
    }

    public void FlushChanges()
    {
        if (_changedChunks.Count == 0 && !_coastDirty) return;
        foreach (var (x, y) in _changedChunks)
        {
            // A hidden region retains edits in the authoritative WorldState;
            // it is rebuilt from current data the next time the camera arrives.
            if (!_chunks.ContainsKey((x, y))) continue;
            BuildChunk(x, y);
            RebuildVegetationChunk(x, y);
        }
        _changedChunks.Clear();
        if (_coastDirty) RefreshCoastMask();
    }

    private void RefreshCoastMask()
    {
        // Shoreline texture is only rebuilt when terrain edits or world
        // replacement change the land/water boundary, never on every frame.
        if (_water.MaterialOverride is not ShaderMaterial material) return;
        byte[] rgba = WaterCoastMath.BuildMask(
            _world.Width, _world.Height, _tiles.Tile, _world.Seed);
        Image image = Image.CreateFromData(_world.Width, _world.Height,
            false, Image.Format.Rgba8, rgba);
        if (_coastMaskTexture is null)
            _coastMaskTexture = ImageTexture.CreateFromImage(image);
        else _coastMaskTexture.Update(image);
        material.SetShaderParameter("shore_mask", _coastMaskTexture);
        material.SetShaderParameter("map_size",
            new Vector2(_world.Width, _world.Height));
        _coastDirty = false;
    }

    public float HeightAt(int x, int y)
    {
        if (!_world.Contains(x, y)) return 0.12f;
        Terrain tile = _tiles.Tile(x, y);
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
        TotalTerrainChunkBuilds++;
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
            Terrain tile = _tiles.Tile(x, y);
            if (tile is Terrain.DeepWater or Terrain.ShallowWater) continue;
            any = true;
            float h = HeightAt(x, y);
            Color col = TerrainColors[(int)tile];
            Vector3 a = new(x, h, y), b = new(x, h, y + 1);
            Vector3 c = new(x + 1, h, y), d = new(x + 1, h, y + 1);
            // Four shaded top triangles share the actual simulation height.
            // Color blends biomes across boundaries, with deterministic
            // small-scale color variation. Picking remains exact.
            if (SmoothLandEnabled)
                AddSmoothTopFace(surface,x,y,col);
            else if (RichTerrainDetails)
                AddTopFace(surface, x, y, h, col, a, b, c, d);
            else
                AddQuad(surface, a, b, c, d, col, Vector3.Up);

            if (SmoothLandEnabled)
            {
                // Only sea-facing and world-boundary skirts: neighboring land
                // cells use the exact SAME corner vertices, so no interior
                // cliff faces or terrain cracks across mesh chunk borders.
                DrawSmoothCoastSkirts(surface,x,y);
            }
            else
            {
                // Real vertical cliff faces, darker beneath the upper terrain.
                Color rock = tile == Terrain.Mountain ? new Color("#5a5d62") :
                             tile == Terrain.Sand ? new Color("#b89c69") : new Color("#77654e");
                float west = x > 0 ? HeightAt(x - 1, y) : 0.12f;
                float east = x < _world.Width - 1 ? HeightAt(x + 1, y) : 0.12f;
                float north = y > 0 ? HeightAt(x, y - 1) : 0.12f;
                float south = y < _world.Height - 1 ? HeightAt(x, y + 1) : 0.12f;
                uint strataVariation = Hash(x, y, _world.Seed + 309);
                if (h > west + 0.02f)
                    DrawCliff(surface, new(x, west, y), new(x, west, y + 1),
                        new(x, h, y), new(x, h, y + 1), rock, Vector3.Left, strataVariation);
                if (h > east + 0.02f)
                    DrawCliff(surface, new(x + 1, east, y + 1), new(x + 1, east, y),
                        new(x + 1, h, y + 1), new(x + 1, h, y), rock, Vector3.Right, strataVariation);
                if (h > north + 0.02f)
                    DrawCliff(surface, new(x + 1, north, y), new(x, north, y),
                        new(x + 1, h, y), new(x, h, y), rock, Vector3.Back, strataVariation);
                if (h > south + 0.02f)
                    DrawCliff(surface, new(x, south, y + 1), new(x + 1, south, y + 1),
                        new(x, h, y + 1), new(x + 1, h, y + 1), rock, Vector3.Forward, strataVariation);

            }

            // Legacy pale stripes are designed for flat land plateaus;
            // smooth mode uses the existing low-cost ocean foam shader.
            if (!SmoothLandEnabled)
            {
                // Narrow pale tide line on the land side of the coast.
                if (x > 0 && IsWater(_tiles.Tile(x - 1, y)))
                    AddQuad(surface,
                        new(x, h + 0.012f, y), new(x, h + 0.012f, y + 1),
                        new(x + 0.12f, h + 0.012f, y), new(x + 0.12f, h + 0.012f, y + 1),
                        new Color("#f5e4b2"), Vector3.Up);
                if (y > 0 && IsWater(_tiles.Tile(x, y - 1)))
                    AddQuad(surface,
                        new(x, h + 0.012f, y), new(x, h + 0.012f, y + 0.12f),
                        new(x + 1, h + 0.012f, y), new(x + 1, h + 0.012f, y + 0.12f),
                        new Color("#f5e4b2"), Vector3.Up);
                if (x + 1 < _world.Width && IsWater(_tiles.Tile(x + 1, y)))
                    AddQuad(surface,
                        new(x + 0.88f, h + 0.012f, y),
                        new(x + 0.88f, h + 0.012f, y + 1),
                        new(x + 1, h + 0.012f, y),
                        new(x + 1, h + 0.012f, y + 1),
                        new Color("#f5e4b2"), Vector3.Up);
                if (y + 1 < _world.Height && IsWater(_tiles.Tile(x, y + 1)))
                    AddQuad(surface,
                        new(x, h + 0.012f, y + 0.88f),
                        new(x, h + 0.012f, y + 1),
                        new(x + 1, h + 0.012f, y + 0.88f),
                        new(x + 1, h + 0.012f, y + 1),
                        new Color("#f5e4b2"), Vector3.Up);
            }
        }

        instance.Visible = any;
        if (any) instance.Mesh = surface.Commit();
        else instance.Mesh = null;
    }

    private void AddSmoothTopFace(SurfaceTool surface, int x, int y, Color centerColor)
    {
        SharedCornerHeightfield.Fan f = EditableFan(x,y);
        Vector3 a = new(x,f.A,y), b = new(x,f.B,y+1);
        Vector3 c = new(x+1,f.C,y), d = new(x+1,f.D,y+1);
        Vector3 mid = new(x+.5f,f.Center,y+.5f);
        Color ca = ToColor(EnvironmentArtMath.CornerColor(x,y,0,0,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cb = ToColor(EnvironmentArtMath.CornerColor(x,y,0,1,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cc = ToColor(EnvironmentArtMath.CornerColor(x,y,1,0,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cd = ToColor(EnvironmentArtMath.CornerColor(x,y,1,1,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        AddColoredTriangle(surface,a,b,mid,ca,cb,centerColor);
        AddColoredTriangle(surface,b,d,mid,cb,cd,centerColor);
        AddColoredTriangle(surface,d,c,mid,cd,cc,centerColor);
        AddColoredTriangle(surface,c,a,mid,cc,ca,centerColor);
    }

    private void DrawSmoothCoastSkirts(SurfaceTool surface,int x,int y)
    {
        SharedCornerHeightfield.Fan f = SharedCornerHeightfield.Tile(
            x,y,_world.Width,_world.Height,HeightAt,ReliefAt(x,y));
        const float sea = .12f;
        Color bank = _tiles.Tile(x,y) == Terrain.Sand
            ? new Color("#b89c69") : new Color("#746d5c");
        uint variation = Hash(x,y,_world.Seed+309);
        // Same edge samples for all chunks, with 3 affordable strata layers.
        if (x == 0 || IsWater(_tiles.Tile(x-1,y)))
            DrawCliff(surface,
                new(x,sea,y),new(x,sea,y+1),
                new(x,f.A,y),new(x,f.B,y+1),
                bank,Vector3.Left,variation);
        if (x == _world.Width-1 || IsWater(_tiles.Tile(x+1,y)))
            DrawCliff(surface,
                new(x+1,sea,y+1),new(x+1,sea,y),
                new(x+1,f.D,y+1),new(x+1,f.C,y),
                bank,Vector3.Right,variation);
        if (y == 0 || IsWater(_tiles.Tile(x,y-1)))
            DrawCliff(surface,
                new(x+1,sea,y),new(x,sea,y),
                new(x+1,f.C,y),new(x,f.A,y),
                bank,Vector3.Back,variation);
        if (y == _world.Height-1 || IsWater(_tiles.Tile(x,y+1)))
            DrawCliff(surface,
                new(x,sea,y+1),new(x+1,sea,y+1),
                new(x,f.B,y+1),new(x+1,f.D,y+1),
                bank,Vector3.Forward,variation);
    }

    private void AddTopFace(SurfaceTool surface, int x, int y, float height,
        Color centerColor, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Color ca = ToColor(EnvironmentArtMath.CornerColor(x,y,0,0,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cb = ToColor(EnvironmentArtMath.CornerColor(x,y,0,1,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cc = ToColor(EnvironmentArtMath.CornerColor(x,y,1,0,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Color cd = ToColor(EnvironmentArtMath.CornerColor(x,y,1,1,
            _world.Width,_world.Height,_world.Tile,_world.Seed));
        Vector3 mid = new(x + .5f, height + ReliefAt(x, y), y + .5f);
        AddColoredTriangle(surface, a,b,mid,ca,cb,centerColor);
        AddColoredTriangle(surface, b,d,mid,cb,cd,centerColor);
        AddColoredTriangle(surface, d,c,mid,cd,cc,centerColor);
        AddColoredTriangle(surface, c,a,mid,cc,ca,centerColor);
    }

    private static Color ToColor(EnvironmentArtMath.Rgb rgb) =>
        new(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f);

    private static void AddColoredTriangle(SurfaceTool surface,
        Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        // Correct per-facet normal gives the 3D slopes an actual lighting
        // response. No extra triangles and no normal map/extra texture reads.
        Vector3 normal = (b - a).Cross(c - a).Normalized();
        surface.SetNormal(normal);
        surface.SetColor(ca); surface.AddVertex(a);
        surface.SetColor(cb); surface.AddVertex(b);
        surface.SetColor(cc); surface.AddVertex(c);
    }

    private void DrawCliff(SurfaceTool surface,
        Vector3 lowA, Vector3 lowB, Vector3 highA, Vector3 highB,
        Color rock, Vector3 normal, uint seed)
    {
        if (RichTerrainDetails)
            AddStratifiedFace(surface, lowA, lowB, highA, highB,
                rock, normal, seed);
        else
            AddQuad(surface, lowA, lowB, highA, highB, rock, normal);
    }

    private static void AddStratifiedFace(SurfaceTool surface,
        Vector3 lowA, Vector3 lowB, Vector3 highA, Vector3 highB,
        Color rock, Vector3 normal, uint variation)
    {
        // Pale rim, ochre/dark banding and irregular seeded tones suggest
        // a geological cut without changing collision or surface height.
        // No heap float[] allocation for every cliff face in a rebuilt chunk.
        for (int layer = 0; layer < 3; layer++)
        {
            float blend = layer switch { 0 => .18f, 1 => .06f, _ => .11f };
            Color tone = layer == 1 ? rock.Lightened(blend) : rock.Darkened(blend);
            if (((variation >> (layer * 3)) & 1u) == 1u)
                tone = tone.Lightened(.055f);
            AddQuad(surface,
                lowA.Lerp(highA, layer == 0 ? 0f : layer == 1 ? .33f : .72f),
                lowB.Lerp(highB, layer == 0 ? 0f : layer == 1 ? .33f : .72f),
                lowA.Lerp(highA, layer == 0 ? .33f : layer == 1 ? .72f : 1f),
                lowB.Lerp(highB, layer == 0 ? .33f : layer == 1 ? .72f : 1f),
                tone, normal);
        }
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

    private void RebuildVegetationChunk(int cx, int cy)
    {
        var key = (cx, cy);
        if (_foliageChunks.TryGetValue(key, out Node3D? previous))
        {
            previous.QueueFree();
            LastPropCount -= _propCounts[key];
        }
        var chunk = new Node3D { Name = $"FoliageChunk_{cx}_{cy}" };
        _vegetation.AddChild(chunk);
        _foliageChunks[key] = chunk;

        int importedTreeCount = 0, importedRockCount = 0;
        // Spatial cap is per chunk: no global allocation or edit-order bias.
        const int MaxImportedTreesPerChunk = 2, MaxImportedRocksPerChunk = 1;
        FoliageChunksRebuilt++;

        var trunks = new List<(Transform3D Transform, Color Color)>();
        var canopies = new List<(Transform3D Transform, Color Color)>();
        var upperCanopies = new List<(Transform3D Transform, Color Color)>();
        var stones = new List<(Transform3D Transform, Color Color)>();
        var shrubs = new List<(Transform3D Transform, Color Color)>();
        var authoredPlacements = new List<NatureArtLibrary.Placement>();

        int count = 0;
        int maxY = Math.Min(_world.Height, (cy + 1) * ChunkSize);
        int maxX = Math.Min(_world.Width, (cx + 1) * ChunkSize);
        for (int y = cy * ChunkSize; y < maxY; y++)
        for (int x = cx * ChunkSize; x < maxX; x++)
        {
            FoliageCellsVisited++;
            Terrain kind = _tiles.Tile(x, y);
            uint h = Hash(x, y, _world.Seed + 119);
            float offsetX = ((h >> 10) % 100) / 310f + 0.34f;
            float offsetZ = ((h >> 18) % 100) / 310f + 0.34f;
            float ground = SurfaceHeightAt(x, y, offsetX, offsetZ);
            Vector3 p = new(x + offsetX, ground, y + offsetZ);

            int species = NatureScatter.Pick(kind, h);
            if (kind == Terrain.Forest && species >= 0)
            {
                if (_treePrefab is not null && importedTreeCount < MaxImportedTreesPerChunk)
                {
                    AddImportedModel(chunk, _treePrefab, p, 2.8f, h);
                    importedTreeCount++;
                    count++;
                    continue;
                }
                if (_natureArt is not null && _natureArt.Has((NatureArtLibrary.Species)species))
                {
                    authoredPlacements.Add(new NatureArtLibrary.Placement(
                        (NatureArtLibrary.Species)species, p,
                        2.4f * NatureScatter.HeightFactor(h), NatureScatter.RotationRadians(h)));
                    count++;
                    continue;
                }
                // Missing import: retain lightweight procedural geometry
                // rather than leaving the map empty or hiding import errors.
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
            else if (kind is Terrain.Mountain or Terrain.Sand && species is 6 or 7)
            {
                if (_rockPrefab is not null && importedRockCount < MaxImportedRocksPerChunk)
                {
                    AddImportedModel(chunk, _rockPrefab, p, 0.8f, h);
                    importedRockCount++;
                    count++;
                    continue;
                }
                if (_natureArt is not null && _natureArt.Has((NatureArtLibrary.Species)species))
                {
                    authoredPlacements.Add(new NatureArtLibrary.Placement(
                        (NatureArtLibrary.Species)species, p,
                        (kind == Terrain.Mountain ? 0.9f : 0.55f) *
                        NatureScatter.HeightFactor(h), NatureScatter.RotationRadians(h)));
                    count++;
                    continue;
                }
                float size = 0.35f + (h % 11) * 0.046f;
                stones.Add((MakeTransform(p + Vector3.Up * 0.17f,
                    new Vector3(size, size * 0.65f, size * 0.8f)),
                    ((h >> 6) & 1) == 0 ? new Color("#a5b1b8") : new Color("#637582")));
                count++;
            }
            else if (kind == Terrain.Grass && species is 8 or 9)
            {
                if (_natureArt is not null && _natureArt.Has((NatureArtLibrary.Species)species))
                {
                    authoredPlacements.Add(new NatureArtLibrary.Placement(
                        (NatureArtLibrary.Species)species, p,
                        (species == 9 ? 0.42f : 0.52f) * NatureScatter.HeightFactor(h),
                        NatureScatter.RotationRadians(h)));
                    count++;
                    continue;
                }
                shrubs.Add((MakeTransform(p + Vector3.Up * 0.13f,
                    new Vector3(0.27f, 0.25f, 0.27f)),
                    ((h >> 4) & 1) == 0 ? new Color("#b7c967") : new Color("#d9b66b")));
                count++;
            }
        }
        var trunkMesh = new CylinderMesh { Height = 1.2f, TopRadius = 0.4f, BottomRadius = 0.5f, RadialSegments = 5 };
        var foliageMesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 7, Rings = 4 };
        var boulderMesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 6, Rings = 3 };

        AddBatched(chunk, "TreeTrunks", trunkMesh, trunks);
        AddBatched(chunk, "TreeCrownsLower", foliageMesh, canopies);
        AddBatched(chunk, "TreeCrownsUpper", foliageMesh, upperCanopies);
        AddBatched(chunk, "Rocks", boulderMesh, stones);
        AddBatched(chunk, "FlowersShrubs", foliageMesh, shrubs);
        _natureArt?.Populate(chunk, authoredPlacements);
        ApplyShadowTier(chunk);
        _propCounts[key] = count;
        LastPropCount += count;
    }

    /// <summary>Use actual CC0 imported GLB materials, with approximate model height normalization.</summary>
    private void AddImportedModel(Node3D parent, PackedScene scene, Vector3 ground, float desiredHeight, uint variant)
    {
        Node3D model = scene.Instantiate<Node3D>();
        float sourceHeight = Math.Max(0.05f, EstimateMeshHeight(model));
        float size = Mathf.Clamp(desiredHeight / sourceHeight, 0.08f, 5f);
        model.Scale = Vector3.One * size;
        model.Position = ground;
        model.Rotation = new Vector3(0, (variant % 8) * Mathf.Pi / 4, 0);
        model.Name = "LicensedNatureModel";
        parent.AddChild(model);
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

    private void AddBatched(Node3D parent, string name, Mesh mesh,
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
        parent.AddChild(new MultiMeshInstance3D
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
