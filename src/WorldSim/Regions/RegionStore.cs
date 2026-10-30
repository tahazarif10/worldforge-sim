using System;
using System.Collections.Generic;

namespace WorldSim.Regions;

/// <summary>
/// Copy-owning immutable snapshot of one logical region.
/// World rendering may read it without touching the live simulation cache.
/// </summary>
public sealed class RegionSnapshot
{
    private readonly byte[] _tiles;
    public RegionKey Key { get; }
    public int Seed { get; }
    public long Revision { get; }

    public RegionSnapshot(RegionKey key, int seed, long revision, byte[] tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (tiles.Length != RegionCoordinates.CellCount)
            throw new ArgumentException("Region must have exactly 64x64 terrain bytes", nameof(tiles));
        Key = key;
        Seed = seed;
        Revision = revision;
        _tiles = (byte[])tiles.Clone();
    }

    public Terrain Tile(LocalCell cell)
    {
        if (cell.X is < 0 or >= RegionCoordinates.Size ||
            cell.Y is < 0 or >= RegionCoordinates.Size)
            throw new ArgumentOutOfRangeException(nameof(cell));
        return (Terrain)_tiles[RegionCoordinates.LocalIndex(cell)];
    }

    public byte[] CopyTiles() => (byte[])_tiles.Clone();
}

/// <summary>
/// Seeded region generator with a stable absolute-coordinate hash.
/// This is a DATA CONTRACT test generator, not final terrain art/erosion.
/// </summary>
public static class RegionGenerator
{
    public static Terrain TerrainAt(CellCoord cell, int seed)
    {
        // Use coarse neighboring lattice values for stable large biome shapes.
        long gx = RegionCoordinates.FloorDivide(cell.X, 24);
        long gy = RegionCoordinates.FloorDivide(cell.Y, 24);
        ulong noise = Hash(gx, gy, seed);
        uint value = (uint)(noise % 100);
        return value switch
        {
            < 18 => Terrain.DeepWater,
            < 25 => Terrain.ShallowWater,
            < 34 => Terrain.Sand,
            < 65 => Terrain.Grass,
            < 86 => Terrain.Forest,
            _ => Terrain.Mountain
        };
    }

    private static ulong Hash(long x, long y, int seed)
    {
        unchecked
        {
            ulong h = (ulong)x * 0x9E3779B185EBCA87UL;
            h ^= (ulong)y * 0xC2B2AE3D27D4EB4FUL;
            h ^= (uint)seed * 0x165667B19E3779F9UL;
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            return h ^ (h >> 33);
        }
    }
}

/// <summary>
/// Bounded resident cache with sparse per-region edits stored separately from
/// the evictable 64x64 byte arrays. No Godot dependency, no entire map array.
/// Persistence is via RegionCodec snapshots; this is not a streaming scheduler.
/// </summary>
public sealed class RegionStore
{
    private sealed class CacheEntry(byte[] tiles, LinkedListNode<RegionKey> node)
    {
        public byte[] Tiles { get; } = tiles;
        public LinkedListNode<RegionKey> Node { get; } = node;
    }

    private readonly Dictionary<RegionKey, CacheEntry> _resident = new();
    private readonly LinkedList<RegionKey> _lru = new();
    private readonly Dictionary<RegionKey, Dictionary<int, byte>> _overrides = new();
    private readonly Dictionary<RegionKey, long> _revision = new();

    public int Seed { get; }
    public int MaxResidentRegions { get; }
    public int ResidentRegions => _resident.Count;
    public int ModifiedRegions => _overrides.Count;

    public RegionStore(int seed, int maxResidentRegions = 32)
    {
        if (maxResidentRegions is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maxResidentRegions));
        Seed = seed;
        MaxResidentRegions = maxResidentRegions;
    }

    public Terrain Tile(CellCoord cell)
    {
        RegionKey key = RegionCoordinates.RegionOf(cell);
        int index = RegionCoordinates.LocalIndex(RegionCoordinates.LocalOf(cell));
        return (Terrain)GetTiles(key)[index];
    }

    public void SetTile(CellCoord cell, Terrain terrain)
    {
        if (!Enum.IsDefined(terrain)) throw new ArgumentOutOfRangeException(nameof(terrain));
        RegionKey key = RegionCoordinates.RegionOf(cell);
        int index = RegionCoordinates.LocalIndex(RegionCoordinates.LocalOf(cell));
        byte[] tiles = GetTiles(key);
        byte value = (byte)terrain;
        if (tiles[index] == value) return;

        tiles[index] = value;
        if (!_overrides.TryGetValue(key, out Dictionary<int, byte>? overrides))
        {
            overrides = new Dictionary<int, byte>();
            _overrides[key] = overrides;
        }
        overrides[index] = value;
        _revision[key] = GetRevision(key) + 1;
    }

    public void PaintCircle(CellCoord center, int radius, Terrain terrain)
    {
        if (radius is < 0 or > 64) throw new ArgumentOutOfRangeException(nameof(radius));
        for (long dy = -radius; dy <= radius; dy++)
        for (long dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > (long)radius * radius) continue;
            SetTile(new CellCoord(checked(center.X + dx), checked(center.Y + dy)), terrain);
        }
    }

    public RegionSnapshot ReadRegion(RegionKey key) =>
        new(key, Seed, GetRevision(key), GetTiles(key));

    public void RestoreRegion(RegionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Seed != Seed)
            throw new ArgumentException("Cannot restore a region from a different world seed", nameof(snapshot));

        RegionKey key = snapshot.Key;
        byte[] tiles = snapshot.CopyTiles();
        var overrides = new Dictionary<int, byte>();
        for (int y = 0; y < RegionCoordinates.Size; y++)
        for (int x = 0; x < RegionCoordinates.Size; x++)
        {
            int index = y * RegionCoordinates.Size + x;
            CellCoord absolute = RegionCoordinates.GlobalOf(key, new LocalCell(x, y));
            if (tiles[index] != (byte)RegionGenerator.TerrainAt(absolute, Seed))
                overrides[index] = tiles[index];
        }
        if (overrides.Count == 0) _overrides.Remove(key);
        else _overrides[key] = overrides;
        _revision[key] = snapshot.Revision;
        if (_resident.TryGetValue(key, out CacheEntry? existing))
            Array.Copy(tiles, existing.Tiles, tiles.Length);
    }

    public long GetRevision(RegionKey key) =>
        _revision.TryGetValue(key, out long revision) ? revision : 0;

    private byte[] GetTiles(RegionKey key)
    {
        if (_resident.TryGetValue(key, out CacheEntry? cached))
        {
            _lru.Remove(cached.Node);
            _lru.AddFirst(cached.Node);
            return cached.Tiles;
        }

        var tiles = new byte[RegionCoordinates.CellCount];
        for (int y = 0; y < RegionCoordinates.Size; y++)
        for (int x = 0; x < RegionCoordinates.Size; x++)
        {
            CellCoord absolute = RegionCoordinates.GlobalOf(key, new LocalCell(x, y));
            tiles[y * RegionCoordinates.Size + x] =
                (byte)RegionGenerator.TerrainAt(absolute, Seed);
        }

        if (_overrides.TryGetValue(key, out Dictionary<int, byte>? changes))
            foreach (var edit in changes) tiles[edit.Key] = edit.Value;

        var node = _lru.AddFirst(key);
        _resident.Add(key, new CacheEntry(tiles, node));
        while (_resident.Count > MaxResidentRegions)
        {
            LinkedListNode<RegionKey> last = _lru.Last!;
            _resident.Remove(last.Value);
            _lru.RemoveLast();
        }
        return tiles;
    }
}
