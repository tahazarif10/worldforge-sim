using Godot;
using WorldSim;

namespace WorldForge.Rendering;

/// <summary>
/// Owns terrain raster, GPU texture, dirty-region reconciliation and ground draw.
/// Citizens and effect redraws never invalidate the terrain CanvasItem.
/// </summary>
public partial class TerrainRenderer : Node2D
{
    public const float WorldTileSize = 8f;

    private static readonly Color[] Palette =
    [
        new("#183e75"), new("#3278ab"), new("#d7c17d"), new("#6aab55"),
        new("#367b43"), new("#78828c"), new("#503e35")
    ];

    private WorldState? _world;
    private Image? _image;
    private ImageTexture? _texture;
    private DirtyTileRegion? _dirty;

    // Diagnostics for performance comparisons (not a GPU timing measurement).
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
        bool resized = _image is null || _image.GetWidth() != world.Width ||
                       _image.GetHeight() != world.Height;
        if (resized)
        {
            _image = Image.CreateEmpty(world.Width, world.Height, false, Image.Format.Rgba8);
            _texture = null;
        }
        _dirty = new DirtyTileRegion(world.Width, world.Height);
        _dirty.MarkAll();
        FlushPending();
    }

    public void MarkBrush(int cx, int cy, int radius)
    {
        _dirty?.MarkCircle(cx, cy, radius);
    }

    public bool FlushPending()
    {
        if (_world is null || _image is null || _dirty is null ||
            !_dirty.TryConsume(out TileBounds bounds)) return false;

        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        for (int x = bounds.Left; x <= bounds.Right; x++)
            _image.SetPixel(x, y, Palette[(int)_world.Tile(x, y)]);

        RecoloredPixels += bounds.Area;
        if (_texture is null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
        TextureUploads++;
        // The resource may update without a CanvasItem redraw, but request one
        // when terrain changes to ensure consistent behavior across renderers.
        QueueRedraw();
        return true;
    }

    public override void _Draw()
    {
        if (_world is not null && _texture is not null)
            DrawTextureRect(
                _texture,
                new Rect2(0f, 0f, _world.Width * WorldTileSize, _world.Height * WorldTileSize),
                false);
    }
}
