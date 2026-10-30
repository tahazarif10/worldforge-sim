using System;
using Godot;
using WorldSim;
using WorldForge.UI;

namespace WorldForge;

public partial class GameRoot : Node2D
{
    private const float TileSize = 8;
    private const double TickSeconds = 0.18;
    private const double HudRefreshIntervalSeconds = 0.25;
    private const double PerformanceSampleIntervalSeconds = 1.0;
    private static readonly Color CitizenOutline = new("#292a33");
    private static readonly Color CitizenFace = new("#ffe0ad");
    private static readonly Color[] Colors =
    [
        new("#183e75"), new("#3278ab"), new("#d7c17d"), new("#6aab55"),
        new("#367b43"), new("#78828c"), new("#503e35")
    ];
    private enum Brush { Land, Ocean, Sand, Forest, Mountain, Person, Meteor }
    private WorldState _world = null!;
    private Camera2D _camera = null!;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private GameHud _hud = null!;
    private Brush _brush = Brush.Person;
    private int _radius = 3;
    private int _speed = 1;
    private double _elapsed;
    private bool _panning;
    private bool _terrainDirty;
    private double _hudClock;
    private double _perfClock;
    private double _worstFrameMilliseconds;
    private int _frameSamples;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        CreateWorld(20261030);
        _camera = new Camera2D
        {
            Enabled = true,
            Position = new Vector2(_world.Width * TileSize / 2, _world.Height * TileSize / 2),
            Zoom = new Vector2(.85f, .85f)
        };
        AddChild(_camera);
        SetupUi();
    }

    private void CreateWorld(int seed)
    {
        _world = new WorldState(160, 120, seed);
        _image = Image.CreateEmpty(_world.Width, _world.Height, false, Image.Format.Rgba8);
        _elapsed = 0;
        _terrainDirty = false;
        UpdateTerrain();
    }

    private void UpdateTerrain()
    {
        for (int y = 0; y < _world.Height; y++)
        for (int x = 0; x < _world.Width; x++)
            _image.SetPixel(x, y, Colors[(int)_world.Tile(x, y)]);
        if (_texture is null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTextureRect(_texture, new Rect2(0, 0, _world.Width * TileSize, _world.Height * TileSize), false);
        foreach (Citizen person in _world.People)
        {
            Vector2 center = new((person.X + .5f) * TileSize, (person.Y + .5f) * TileSize);
            DrawCircle(center, 3.1f, CitizenOutline);
            DrawCircle(center, 2.2f, CitizenFace);
        }
    }

    public override void _Process(double delta)
    {
        // Input events may arrive many times between rendered frames. Upload the terrain
        // texture once per frame rather than once per pointer movement.
        if (_terrainDirty)
        {
            _terrainDirty = false;
            UpdateTerrain();
        }

        if (_speed > 0)
        {
            _elapsed += delta * _speed;
            int steps = 0;
            while (_elapsed >= TickSeconds && steps++ < 12)
            {
                _world.Advance();
                _elapsed -= TickSeconds;
                QueueRedraw();
            }
            if (_elapsed > TickSeconds * 12) _elapsed = TickSeconds;
        }
        _hudClock += delta;
        if (_hudClock >= HudRefreshIntervalSeconds)
        {
            _hudClock = 0;
            RefreshHud();
        }

        // Sample frame time without formatting strings or mutating Controls every frame.
        _frameSamples++;
        _perfClock += delta;
        _worstFrameMilliseconds = Math.Max(_worstFrameMilliseconds, delta * 1000.0);
        if (_perfClock >= PerformanceSampleIntervalSeconds)
        {
            int fps = (int)Math.Round(_frameSamples / _perfClock);
            _hud.UpdatePerformance(fps, _worstFrameMilliseconds);
            _perfClock = 0;
            _frameSamples = 0;
            _worstFrameMilliseconds = 0;
        }
    }

    private void RefreshHud() =>
        _hud.UpdateState(_brush.ToString(), _radius, _speed, _world.Seed, _world.People.Count, _world.Tick);

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed) Zoom(1.12f);
            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed) Zoom(1f / 1.12f);
            if (button.ButtonIndex == MouseButton.Right) _panning = button.Pressed;
            if (button.ButtonIndex == MouseButton.Left && button.Pressed) Apply();
        }
        else if (input is InputEventMouseMotion motion)
        {
            if (_panning) _camera.Position -= motion.Relative / _camera.Zoom;
            else if (Input.IsMouseButtonPressed(MouseButton.Left)) Apply();
        }
        else if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Space) _speed = _speed == 0 ? 1 : 0;
            if (key.Keycode == Key.Key1) _speed = 1;
            if (key.Keycode == Key.Key2) _speed = 2;
            if (key.Keycode == Key.Key3) _speed = 4;
            RefreshHud();
        }
    }

    private void Zoom(float by)
    {
        float value = Mathf.Clamp(_camera.Zoom.X * by, .35f, 4f);
        _camera.Zoom = Vector2.One * value;
    }

    private void Apply()
    {
        Vector2 pos = GetGlobalMousePosition() / TileSize;
        int x = Mathf.FloorToInt(pos.X), y = Mathf.FloorToInt(pos.Y);
        if (!_world.Contains(x, y)) return;
        switch (_brush)
        {
            case Brush.Land: _world.Paint(x, y, _radius, Terrain.Grass); break;
            case Brush.Ocean: _world.Paint(x, y, _radius, Terrain.DeepWater); break;
            case Brush.Sand: _world.Paint(x, y, _radius, Terrain.Sand); break;
            case Brush.Forest: _world.Paint(x, y, _radius, Terrain.Forest); break;
            case Brush.Mountain: _world.Paint(x, y, _radius, Terrain.Mountain); break;
            case Brush.Person: _world.Spawn(x, y); break;
            case Brush.Meteor: _world.Meteor(x, y, Math.Max(2, _radius)); break;
        }
        if (_brush != Brush.Person) _terrainDirty = true;
        QueueRedraw();
    }

    private void SetupUi()
    {
        var uiLayer = new CanvasLayer();
        AddChild(uiLayer);
        _hud = new GameHud();
        uiLayer.AddChild(_hud);

        // Top-wide anchors adapt to 1280x800 and larger native game windows.
        _hud.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _hud.OffsetLeft = 16;
        _hud.OffsetRight = -16;
        _hud.OffsetTop = 16;
        _hud.OffsetBottom = 164;

        _hud.ToolSelected += index => { _brush = (Brush)index; RefreshHud(); };
        _hud.RadiusChangeRequested += delta => { _radius = Math.Clamp(_radius + delta, 0, 12); RefreshHud(); };
        _hud.SpeedRequested += speed => { _speed = speed; RefreshHud(); };
        _hud.NewWorldRequested += () => { CreateWorld(Random.Shared.Next()); RefreshHud(); };
        _hud.SetSelectedTool((int)_brush);
        RefreshHud();
    }
}
