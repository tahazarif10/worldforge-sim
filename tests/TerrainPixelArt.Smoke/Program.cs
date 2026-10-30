using System;
using System.Linq;
using WorldSim;
using WorldForge.Rendering;

static void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}

const int width = 48, height = 32, seed = 20261030;
var world = new WorldState(width, height, seed);
byte[] first = TerrainPixelArt.Build(width, height, seed, world.Tile);
byte[] repeated = TerrainPixelArt.Build(width, height, seed, world.Tile);

Check(first.Length == width * height * 8 * 8 * 4, "8x8 pixels per terrain tile");
Check(first.SequenceEqual(repeated), "same seed and terrain have identical pixel art");
Check(!first.SequenceEqual(TerrainPixelArt.Build(width, height, seed + 1, world.Tile)),
    "visual seed changes fine texture independently of simulation");
Check(first.Where((_, index) => index % 4 == 3).All(alpha => alpha == 255),
    "terrain texture fully opaque");

const int cx = 24, cy = 16;
byte[] partial = (byte[])first.Clone();
world.Paint(cx, cy, 0, Terrain.Sand);
TerrainPixelArt.Paint(partial, width, height, seed, world.Tile,
    cx - 1, cy - 1, cx + 1, cy + 1);
byte[] fullRebuild = TerrainPixelArt.Build(width, height, seed, world.Tile);
Check(partial.SequenceEqual(fullRebuild),
    "painting one cell plus 1-tile halo equals complete rebuild");
Check(first.Take(4).SequenceEqual(partial.Take(4)),
    "distant pixels untouched by local terrain update");

var beach = new Terrain[3, 3];
for (int y = 0; y < 3; y++)
for (int x = 0; x < 3; x++)
    beach[x, y] = Terrain.Grass;
beach[1, 1] = Terrain.Sand;
beach[1, 0] = Terrain.ShallowWater;
byte[] coast = TerrainPixelArt.Build(3, 3, seed, (x, y) => beach[x, y]);
int scanWidth = 3 * TerrainPixelArt.TexelsPerTile;
static uint PixelRgb(byte[] data, int stride, int px, int py)
{
    int i = (py * stride + px) * 4;
    return ((uint)data[i] << 16) | ((uint)data[i + 1] << 8) | data[i + 2];
}
Check(PixelRgb(coast, scanWidth, 12, 8) == 0xF7E5B1u,
    "beach border highlights at north water boundary");
Check(PixelRgb(coast, scanWidth, 12, 7) == 0xD3E8CDu,
    "water side gains pale foam along coast");

Check(!TerrainPixelArt.Build(3, 3, seed, (x, y) => beach[x, y])
    .SequenceEqual(TerrainPixelArt.Build(3, 3, seed, (x, y) =>
        x == 1 && y == 0 ? Terrain.DeepWater : beach[x, y])),
    "deep versus shallow water has visually distinct fill");

bool badLength = false;
try
{
    TerrainPixelArt.Paint(new byte[4], 3, 3, seed, (x,y) => beach[x,y], 0, 0, 2, 2);
}
catch (ArgumentException) { badLength = true; }
Check(badLength, "reject corrupt pixel buffer length");

Console.WriteLine("PASS ALL terrain pixel-art smoke tests");
