using System;
using System.Collections.Generic;

namespace WorldSim;

/// <summary>
/// Minimal authoritative resources: wood is attached to the existing
/// forest-tree scatter, berries to a sparse seeded grassland site.
/// No camera, render meshes, engine time or additional simulation RNG calls.
/// </summary>
public enum ResourceKind : byte { None, Wood, Food }

public readonly record struct ResourceDeposit(ResourceKind Kind, int Remaining, int Capacity);
public readonly record struct GatherResult(ResourceKind Kind, int Collected, int Remaining);
public readonly record struct ResourceSite(int X, int Y, int Remaining, int Capacity);

public sealed partial class WorldResources
{
    public const long FoodRegrowthTicks = 40;

    private readonly int _width, _height, _seed;
    private readonly ResourceKind[] _kinds;
    private readonly byte[] _amounts;
    private readonly byte[] _capacity;
    private readonly long[] _nextGrowth;
    private readonly SortedSet<int> _foodSites = new();
    // Only exhausted tree sites are stored. The underlying forest biome is
    // still Forest and retains its terrain, paths and seeded world identity.
    private readonly SortedSet<int> _stumpSites = new();
    private readonly Queue<int> _recentlyFelled=new();
    private readonly HashSet<int> _growingFood = new();
    private readonly List<int> _completedGrowth = new();
    private long _revision;

    public long Revision => _revision;
    public int WoodCollected { get; private set; }
    public int FoodCollected { get; private set; }
    public int FoodSiteCount => _foodSites.Count;
    public int StumpCount => _stumpSites.Count;

    /// <summary>Stable identity for a resource tile within this world.</summary>
    public int SiteId(int x,int y) => x>=0 && x<_width && y>=0 && y<_height
        ? checked(y*_width+x+1) : 0;

    public bool HasStump(int x,int y)
    {
        int id=SiteId(x,y);
        return id>0 && _stumpSites.Contains(id-1);
    }

    /// <summary>Transient visual notices; never drive simulation logic.</summary>
    public bool TryTakeFelled(out ResourceSite site)
    {
        if(_recentlyFelled.Count==0){site=default;return false;}
        int index=_recentlyFelled.Dequeue();
        site=new(index%_width,index/_width,0,_capacity[index]);
        return true;
    }

    public IEnumerable<ResourceSite> StumpSites()
    {
        foreach(int index in _stumpSites)
            yield return new(index%_width,index/_width,0,_capacity[index]);
    }

    public WorldResources(int width, int height, int seed, Func<int, int, Terrain> terrainAt)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(terrainAt);
        _width = width;
        _height = height;
        _seed = seed;
        _kinds = new ResourceKind[checked(width * height)];
        _amounts = new byte[_kinds.Length];
        _capacity = new byte[_kinds.Length];
        _nextGrowth = new long[_kinds.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            SetBiome(x, y, terrainAt(x, y));
    }

    public ResourceDeposit At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return default;
        int index = y * _width + x;
        return new(_kinds[index], _amounts[index], _capacity[index]);
    }

    /// <summary>
    /// Forest-tree placement uses the same deterministic hash and 5-of-7
    /// selection gate as NatureScatter.Pick. No WorldSim dependency on Godot.
    /// Grass berries are sparse and intentionally separate from decorative
    /// plants; they do not change the accepted foliage's geometry/materials.
    /// </summary>
    public void SetBiome(int x, int y, Terrain biome)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            throw new ArgumentOutOfRangeException(nameof(x));
        int index = y * _width + x;
        uint hash = ScatterHash(x, y, unchecked(_seed + 119));
        ResourceKind kind = biome switch
        {
            Terrain.Forest when hash % 7 < 5 => ResourceKind.Wood,
            Terrain.Grass when hash % 19 == 4 => ResourceKind.Food,
            _ => ResourceKind.None
        };
        byte max = kind switch
        {
            ResourceKind.Wood => (byte)(5 + (hash >> 6) % 4),
            ResourceKind.Food => (byte)(2 + (hash >> 8) % 3),
            _ => (byte)0
        };
        _growingFood.Remove(index);
        _stumpSites.Remove(index);
        _nextGrowth[index] = 0;
        _foodSites.Remove(index);
        _kinds[index] = kind;
        _amounts[index] = max;
        _capacity[index] = max;
        if (kind == ResourceKind.Food) _foodSites.Add(index);
        _revision++;
    }

    /// <summary>
    /// A successful click collects exactly one unit. Wood remains depleted
    /// for now; food regrows on WorldState ticks even when it is off camera.
    /// </summary>
    public bool TryGather(int x, int y, long tick, out GatherResult result)
    {
        result = default;
        if (x < 0 || y < 0 || x >= _width || y >= _height) return false;
        int index = y * _width + x;
        ResourceKind kind = _kinds[index];
        if (kind == ResourceKind.None || _amounts[index] == 0) return false;
        --_amounts[index];
        if (kind == ResourceKind.Wood)
        {
            WoodCollected++;
            if (_amounts[index] == 0)
            {
                _stumpSites.Add(index);
                _recentlyFelled.Enqueue(index);
            }
        }
        else
        {
            FoodCollected++;
            if (_growingFood.Add(index))
                _nextGrowth[index] = checked(tick + FoodRegrowthTicks);
        }
        _revision++;
        result = new(kind, 1, _amounts[index]);
        return true;
    }

    public void Advance(long tick)
    {
        if (_growingFood.Count == 0) return;
        _completedGrowth.Clear();
        foreach (int index in _growingFood)
        {
            if (tick < _nextGrowth[index]) continue;
            if (_kinds[index] != ResourceKind.Food)
            {
                _completedGrowth.Add(index);
                continue;
            }

            if (_amounts[index] < _capacity[index])
            {
                _amounts[index]++;
                _revision++;
            }
            if (_amounts[index] >= _capacity[index])
                _completedGrowth.Add(index);
            else
                _nextGrowth[index] = checked(tick + FoodRegrowthTicks);
        }
        foreach (int index in _completedGrowth) _growingFood.Remove(index);
    }

    public IEnumerable<ResourceSite> FoodSites()
    {
        foreach (int index in _foodSites)
            yield return new(index % _width, index / _width,
                _amounts[index], _capacity[index]);
    }

    private static uint ScatterHash(int x, int y, int seed)
    {
        // Identical to EnvironmentArtMath.Hash without referencing Godot.
        unchecked
        {
            uint h = (uint)seed ^ ((uint)x * 0x9E3779B9u) ^ ((uint)y * 0x85EBCA6Bu);
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15;
            h *= 0x846CA68Bu;
            return h ^ (h >> 16);
        }
    }
}
