using Godot;
using WorldSim;

namespace WorldForge.Rendering;

/// <summary>
/// GPU-facing terrain canvas backed by original 8x8 pixel-art per world tile.
/// Keeps simulation terrain and its visual presentation fully separate.
/// </summary>
public partial class TerrainRenderer : Node2D
{
    public const float WorldTileSize = 8f;

    private WorldState? _world;
    private Image? _image;
    private ImageTexture? _texture;
    private DirtyTileRegion? _dirty;
    private byte[]? _rgba;

    // Diagnostic counters: CPU recolored texels and full GPU texture uploads.
    // ImageTexture.Update still updates the whole backing GPU image.
    public long TextureUploads { get; private set; }
    public long RecoloredPixels { get; private set; }

    public TerrainRenderer()
    {
        Name = "TerrainLayer";
        ZIndex = 0;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    public void BindWorld(WorldState world)
    {
        _world = world;
        int rasterWidth = checked(world.Width * TerrainPixelArt.TexelsPerTile);
        int rasterHeight = checked(world.Height * TerrainPixelArt.TexelsPerTile);
        bool resized = _rgba is null ||
                       _image is null ||
                       _image.GetWidth() != rasterWidth ||
                       _image.GetHeight() != rasterHeight;
        if (resized)
        {
            _rgba = new byte[checked(rasterWidth * rasterHeight * 4)];
            _image = null;
            _texture = null;
        }

        _dirty = new DirtyTileRegion(world.Width, world.Height);
        _dirty.MarkAll();
        FlushPending();
    }

    public void MarkBrush(int cx, int cy, int radius)
    {
        // A changed land/water boundary also affects the 8-pixel appearance
        // of adjacent cells, so mark one additional tile in every direction.
        _dirty?.MarkCircle(cx, cy, radius + 1);
    }

    public bool FlushPending()
    {
        if (_world is null || _rgba is null || _dirty is null ||
            !_dirty.TryConsume(out TileBounds bounds)) return false;

        TerrainPixelArt.Paint(_rgba, _world.Width, _world.Height, _world.Seed,
            _world.Tile, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

        RecoloredPixels += (long)bounds.Area * TerrainPixelArt.TexelsPerTile *
                           TerrainPixelArt.TexelsPerTile;
        int rasterWidth = _world.Width * TerrainPixelArt.TexelsPerTile;
        int rasterHeight = _world.Height * TerrainPixelArt.TexelsPerTile;
        if (_image is null)
            _image = Image.CreateFromData(rasterWidth, rasterHeight, false, Image.Format.Rgba8, _rgba);
        else
            _image.SetData(rasterWidth, rasterHeight, false, Image.Format.Rgba8, _rgba);

        if (_texture is null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
        TextureUploads++;
        QueueRedraw();
        return true;
    }

    public override void _Draw()
    {
        if (_world is not null && _texture is not null)
        {
            DrawTextureRect(_texture,
                new Rect2(0f, 0f, _world.Width * WorldTileSize, _world.Height * WorldTileSize),
                false);
        }
    }
}
