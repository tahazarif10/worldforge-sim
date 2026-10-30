using System;
using System.Collections.Generic;

namespace WorldSim;

public enum Terrain : byte { DeepWater, ShallowWater, Sand, Grass, Forest, Mountain, Burned }

public sealed class Citizen(int id, int x, int y, long bornAt)
{
    public int Id { get; } = id;
    public int X { get; internal set; } = x;
    public int Y { get; internal set; } = y;
    public long BornAt { get; } = bornAt;
}

/// <summary>Deterministic simulation; no dependency on Godot, drawing, or input APIs.</summary>
public sealed class WorldState
{
    private readonly Terrain[] _tiles;
    private readonly Random _rng;
    private int _nextId = 1;

    public WorldState(int width, int height, int seed)
    {
        if (width < 16 || height < 16 || width > 1024 || height > 1024)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        Seed = seed;
        _tiles = new Terrain[width * height];
        _rng = new Random(seed);
        People = new List<Citizen>();
        Generate();
    }

    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public long Tick { get; private set; }
    public List<Citizen> People { get; }
    public int PopulationLimit { get; set; } = 600;
    public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    public Terrain Tile(int x, int y) => Contains(x, y) ? _tiles[y * Width + x] : throw new ArgumentOutOfRangeException(nameof(x));
    public bool Walkable(int x, int y) => Contains(x, y) && _tiles[y * Width + x] is not (Terrain.DeepWater or Terrain.ShallowWater or Terrain.Mountain);

    public void Paint(int cx, int cy, int radius, Terrain kind)
    {
        Circle(cx, cy, radius, (x, y) => _tiles[y * Width + x] = kind);
        People.RemoveAll(p => !Walkable(p.X, p.Y));
    }

    public void Meteor(int cx, int cy, int radius)
    {
        Circle(cx, cy, radius, (x, y) =>
        {
            int i = y * Width + x;
            if (_tiles[i] is not (Terrain.DeepWater or Terrain.ShallowWater)) _tiles[i] = Terrain.Burned;
        });
        People.RemoveAll(p => DistanceSquared(p.X, p.Y, cx, cy) <= (long)radius * radius);
    }

    public bool Spawn(int x, int y)
    {
        if (!Walkable(x, y) || People.Count >= PopulationLimit) return false;
        People.Add(new Citizen(_nextId++, x, y, Tick));
        return true;
    }

    public void Advance()
    {
        Tick++;
        foreach (Citizen p in People)
        {
            if (_rng.NextDouble() >= 0.70) continue;
            int direction = _rng.Next(4);
            int nx = p.X + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
            int ny = p.Y + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
            if (Walkable(nx, ny)) { p.X = nx; p.Y = ny; }
        }
        if (Tick % 45 == 0 && People.Count is >= 2 && People.Count < PopulationLimit)
        {
            int count = Math.Min(People.Count, 60);
            for (int i = 0; i < count && People.Count < PopulationLimit; i++)
            {
                if (_rng.NextDouble() > 0.06) continue;
                Citizen p = People[_rng.Next(count)];
                Spawn(p.X + _rng.Next(-2, 3), p.Y + _rng.Next(-2, 3));
            }
        }
        if (Tick % 60 == 0) People.RemoveAll(p => Tick - p.BornAt > 3500);
    }

    private void Circle(int cx, int cy, int radius, Action<int, int> apply)
    {
        if (radius < 0 || radius > 64) throw new ArgumentOutOfRangeException(nameof(radius));
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(Height - 1, cy + radius); y++)
        for (int x = Math.Max(0, cx - radius); x <= Math.Min(Width - 1, cx + radius); x++)
            if (DistanceSquared(x, y, cx, cy) <= (long)radius * radius) apply(x, y);
    }

    private static long DistanceSquared(int x, int y, int cx, int cy)
    {
        long dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy;
    }

    private void Generate()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double dx = (x + .5 - Width * .5) / (Width * .5);
            double dy = (y + .5 - Height * .5) / (Height * .5);
            double radius = Math.Sqrt(dx * dx + dy * dy) / Math.Sqrt(2);
            double elevation = Noise(x, y, Seed, .55) * .53 + Math.Max(0, 1 - radius * 1.28) * .64 - .13;
            double moisture = Noise(x + 344, y - 753, Seed ^ 0x00b0a013, .32);
            _tiles[y * Width + x] = elevation switch
            {
                < .26 => Terrain.DeepWater,
                < .34 => Terrain.ShallowWater,
                < .395 => Terrain.Sand,
                > .77 => Terrain.Mountain,
                _ => moisture > .57 ? Terrain.Forest : Terrain.Grass
            };
        }
    }

    private static double Noise(int x, int y, int seed, double weight) =>
        Value(x / 48.0, y / 48.0, seed) * weight
        + Value(x / 18.0, y / 18.0, seed + 71) * .31
        + Value(x / 7.0, y / 7.0, seed + 153) * (.69 - weight);

    private static double Value(double x, double y, int seed)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Mix(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), fx);
        double b = Mix(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), fx);
        return Mix(a, b, fy);
    }

    private static double Mix(double a, double b, double t) => a + (b - a) * t;

    private static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed ^ ((uint)x * 0x9e3779b9u) ^ ((uint)y * 0x85ebca6bu);
            h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
            return h / (double)uint.MaxValue;
        }
    }
}
