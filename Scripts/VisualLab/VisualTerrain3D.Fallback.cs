using System;
using System.Collections.Generic;
using Godot;
using WorldForge.Streaming;
using WorldSim;
using Num4 = System.Numerics.Vector4;

namespace WorldForge.VisualLab;

public partial class VisualTerrain3D
{
    private sealed record ViewportSurface(ChunkGeneration Generation,
        int LandCells, int WaterCells, MeshInstance3D? Node, Mesh? Mesh);

    private readonly Dictionary<ChunkCoord, ViewportSurface> _viewportDetails = new();
    private readonly Dictionary<ChunkCoord, ViewportSurface> _viewportFallbacks = new();
    public int ActiveFallbackChunks => _viewportFallbacks.Count;
    public long TotalFallbackBuilds { get; private set; }

    private bool HasOceanCoverage =>
        GodotObject.IsInstanceValid(_water) && !_water.IsQueuedForDeletion() &&
        _water.IsVisibleInTree() && _water.Mesh is PlaneMesh plane &&
        plane.Size.IsEqualApprox(new Vector2(_world.Width, _world.Height)) &&
        _water.Position.IsEqualApprox(new Vector3(_world.Width * .5f, .12f, _world.Height * .5f)) &&
        _water.MaterialOverride is ShaderMaterial water && water.Shader is not null &&
        water.Shader.ResourcePath == "res://Assets/Shaders/stylized_water.gdshader";

    private static bool HasLandMesh(ViewportSurface surface, bool requireVisible) =>
        surface.LandCells == 0 ||
        (surface.Node is not null && GodotObject.IsInstanceValid(surface.Node) &&
         !surface.Node.IsQueuedForDeletion() && surface.Mesh is not null &&
         GodotObject.IsInstanceValid(surface.Mesh) &&
         surface.Node.Mesh == surface.Mesh && surface.Mesh.GetSurfaceCount() > 0 &&
         (!requireVisible || surface.Node.IsVisibleInTree()));

    private bool CoversSurface(ViewportSurface surface) =>
        HasLandMesh(surface, requireVisible: true) &&
        (surface.WaterCells == 0 || HasOceanCoverage);

    public bool HasViewportDetailedCoverage(int x, int z)
    {
        var key = new ChunkCoord(x, z);
        return _viewportQueue.StateOf(key) == ChunkResidencyState.Ready &&
            _viewportDetails.TryGetValue(key, out ViewportSurface? surface) &&
            surface.Generation == ViewportGeneration(key) && CoversSurface(surface);
    }

    public bool HasViewportSurfaceCoverage(int x, int z)
    {
        if (HasViewportDetailedCoverage(x, z)) return true;
        var key = new ChunkCoord(x, z);
        return _viewportFallbacks.TryGetValue(key, out ViewportSurface? surface) &&
            surface.Generation == ViewportGeneration(key) && CoversSurface(surface);
    }

    private void RecordViewportDetail(int x, int z, MeshInstance3D node, int landCells)
    {
        if (!ViewportStreamingEnabled) return;
        var key = new ChunkCoord(x, z);
        int cells = Math.Min(ChunkSize, _world.Width - x * ChunkSize) *
            Math.Min(ChunkSize, _world.Height - z * ChunkSize);
        _viewportDetails[key] = new ViewportSurface(ViewportGeneration(key),
            landCells, cells - landCells, node, node.Mesh);
        // Terrain may finish before its foliage/lease. Keep the proxy until
        // the entire synchronous replacement has successfully completed.
        if (_viewportFallbacks.ContainsKey(key)) node.Visible = false;
    }

    private void CompleteViewportDetail(ChunkRequest request)
    {
        if (!_viewportDetails.TryGetValue(request.Key, out ViewportSurface? surface) ||
            surface.Generation != request.Generation ||
            !HasLandMesh(surface, requireVisible: false) ||
            (surface.WaterCells > 0 && !HasOceanCoverage))
            throw new InvalidOperationException("Detailed surface is not ready for publication.");
        if (!_viewportQueue.Complete(request))
            throw new InvalidOperationException("Synchronous chunk lease was invalidated.");
        if (surface.Node is not null) surface.Node.Visible = surface.LandCells > 0;
        if (_foliageChunks.TryGetValue((request.Key.X, request.Key.Z), out Node3D? foliage))
            foliage.Visible = true;
        RetireViewportFallback(request.Key);
    }

    private void RefreshViewportFallbacks()
    {
        if (!ViewportStreamingEnabled || _viewportPlan is null) return;
        foreach (ChunkCoord key in _viewportPlan.Visible)
        {
            if (HasViewportDetailedCoverage(key.X, key.Z))
            {
                RetireViewportFallback(key);
                continue;
            }
            // A Ready lease without its actual mesh is not coverage. Repair
            // it through the same generation pipeline instead of trusting a key.
            if (_viewportQueue.StateOf(key) == ChunkResidencyState.Ready)
                MarkViewportChunkDirty(key.X, key.Z);
            if (_viewportFallbacks.TryGetValue(key, out ViewportSurface? existing) &&
                existing.Generation == ViewportGeneration(key) && CoversSurface(existing))
                continue;
            BuildViewportFallback(key);
        }
        foreach (ChunkCoord key in new List<ChunkCoord>(_viewportFallbacks.Keys))
            if (!_viewportPlan.Visible.Contains(key)) RetireViewportFallback(key);
    }

