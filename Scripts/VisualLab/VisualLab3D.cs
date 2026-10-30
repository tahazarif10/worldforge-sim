using System;
using Godot;
using WorldSim;
using WorldForge.UI;

namespace WorldForge.VisualLab;

/// <summary>
/// V2 visual art-direction preview. Uses the same deterministic WorldSim model,
/// but renders a true 3D isometric world with real height, light and meshes.
/// The existing 2D game remains unchanged and is still the default scene.
/// </summary>
public partial class VisualLab3D : Node3D
{
    private const double TickSeconds = 0.18;

    private enum Brush { Land, Ocean, Sand, Forest, Mountain, Person, Meteor }

    private WorldState _world = null!;
    private VisualTerrain3D _terrain = null!;
    private MultiMeshInstance3D _citizens = null!;
    private Godot.Camera3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private GameHud _hud = null!;
    private Brush _brush = Brush.Person;
    private int _radius = 2;
    private int _speed = 1;
    private WorldForge.Camera3D.StrategyCameraRig _cameraRig = null!;
    private double _tickAccumulator;
    private double _terrainRefresh;
    private double _hudRefresh;
    private double _fpsWindow;
    private double _slowestFrame;
    private int _frames;

    public override void _Ready()
    {
        Name = "WorldForge3D — Visual Lab";
        SetupLighting();

        _terrain = new VisualTerrain3D { Name = "WorldAndFoliage" };
        AddChild(_terrain);
        _citizens = new MultiMeshInstance3D
        {
            Name = "Batched3DCitizens",
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                AlbedoColor = Colors.White,
                Roughness = 0.86f
            }
        };
        AddChild(_citizens);

        _camera = new Godot.Camera3D
        {
            Name = "IsometricCamera",
            Projection = Godot.Camera3D.ProjectionType.Orthogonal,
            Size = 79f,
            Near = 0.05f,
            Far = 550f,
            Current = true
        };
        AddChild(_camera);

