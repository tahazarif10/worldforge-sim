using System;
using Godot;
using WorldSim;
using WorldForge.UI;
using WorldForge.Camera;
using WorldForge.Rendering;

namespace WorldForge;

public partial class GameRoot : Node2D
{
    private const float TileSize = 8;
    private const double TickSeconds = 0.18;
    private const double HudRefreshIntervalSeconds = 0.25;
    private const double PerformanceSampleIntervalSeconds = 1.0;
    private enum Brush { Land, Ocean, Sand, Forest, Mountain, Person, Meteor }
    private WorldState _world = null!;
    private Camera2D _camera = null!;
    private WorldCameraController _cameraController = null!;
    private TerrainRenderer _terrainRenderer = null!;
    private EntityRenderer _entityRenderer = null!;
    private GameHud _hud = null!;
    private Brush _brush = Brush.Person;
    private int _radius = 3;
    private int _speed = 1;
    private double _elapsed;
    private bool _panning;
    private double _hudClock;
    private double _perfClock;
    private double _worstFrameMilliseconds;
    private int _frameSamples;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _terrainRenderer = new TerrainRenderer();
        AddChild(_terrainRenderer);
        _entityRenderer = new EntityRenderer();
        AddChild(_entityRenderer);
        // Dedicated foreground layer for future particles/impact visuals.
        AddChild(new Node2D { Name = "EffectsLayer", ZIndex = 20 });

        CreateWorld(20261030);
        _camera = new Camera2D
        {
            Enabled = true,
            Position = new Vector2(_world.Width * TileSize / 2, _world.Height * TileSize / 2),
            Zoom = Vector2.One
        };
        AddChild(_camera);
        _cameraController = new WorldCameraController(
            _camera, GetViewport(), new Vector2(_world.Width * TileSize, _world.Height * TileSize));
        GetViewport().SizeChanged += _cameraController.KeepInBounds;
        SetupUi();
    }

    private void CreateWorld(int seed)
    {
        _world = new WorldState(160, 120, seed);
        _elapsed = 0;
        _terrainRenderer.BindWorld(_world);
        _entityRenderer.BindWorld(_world);
    }

    public override void _Process(double delta)
    {
        Vector2 keyboardPan = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) keyboardPan.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) keyboardPan.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) keyboardPan.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) keyboardPan.Y += 1f;
        _cameraController.PanByKeyboard(keyboardPan, delta);
        // Only terrain changes require GPU texture upload. Multiple brush
        // inputs coalesce into a bounded CPU pixel update once per frame.
        _terrainRenderer.FlushPending();

        if (_speed > 0)
        {
            _elapsed += delta * _speed;
            int steps = 0;
            while (_elapsed >= TickSeconds && steps++ < 12)
            {
                _world.Advance();
                _elapsed -= TickSeconds;
                _entityRenderer.MarkChanged();
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
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
                _cameraController.ZoomAtScreen(button.Position, +1);
            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
                _cameraController.ZoomAtScreen(button.Position, -1);
            if (button.ButtonIndex == MouseButton.Right) _panning = button.Pressed;
            if (button.ButtonIndex == MouseButton.Left && button.Pressed) Apply();
        }
        else if (input is InputEventMouseMotion motion)
        {
            if (_panning && Input.IsMouseButtonPressed(MouseButton.Right))
                _cameraController.DragByPixels(motion.Relative);
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
        if (_brush != Brush.Person)
            _terrainRenderer.MarkBrush(x, y, _brush == Brush.Meteor ? Math.Max(2, _radius) : _radius);
        _entityRenderer.MarkChanged();
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
        _hud.OffsetBottom = 194;

        _hud.ToolSelected += index => { _brush = (Brush)index; RefreshHud(); };
        _hud.RadiusChangeRequested += delta => { _radius = Math.Clamp(_radius + delta, 0, 12); RefreshHud(); };
        _hud.SpeedRequested += speed => { _speed = speed; RefreshHud(); };
        _hud.NewWorldRequested += () => { CreateWorld(Random.Shared.Next()); RefreshHud(); };
        _hud.SetSelectedTool((int)_brush);
        RefreshHud();
    }
}
