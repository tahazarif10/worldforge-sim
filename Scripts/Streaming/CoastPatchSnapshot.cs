using System;
using System.Threading;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.Streaming;

/// <summary>
/// Immutable local coast input. At most 40x40 terrain values: 32 interior,
/// two filtering texels, and six kernel halo cells per axis.
/// No live delegate or Godot object survives construction.
/// </summary>
public sealed class CoastPatchSnapshot
{
    public const int InteriorLimit = 32, FilterGutter = 1, KernelHalo = 3;
    public const int SourceSideLimit = InteriorLimit + 2 * (FilterGutter + KernelHalo);
    public const int WorldSideLimit = 8192;
    private readonly Terrain[] _tiles;
    private readonly int _sourceX, _sourceZ, _sourceWidth, _sourceHeight, _seed;
    public int InteriorX { get; }
    public int InteriorZ { get; }
    public int InteriorWidth { get; }
    public int InteriorHeight { get; }
    public int MaskX { get; }
    public int MaskZ { get; }
    public int MaskWidth { get; }
    public int MaskHeight { get; }
    public int CapturedCells => _tiles.Length;

    public CoastPatchSnapshot(int worldWidth, int worldHeight, int x, int z,
        int width, int height, int seed, Func<int, int, Terrain> tileAt)
    {
        ArgumentNullException.ThrowIfNull(tileAt);
        // Validate before allocation/source reads, without overflowing coordinates.
        if (worldWidth is < 1 or > WorldSideLimit || worldHeight is < 1 or > WorldSideLimit ||
            width is < 1 or > InteriorLimit || height is < 1 or > InteriorLimit ||
            x < 0 || z < 0 || x > worldWidth - width || z > worldHeight - height)
            throw new ArgumentOutOfRangeException(nameof(width), "Invalid bounded coast patch.");
        InteriorX = x; InteriorZ = z; InteriorWidth = width; InteriorHeight = height;
        MaskX = Math.Max(0, x - FilterGutter); MaskZ = Math.Max(0, z - FilterGutter);
        int right = Math.Min(worldWidth, x + width + FilterGutter);
        int bottom = Math.Min(worldHeight, z + height + FilterGutter);
        MaskWidth = right - MaskX; MaskHeight = bottom - MaskZ;
        _sourceX = Math.Max(0, MaskX - KernelHalo); _sourceZ = Math.Max(0, MaskZ - KernelHalo);
        _sourceWidth = Math.Min(worldWidth, right + KernelHalo) - _sourceX;
        _sourceHeight = Math.Min(worldHeight, bottom + KernelHalo) - _sourceZ;
        _seed = seed;
        _tiles = new Terrain[checked(_sourceWidth * _sourceHeight)];
        for (int row = 0; row < _sourceHeight; row++)
        for (int col = 0; col < _sourceWidth; col++)
        {
            Terrain tile = tileAt(_sourceX + col, _sourceZ + row);
            if ((byte)tile > (byte)Terrain.Burned)
                throw new ArgumentException("Invalid terrain in coast patch.", nameof(tileAt));
            _tiles[row * _sourceWidth + col] = tile;
        }
    }

    /// <summary>RGBA includes the clipped one-texel gutter, not the kernel halo.</summary>
    public byte[] Build(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        byte[] local = WaterCoastMath.BuildMask(_sourceWidth, _sourceHeight,
            (x,z) => _tiles[z * _sourceWidth + x], _seed, _sourceX, _sourceZ);
        byte[] output = new byte[checked(MaskWidth * MaskHeight * 4)];
        for (int row = 0; row < MaskHeight; row++)
        {
            token.ThrowIfCancellationRequested();
            local.AsSpan(((MaskZ - _sourceZ + row) * _sourceWidth + MaskX - _sourceX) * 4,
                MaskWidth * 4).CopyTo(output.AsSpan(row * MaskWidth * 4));
        }
        token.ThrowIfCancellationRequested();
        return output;
    }
}
