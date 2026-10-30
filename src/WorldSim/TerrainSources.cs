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
/// Temporary parity bridge: lazily caches the *actual V2 tiles* in 64x64
/// RegionStore areas rather than using RegionGenerator (different visual art).
/// Both backends preserve V2 terrain, coast, actors and mouse picking.
/// Not a large-world authority: WorldState still owns simulation and the source resamples WorldState on region cache misses, so no duplicated
/// WorldState-size initialization or persistent override dictionary is needed.
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
        // Read the actual WorldState as the baseline *on every region
        // regeneration*. An evicted region never falls back to the different
        // experimental RegionGenerator, even after V2 painting or meteor.
        _regions = new RegionStore(world.Seed, maxResidentRegions, cell =>
            cell.X >= 0 && cell.Y >= 0 &&
            cell.X < world.Width && cell.Y < world.Height
                ? world.Tile((int)cell.X, (int)cell.Y)
                : Terrain.DeepWater);

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
