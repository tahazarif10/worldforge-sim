using System;
using WorldSim.Regions;

namespace WorldSim;

/// <summary>
/// World terrain access contract for the EXACT SAME renderer and camera.
/// This first migration adapter deliberately retains the approved V2 world
/// bounds and seed. Large-world generation is a later, separate milestone.
/// </summary>
public interface ITerrainTileSource
{
    int Width { get; }
    int Height { get; }
    int Seed { get; }
    Terrain Tile(int x, int y);

    /// <summary>Called after an authoritative WorldState brush operation.
    /// Never mutates the gameplay simulation or the RNG.</summary>
    void SynchronizeArea(int centerX, int centerY, int radius);
}

/// <summary>Legacy gameplay path; avoids all region mirroring overhead.</summary>
public sealed class DirectWorldTerrainSource(WorldState world) : ITerrainTileSource
{
    private readonly WorldState _world = world ?? throw new ArgumentNullException(nameof(world));
    public int Width => _world.Width;
    public int Height => _world.Height;
    public int Seed => _world.Seed;
    public Terrain Tile(int x, int y) => _world.Tile(x, y);
    public void SynchronizeArea(int centerX, int centerY, int radius) { }
}

/// <summary>
/// Temporary parity bridge: copies the *actual V2 tiles* into 64x64 RegionStore
/// snapshots rather than substituting RegionGenerator (different visual art).
/// Both backends preserve V2 terrain, coast, actors and mouse picking.
/// Not a large-world authority: WorldState still owns simulation and this
/// mirror uses sparse overrides to retain the existing generator's appearance.
/// </summary>
public sealed class RegionMirrorTerrainSource : ITerrainTileSource
{
    private readonly WorldState _world;
    private readonly RegionStore _regions;

    public int Width => _world.Width;
    public int Height => _world.Height;
    public int Seed => _world.Seed;
    public int ResidentRegions => _regions.ResidentRegions;
    public int MaxResidentRegions => _regions.MaxResidentRegions;

    public RegionMirrorTerrainSource(WorldState world, int maxResidentRegions = 8)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _regions = new RegionStore(world.Seed, maxResidentRegions);
        int regionsX = (world.Width - 1) / RegionCoordinates.Size + 1;
        int regionsY = (world.Height - 1) / RegionCoordinates.Size + 1;
        for (int ry = 0; ry < regionsY; ry++)
        for (int rx = 0; rx < regionsX; rx++)
        {
            var key = new RegionKey(rx, ry);
            var bytes = new byte[RegionCoordinates.CellCount];
            for (int ly = 0; ly < RegionCoordinates.Size; ly++)
            for (int lx = 0; lx < RegionCoordinates.Size; lx++)
            {
                int x = rx * RegionCoordinates.Size + lx;
                int y = ry * RegionCoordinates.Size + ly;
                // No phantom land outside the V2 world; the renderer never
                // reads these cells, but snapshots remain valid 64x64 data.
                bytes[ly * RegionCoordinates.Size + lx] = (byte)
                    (x < world.Width && y < world.Height
                        ? world.Tile(x,y) : Terrain.DeepWater);
            }
            _regions.RestoreRegion(new RegionSnapshot(key, world.Seed, 0, bytes));
        }
    }

    public Terrain Tile(int x, int y)
    {
        if (!_world.Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        return _regions.Tile(new CellCoord(x, y));
    }

    public void SynchronizeArea(int centerX, int centerY, int radius)
    {
        if (radius is < 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(radius));
        int left = Math.Max(0, centerX - radius);
        int right = Math.Min(Width - 1, centerX + radius);
        int top = Math.Max(0, centerY - radius);
        int bottom = Math.Min(Height - 1, centerY + radius);
        // Each touched tile is compared to the authoritative gameplay source;
        // world simulation still owns citizens, timing and generator.
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++)
            _regions.SetTile(new CellCoord(x,y), _world.Tile(x,y));
    }
}
