using Godot;
using WorldSim;

namespace WorldForge.Rendering;

/// <summary>
/// Draws citizens independently of terrain. Tick/spawn/death triggers only
/// this CanvasItem's invalidation, not a terrain raster upload.
/// </summary>
public partial class EntityRenderer : Node2D
{
    private static readonly Color Outline = new("#292a33");
    private static readonly Color Skin = new("#ffe0ad");
    private WorldState? _world;

    public long DrawPasses { get; private set; }

    public EntityRenderer()
    {
        Name = "EntityLayer";
        ZIndex = 10;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    public void BindWorld(WorldState world)
    {
        _world = world;
        MarkChanged();
    }

    public void MarkChanged() => QueueRedraw();

    public override void _Draw()
    {
        DrawPasses++;
        if (_world is null) return;
        foreach (Citizen person in _world.People)
        {
            Vector2 center = new(
                (person.X + 0.5f) * TerrainRenderer.WorldTileSize,
                (person.Y + 0.5f) * TerrainRenderer.WorldTileSize);
            DrawCircle(center, 3.1f, Outline);
            DrawCircle(center, 2.2f, Skin);
        }
    }
}
