using System;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Fast distance-graded coastline for a single small/medium active scene.
/// Two linear sweeps, O(width*height), with no radius-square neighborhood
/// scan. On future huge worlds the same method must be run per resident
/// water/coast region with a small cross-region halo, not globally.
/// RGBA: R=shore foam falloff; G=water shallow depth color;
/// B=stable seeded tint; A=255.
/// </summary>
public static class WaterCoastMath
{
    private const byte FarDistance = 5;

    public static byte[] BuildMask(int width, int height,
        Func<int, int, Terrain> tileAt, int seed)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(tileAt);
        int cells = checked(width * height);
        byte[] distance = new byte[cells];
        byte[] types = new byte[cells];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            Terrain tile = tileAt(x, y);
            types[index] = (byte)tile;
            distance[index] = EnvironmentArtMath.Water(tile) ? FarDistance : (byte)0;
        }
        // Manhattan 4-neighbor distance to land is conservative near shores,
        // monotonic and has no costly per-pixel sqrt/trig/heap allocation.
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            if (x > 0)
                distance[index] = Math.Min(distance[index],
                    (byte)Math.Min(FarDistance, distance[index - 1] + 1));
            if (y > 0)
                distance[index] = Math.Min(distance[index],
                    (byte)Math.Min(FarDistance, distance[index - width] + 1));
        }
        for (int y = height - 1; y >= 0; y--)
        for (int x = width - 1; x >= 0; x--)
        {
            int index = y * width + x;
            if (x + 1 < width)
                distance[index] = Math.Min(distance[index],
                    (byte)Math.Min(FarDistance, distance[index + 1] + 1));
            if (y + 1 < height)
                distance[index] = Math.Min(distance[index],
                    (byte)Math.Min(FarDistance, distance[index + width] + 1));
        }

        byte[] rgba = new byte[checked(cells * 4)];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int cell = y * width + x;
            Terrain tile = (Terrain)types[cell];
            bool water = EnvironmentArtMath.Water(tile);
            int i = cell * 4;
            rgba[i] = water ? distance[cell] switch
            {
                1 => (byte)226, 2 => (byte)114, 3 => (byte)45,
                _ => (byte)0
            } : (byte)0;
            rgba[i + 1] = tile switch
            {
                Terrain.ShallowWater => (byte)216,
                Terrain.DeepWater => (byte)28,
                _ => (byte)0
            };
            rgba[i + 2] = (byte)(EnvironmentArtMath.Hash(x, y, seed + 191) & 255);
            rgba[i + 3] = 255;
        }
        return rgba;
    }
}
