using System;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Godot-independent surface palette, corner blending and coast sampling.
/// Deterministic from the WorldSim terrain labels without touching simulation
/// RNG; supports live brush edits and 3D terrain chunk regeneration.
/// </summary>
public static class EnvironmentArtMath
{
    public readonly record struct Rgb(byte R, byte G, byte B)
    {
        public uint Packed => (uint)(R << 16 | G << 8 | B);
    }

    // Palette groups share a warm/cool painterly progression. Never allow
    // open water to bleed cyan into a grass/rock vertex color.
    private static readonly Rgb[] Ground =
    [
        new(40, 81, 106), new(84, 159, 174), // 0/1 water
        new(226, 200, 144),                   // 2 sand
        new(124, 167, 93),                    // 3 meadow
        new(80, 126, 90),                     // 4 forest
        new(142, 145, 144),                   // 5 mountain
        new(107, 83, 74)                      // 6 burned
    ];

    public static Rgb GroundColor(Terrain kind) =>
        Ground[Math.Clamp((int)kind, 0, Ground.Length - 1)];

    public static Rgb Mix(Rgb a, Rgb b, float weight)
    {
        weight = Math.Clamp(weight, 0f, 1f);
        static byte Blend(byte x, byte y, float t) =>
            (byte)Math.Clamp((int)MathF.Round(x + (y - x) * t), 0, 255);
        return new Rgb(Blend(a.R, b.R, weight),
                       Blend(a.G, b.G, weight),
                       Blend(a.B, b.B, weight));
    }

    public static bool Water(Terrain terrain) =>
        terrain is Terrain.DeepWater or Terrain.ShallowWater;

    /// <summary>
    /// A source tile's corner color is a subtle blend of adjacent *land*
    /// palette values; use golden sand at the water boundary, not dark blue.
    /// </summary>
    public static Rgb CornerColor(int tileX, int tileY, int offsetX, int offsetY,
        int width, int height, Func<int, int, Terrain> terrainAt, int seed)
    {
        if (offsetX is < 0 or > 1 || offsetY is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(offsetX));
        Terrain source = terrainAt(tileX, tileY);
        Rgb baseColor = GroundColor(source);
        if (!Water(source))
        {
            int dx = offsetX == 0 ? -1 : 1;
            int dy = offsetY == 0 ? -1 : 1;
            int[] xs = [tileX + dx, tileX, tileX + dx];
            int[] ys = [tileY, tileY + dy, tileY + dy];
            for (int n = 0; n < 3; n++)
            {
                int x = xs[n], y = ys[n];
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                Terrain neighbor = terrainAt(x, y);
                if (neighbor == source) continue;
                Rgb neighborColor = Water(neighbor)
                    ? GroundColor(Terrain.Sand) : GroundColor(neighbor);
                baseColor = Mix(baseColor, neighborColor, n == 2 ? .105f : .17f);
            }
        }
        float variation = ((int)(Hash(tileX * 7 + offsetX, tileY * 7 + offsetY,
            seed + 57) % 17) - 8) / 130f;
        return variation >= 0 ? Mix(baseColor, new Rgb(255, 255, 245), variation)
                              : Mix(baseColor, new Rgb(53, 62, 55), -variation);
    }

    /// <summary>
    /// RGBA mask channels: R = coastline foam (on water cells near land);
    /// G = shallow water blend; B = seeded subtle microvariation; A = 255.
    /// Filtered in one cheap sampling operation in Compatibility water shader.
    /// </summary>
    public static byte[] BuildShoreMask(int width, int height,
        Func<int, int, Terrain> at, int seed)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(at);
        byte[] bytes = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Terrain t = at(x, y);
            bool sea = Water(t);
            int cardinal = 0, diagonal = 0;
            if (sea)
            {
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    if (!Water(at(px, py)))
                    {
                        if (dx == 0 || dy == 0) cardinal++;
                        else diagonal++;
                    }
                }
            }
            int offset = (y * width + x) * 4;
            bytes[offset] = (byte)(sea ? Math.Min(235, cardinal * 78 + diagonal * 20) : 0);
            bytes[offset + 1] = (byte)(t == Terrain.ShallowWater ? 220 :
                                    t == Terrain.DeepWater ? 26 : 0);
            bytes[offset + 2] = (byte)(Hash(x, y, seed + 191) % 256);
            bytes[offset + 3] = 255;
        }
        return bytes;
    }

    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed ^ ((uint)x * 0x9E3779B9u) ^ ((uint)y * 0x85EBCA6Bu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            return h ^ (h >> 16);
        }
    }
}
