using System;
using WorldSim;

namespace WorldForge.Rendering;

/// <summary>
/// Entirely original deterministic pixel art for terrain. The 8x8 logical
/// tile grid generates 8x8 actual RGBA texels per world tile, avoiding
/// blurry 1-texel solid-color tiles. No Godot dependency; testable in CI.
/// </summary>
public static class TerrainPixelArt
{
    public const int TexelsPerTile = 8;

    // Three tonal steps per terrain, plus drawn shoreline/vegetation details.
    // Colors are packed RRGGBB to avoid allocating any per-pixel objects.
    private static readonly uint[] Shadow =
    [
        0x102B53u, 0x22577Eu, 0xAC9557u, 0x457A36u,
        0x204D31u, 0x505B68u, 0x322A27u
    ];

    private static readonly uint[] Base =
    [
        0x1B4376u, 0x3482ADu, 0xD8C37Fu, 0x72B459u,
        0x347A48u, 0x8794A1u, 0x55413Bu
    ];

    private static readonly uint[] Highlight =
    [
        0x376391u, 0x66B3D0u, 0xF0D99Au, 0x99CE6Du,
        0x61AA68u, 0xBAC3C7u, 0x887069u
    ];

    public static byte[] Build(int width, int height, int seed, Func<int, int, Terrain> at)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(at);
        int bytes = checked(checked(width * TexelsPerTile) * checked(height * TexelsPerTile) * 4);
        byte[] pixels = new byte[bytes];
        Paint(pixels, width, height, seed, at, 0, 0, width - 1, height - 1);
        return pixels;
    }

    /// <summary>
    /// Repaint an inclusive rectangle of tile cells. No new array allocation.
    /// The caller must include a 1-tile halo when terrain classes change,
    /// because adjacent tiles may gain or lose shoreline edge pixels.
    /// </summary>
    public static void Paint(byte[] pixels, int width, int height, int seed,
        Func<int, int, Terrain> at, int left, int top, int right, int bottom)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(at);
        ArgumentNullException.ThrowIfNull(pixels);
        int stride = checked(width * TexelsPerTile);
        int required = checked(stride * checked(height * TexelsPerTile) * 4);
        if (pixels.Length != required)
            throw new ArgumentException("RGBA8 pixel buffer dimensions do not match terrain dimensions.", nameof(pixels));
        if (left > right || top > bottom) return;

        left = Math.Clamp(left, 0, width - 1);
        top = Math.Clamp(top, 0, height - 1);
        right = Math.Clamp(right, 0, width - 1);
        bottom = Math.Clamp(bottom, 0, height - 1);

        for (int tileY = top; tileY <= bottom; tileY++)
        for (int tileX = left; tileX <= right; tileX++)
        {
            Terrain kind = at(tileX, tileY);
            uint variant = Hash(tileX, tileY, seed);
            bool water = IsWater(kind);
            bool coastNorth = tileY > 0 && IsWater(at(tileX, tileY - 1)) != water;
            bool coastSouth = tileY + 1 < height && IsWater(at(tileX, tileY + 1)) != water;
            bool coastWest = tileX > 0 && IsWater(at(tileX - 1, tileY)) != water;
            bool coastEast = tileX + 1 < width && IsWater(at(tileX + 1, tileY)) != water;
            bool nearForest = kind == Terrain.Grass &&
                ((tileX > 0 && at(tileX - 1, tileY) == Terrain.Forest) ||
                 (tileX + 1 < width && at(tileX + 1, tileY) == Terrain.Forest) ||
                 (tileY > 0 && at(tileX, tileY - 1) == Terrain.Forest) ||
                 (tileY + 1 < height && at(tileX, tileY + 1) == Terrain.Forest));

            for (int py = 0; py < TexelsPerTile; py++)
            for (int px = 0; px < TexelsPerTile; px++)
            {
                int globalX = tileX * TexelsPerTile + px;
                int globalY = tileY * TexelsPerTile + py;
                uint fleck = Hash(globalX, globalY, seed + 79);
                int biome = (int)kind;
                int d = (int)(fleck % 100);
                uint rgb = d < 18 ? Shadow[biome] : d > 89 ? Highlight[biome] : Base[biome];

                // Draw original little tile decorations, chosen by hash so every
                // forest/mountain patch has recognizable detail without assets.
                if (kind == Terrain.Forest)
                {
                    int cx = 3 + (int)(variant % 3) - 1;
                    int cy = 3 + (int)((variant >> 3) % 3) - 1;
                    int dx = Math.Abs(px - cx), dy = Math.Abs(py - cy);
                    if (dx <= 2 && dy <= 2 && dx + dy <= 4)
                    {
                        rgb = (px + py + (int)(variant & 3)) % 5 == 0 ? 0x79C275u :
                              py <= cy && px <= cx ? 0x4EAA62u : 0x2D7545u;
                    }
                    else if (px == cx && py == Math.Min(7, cy + 3)) rgb = 0x6A563Bu;
                }
                else if (kind == Terrain.Mountain)
                {
                    int ridgeline = (int)((variant >> 5) % 3) + 2 + (px / 3);
                    if (py == ridgeline || py == ridgeline + 1) rgb = 0xCFD7D4u;
                    if (py > ridgeline + 1 && px > 3 && (px + py) % 4 == 0) rgb = 0x596777u;
                }
                else if (kind == Terrain.Grass)
                {
                    if ((variant & 3) == 0 && px == 2 && py >= 4 && py <= 6) rgb = 0xB5D77Du;
                    if (((variant >> 4) & 3) == 1 && px == 5 && py == 2) rgb = 0xC4C85Du;
                    if (nearForest && d < 10) rgb = 0x4B9150u;
                }
                else if (kind == Terrain.Sand)
                {
                    if (d < 7) rgb = 0xB5A16Eu;
                    if (d >= 97) rgb = 0xFFF0B6u;
                }
                else if (water)
                {
                    // Sparse horizontal wave glyphs; repeated uniformly in
                    // one region while remaining deterministic across builds.
                    if ((variant & 3) == 0 && py == 3 && px >= 2 && px <= 5)
                        rgb = kind == Terrain.DeepWater ? 0x487CADu : 0x80CAE0u;
                    if (((variant >> 4) & 3) == 1 && py == 6 && px >= 1 && px <= 3)
                        rgb = 0x4D94BAu;
                }
                else if (kind == Terrain.Burned)
                {
                    if (d < 10) rgb = 0x221F21u;
                    if (d >= 97) rgb = 0xB77A57u;
                }

                // Continuous coast transition based on N/S/E/W neighbor
                // classes. No tile atlas seams or proprietary sprite assets.
                bool onBorder = (coastNorth && py <= 1) || (coastSouth && py >= 6) ||
                                (coastWest && px <= 1) || (coastEast && px >= 6);
                if (onBorder)
                {
                    bool veryEdge = (coastNorth && py == 0) || (coastSouth && py == 7) ||
                                    (coastWest && px == 0) || (coastEast && px == 7);
                    if (water)
                        rgb = veryEdge ? 0xD3E8CDu : 0x89C9CDu;
                    else if (kind == Terrain.Sand)
                        rgb = veryEdge ? 0xF7E5B1u : 0xC0A76Eu;
                    else
                        rgb = veryEdge ? 0xE4CF90u : 0x91B26Au;
                }

                int offset = checked((globalY * stride + globalX) * 4);
                pixels[offset] = (byte)(rgb >> 16);
                pixels[offset + 1] = (byte)(rgb >> 8);
                pixels[offset + 2] = (byte)rgb;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static bool IsWater(Terrain kind) =>
        kind is Terrain.DeepWater or Terrain.ShallowWater;

    private static uint Hash(int x, int y, int seed)
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