        CreateWorld(20261030);
        _cameraRig = new WorldForge.Camera3D.StrategyCameraRig(_camera,
            (x, y) => _terrain.HeightAt(x, y), _world.Width, _world.Height);
        SetupHud();
    }

    private void SetupLighting()
    {
        var atmosphere = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#b8d3dc"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#d9ecdc"),
                AmbientLightEnergy = 0.74f
            }
        };
        AddChild(atmosphere);

        _sun = new DirectionalLight3D
        {
            Name = "AfternoonSun",
            RotationDegrees = new Vector3(-59f, -30f, 13f),
            LightColor = new Color("#ffedcf"),
            LightEnergy = 1.15f,
            ShadowEnabled = true
        };
        AddChild(_sun);
    }

    private void CreateWorld(int seed)
    {
        // Reduced preview resolution leaves GPU headroom on the R9 M395X.
        // The simulation is the same WorldSim class as the production 2D game.
        _world = new WorldState(120, 88, seed);
        _world.PopulationLimit = 450;
        _terrain.BindWorld(_world);
        _cameraRig?.ResetWorld(_world.Width, _world.Height);
        _tickAccumulator = 0;
        _terrainRefresh = 0;
        UpdateActors();
    }

    private void UpdateActors()
    {
        var sphere = new SphereMesh { Radius = 0.22f, Height = 0.58f, RadialSegments = 7, Rings = 3 };
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = sphere,
            InstanceCount = _world.People.Count
        };
        int i = 0;
        foreach (Citizen citizen in _world.People)
        {
            float terrainHeight = _terrain.HeightAt(citizen.X, citizen.Y);
            batch.SetInstanceTransform(i, new Transform3D(
                Basis.Identity,
                new Vector3(citizen.X + 0.5f, terrainHeight + 0.38f, citizen.Y + 0.5f)));
            batch.SetInstanceColor(i, (citizen.Id & 1) == 0
                ? new Color("#f3c78e") : new Color("#f4e0ba"));
            i++;
        }
        _citizens.Multimesh = batch;
    }

    public override void _Process(double delta)
    {
        Vector2 keys = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) keys.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) keys.X += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) keys.Y += 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) keys.Y -= 1f;
        if (keys.LengthSquared() > 0.01f)
        {
            _cameraRig.PanByKeyboard(keys, delta);
        }

        if (_speed > 0)
        {
            _tickAccumulator += delta * _speed;
            int steps = 0;
            while (_tickAccumulator >= TickSeconds && steps++ < 12)
            {
                _world.Advance();
                _tickAccumulator -= TickSeconds;
                UpdateActors();
            }
            if (_tickAccumulator > TickSeconds * 12) _tickAccumulator = TickSeconds;
        }

        _terrainRefresh += delta;
        if (_terrainRefresh >= 0.16)
        {
            _terrainRefresh = 0;
            _terrain.FlushChanges();
        }

        _hudRefresh += delta;
        if (_hudRefresh >= 0.25)
        {
            _hudRefresh = 0;
            RefreshHud();
        }

        _fpsWindow += delta;
        _slowestFrame = Math.Max(_slowestFrame, delta * 1000);
        _frames++;
        if (_fpsWindow >= 1.0)
        {
            _hud.UpdatePerformance((int)Math.Round(_frames / _fpsWindow), _slowestFrame);
            _fpsWindow = 0;
            _slowestFrame = 0;
            _frames = 0;
        }
    }

    private void RefreshHud() =>
        _hud?.UpdateState(_brush.ToString(), _radius, _speed, _world.Seed,
                         _world.People.Count, _world.Tick);

    private void SetupHud()
    {
        var layer = new CanvasLayer { Name = "HudOverlay" };
        AddChild(layer);
        _hud = new GameHud();
        layer.AddChild(_hud);
        _hud.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _hud.OffsetLeft = 12;
        _hud.OffsetRight = -12;
        _hud.OffsetTop = 12;
        _hud.OffsetBottom = 194;

        _hud.ToolSelected += tool => { _brush = (Brush)tool; RefreshHud(); };
        _hud.RadiusChangeRequested += amount =>
        {
            _radius = Math.Clamp(_radius + amount, 0, 12);
            RefreshHud();
        };
        _hud.SpeedRequested += speed => { _speed = speed; RefreshHud(); };
        _hud.NewWorldRequested += () => { CreateWorld(Random.Shared.Next()); RefreshHud(); };
        _hud.SetSelectedTool((int)_brush);
        RefreshHud();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                _cameraRig.ZoomAt(mb.Position, 1f / 1.14f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                _cameraRig.ZoomAt(mb.Position, 1.14f);
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed) _cameraRig.BeginDrag(mb.Position);
                else _cameraRig.EndDrag();
            }
            else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                ApplyAtScreen(mb.Position);
        }
        else if (input is InputEventMouseMotion mm)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Right))
                _cameraRig.DragTo(mm.Position);
            else if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                ApplyAtScreen(mm.Position);
            }
        }
        else if (input is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Space) _speed = _speed == 0 ? 1 : 0;
            if (k.Keycode == Key.Key1) _speed = 1;
            if (k.Keycode == Key.Key2) _speed = 2;
            if (k.Keycode == Key.Key3) _speed = 4;
            if (k.Keycode == Key.F2) _sun.ShadowEnabled = !_sun.ShadowEnabled;
            if (k.Keycode == Key.Escape) GetTree().Quit();
            RefreshHud();
        }
    }

    private void ApplyAtScreen(Vector2 screen)
    {
        if (!_cameraRig.TryPickTerrain(screen, out var hit)) return;
        int x = hit.X, y = hit.Y;

        bool terrainEdited = true;
        switch (_brush)
        {
            case Brush.Land: _world.Paint(x, y, _radius, Terrain.Grass); break;
            case Brush.Ocean: _world.Paint(x, y, _radius, Terrain.DeepWater); break;
            case Brush.Sand: _world.Paint(x, y, _radius, Terrain.Sand); break;
            case Brush.Forest: _world.Paint(x, y, _radius, Terrain.Forest); break;
            case Brush.Mountain: _world.Paint(x, y, _radius, Terrain.Mountain); break;
            case Brush.Person: terrainEdited = false; _world.Spawn(x, y); break;
            case Brush.Meteor: _world.Meteor(x, y, Math.Max(2, _radius)); break;
        }
        if (terrainEdited)
            _terrain.MarkChanged(x, y, _brush == Brush.Meteor ? Math.Max(2, _radius) : _radius);
        UpdateActors();
    }
}