    private void BuildViewportFallback(ChunkCoord key)
    {
        int ox = key.X * ChunkSize, oz = key.Z * ChunkSize;
        int width = Math.Min(ChunkSize, _world.Width - ox);
        int depth = Math.Min(ChunkSize, _world.Height - oz);
        var cells = new FallbackCell[width * depth];
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
            cells[z * width + x] = SampleFallbackCell(ox + x, oz + z);
        FallbackMeshData data = TerrainFallbackMesh.Build(ox, oz, width, depth, cells);
        if (data.WaterCells > 0 && !HasOceanCoverage)
            throw new InvalidOperationException("The approved opaque ocean surface is unavailable.");

        MeshInstance3D? node = null;
        ArrayMesh? mesh = null;
        if (data.LandCells > 0)
        {
            var positions = new Vector3[data.Positions.Length];
            var normals = new Vector3[data.Normals.Length];
            var colors = new Color[data.Colors.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var p = data.Positions[i]; var n = data.Normals[i]; Num4 c = data.Colors[i];
                positions[i] = new Vector3(p.X, p.Y, p.Z);
                normals[i] = new Vector3(n.X, n.Y, n.Z);
                colors[i] = new Color(c.X, c.Y, c.Z, c.W);
            }
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = positions;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            if (mesh.GetSurfaceCount() != 1)
            {
                mesh.Dispose();
                throw new InvalidOperationException("Fallback mesh upload did not create a surface.");
            }
            mesh.SurfaceSetMaterial(0, _earthMaterial);
            node = new MeshInstance3D
            {
                Name = $"ViewportFallback_{key.X}_{key.Z}_{_viewportWorldEpoch}_{TotalFallbackBuilds}",
                Mesh = mesh, Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(node);
        }

        // Publish the new resource first; retire old native nodes only after
        // success, and hide them immediately (QueueFree is deferred).
        var replacement = new ViewportSurface(ViewportGeneration(key),
            data.LandCells, data.WaterCells, node, mesh);
        _viewportFallbacks.TryGetValue(key, out ViewportSurface? previous);
        _viewportFallbacks[key] = replacement;
        if (node is not null) node.Visible = true;
        if (_chunks.TryGetValue((key.X, key.Z), out MeshInstance3D? detail))
            detail.Visible = false;
        if (_foliageChunks.TryGetValue((key.X, key.Z), out Node3D? foliage))
            foliage.Visible = false;
        RetireSurfaceNode(previous);
        TotalFallbackBuilds++;
    }

    private FallbackCell SampleFallbackCell(int x, int z)
    {
        Terrain tile = _tiles.Tile(x, z);
        if (IsWater(tile)) return default;
        float h = HeightAt(x, z);
        SharedCornerHeightfield.Fan fan = SmoothLandEnabled ? EditableFan(x, z) :
            new SharedCornerHeightfield.Fan(h, h, h, h, h + ReliefAt(x, z));
        Num4 center = NumericColor(TerrainColors[(int)tile]);
        Num4 Corner(int dx, int dz) => RichTerrainDetails
            ? NumericColor(ToColor(EnvironmentArtMath.CornerColor(x, z, dx, dz,
                _world.Width, _world.Height, _world.Tile, _world.Seed))) : center;
        var banks = FallbackBanks.None;
        var floors = new Num4(.12f);
        bool Edge(int nx, int nz) => !_world.Contains(nx, nz) || IsWater(_tiles.Tile(nx, nz));
        if (SmoothLandEnabled)
        {
            if (Edge(x - 1, z)) banks |= FallbackBanks.West;
            if (Edge(x + 1, z)) banks |= FallbackBanks.East;
            if (Edge(x, z - 1)) banks |= FallbackBanks.North;
            if (Edge(x, z + 1)) banks |= FallbackBanks.South;
        }
        else
        {
            floors = new Num4(HeightAt(x - 1, z), HeightAt(x + 1, z),
                HeightAt(x, z - 1), HeightAt(x, z + 1));
            if (h > floors.X + .02f) banks |= FallbackBanks.West;
            if (h > floors.Y + .02f) banks |= FallbackBanks.East;
            if (h > floors.Z + .02f) banks |= FallbackBanks.North;
            if (h > floors.W + .02f) banks |= FallbackBanks.South;
        }
        Color bank = SmoothLandEnabled
            ? (tile == Terrain.Sand ? new Color("#b89c69") : new Color("#746d5c"))
            : (tile == Terrain.Mountain ? new Color("#5a5d62") :
                tile == Terrain.Sand ? new Color("#b89c69") : new Color("#77654e"));
        return new FallbackCell(true, new Num4(fan.A, fan.B, fan.C, fan.D), fan.Center,
            Corner(0, 0), Corner(0, 1), Corner(1, 0), Corner(1, 1), center,
            banks, floors, NumericColor(bank));
    }

    private static Num4 NumericColor(Color color) => new(color.R, color.G, color.B, color.A);

    private static void RetireSurfaceNode(ViewportSurface? surface)
    {
        if (surface?.Node is not MeshInstance3D node || !GodotObject.IsInstanceValid(node)) return;
        node.Visible = false;
        node.QueueFree();
    }

    private void RetireViewportFallback(ChunkCoord key)
    {
        if (_viewportFallbacks.Remove(key, out ViewportSurface? surface)) RetireSurfaceNode(surface);
    }

    private void ResetViewportSurfaces()
    {
        foreach (ViewportSurface surface in _viewportFallbacks.Values) RetireSurfaceNode(surface);
        _viewportFallbacks.Clear();
        _viewportDetails.Clear();
    }
}
