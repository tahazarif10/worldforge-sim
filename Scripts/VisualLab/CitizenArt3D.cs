using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Original, low-poly miniature villager art for the approved 3D world.
/// Face, hair, scarf, tunic, satchel, arms, legs and boots are authored as
/// colored faceted geometry. Five GPU MultiMeshes per palette keep thousands
/// of pieces out of the SceneTree; no independent citizen MeshInstance nodes.
/// This class only draws WorldState citizens; it never changes their behavior.
/// </summary>
public sealed partial class CitizenArt3D : Node3D
{
    private const int Looks = 3;
    private const int Body = 0, LeftArm = 1, RightArm = 2, LeftLeg = 3, RightLeg = 4;
    private const int Parts = 5;
    private const float SimulationStep = 0.18f;

    private readonly MultiMesh[,] _instances = new MultiMesh[Looks, Parts];
    private readonly List<Pose>[] _groups = [new(), new(), new()];
    private readonly Dictionary<int, Pose> _poses = new();
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private RiggedCitizenPreview? _riggedPreview;
    private int _riggedCitizenId = -1;

    // This is intentionally OFF in ordinary play; the approved instanced
    // characters, old-GPU budgets and all existing scene tests remain intact.
    public bool RiggedPreviewEnabled { get; set; } =
        Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--rigged-citizen-demo");

    public int VisibleCitizenCount => _poses.Count;
    public int InstancedDrawBatches => Looks * Parts;

    private sealed class Pose
    {
        public Vector2 Start;
        public Vector2 Destination;
        public Vector2 Position;
        public float Progress = 1f;
        public float Yaw = Mathf.Pi / 4f;
        public float GoalYaw = Mathf.Pi / 4f;
        public float Stride;
        public float WalkWeight;
        public float IdleClock;
        public float IdlePhase;
        public float IdleRefresh;
        public float GroundHeight;
        public bool GroundDirty = true;
        public bool HasRendered;
        public bool Moving;
    }

    internal readonly record struct Palette(
        Color Skin, Color Hair, Color Shirt, Color Tunic, Color Scarf,
        Color Pants, Color Leather, Color Boots, Color Trim);

    internal static readonly Palette[] Palettes =
    [
        new(new("#edbc91"), new("#6a3e29"), new("#ead7b4"), new("#846344"),
            new("#ba7048"), new("#55473f"), new("#70442d"), new("#453427"), new("#d6b16b")),
        new(new("#d99f7b"), new("#3b302c"), new("#e4d2ad"), new("#557763"),
            new("#bd945b"), new("#48544d"), new("#725037"), new("#44372d"), new("#c8ad70")),
        new(new("#f2c9a4"), new("#926343"), new("#d8c1a1"), new("#6d7063"),
            new("#688e89"), new("#4b4e59"), new("#785443"), new("#4a342c"), new("#e3c883"))
    ];

    public override void _Ready()
    {
        Name = "Batched3DCitizens";
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.93f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        for (int look = 0; look < Looks; look++)
        {
            Palette p = Palettes[look];
            Mesh body = BuildBody(p, material);
            Mesh arm = BuildArm(p, material);
            Mesh leg = BuildLeg(p, material);
            Mesh[] parts = [body, arm, arm, leg, leg];
            for (int part = 0; part < Parts; part++)
            {
                var batch = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh = parts[part],
                    InstanceCount = 0
                };
                _instances[look, part] = batch;
                AddChild(new MultiMeshInstance3D
                {
                    Name = $"Citizen_{look}_{part}",
                    Multimesh = batch,
                    CastShadow = part == Body
                        ? GeometryInstance3D.ShadowCastingSetting.On
                        : GeometryInstance3D.ShadowCastingSetting.Off
                });
            }
        }
        if (RiggedPreviewEnabled)
        {
            _riggedPreview = new RiggedCitizenPreview
            {
                Name = "RiggedCitizenPreview", Visible = false
            };
            AddChild(_riggedPreview);
        }
    }

    public void SetEcoMode(bool eco)
    {
        for (int look = 0; look < Looks; look++)
        {
            var body = GetNode<MultiMeshInstance3D>($"Citizen_{look}_{Body}");
            body.CastShadow = eco
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : GeometryInstance3D.ShadowCastingSetting.On;
        }
        _riggedPreview?.SetEcoMode(eco);
    }

    public void Bind(WorldState world, VisualTerrain3D terrain)
    {
        _world = world;
        _terrain = terrain;
        _riggedCitizenId = -1;
        if (_riggedPreview is not null) _riggedPreview.Visible = false;
        _poses.Clear();
        Sync();
    }

    /// <summary>Capture authoritative citizen positions after simulation ticks, spawns and removals.</summary>
    public void Sync()
    {
        if (_world is null || _terrain is null) return;

        foreach (List<Pose> group in _groups) group.Clear();
        if (_riggedPreview is not null && _riggedCitizenId < 0 &&
            _world.People.Count > 0)
            _riggedCitizenId = _world.People[0].Id;
        var alive = new HashSet<int>();
        foreach (Citizen citizen in _world.People)
        {
            alive.Add(citizen.Id);
            Vector2 destination = new(citizen.X + .5f, citizen.Y + .5f);
            if (!_poses.TryGetValue(citizen.Id, out Pose? state))
            {
                state = new Pose
                {
                    Start = destination,
                    Destination = destination,
                    Position = destination,
                    // The citizens never breathe or glance in lockstep.
                    IdlePhase = citizen.Id * 2.399963f % Mathf.Tau
                };
                _poses.Add(citizen.Id, state);
            }
            else if (state.Destination.DistanceSquaredTo(destination) > 0.0001f)
            {
                Vector2 direction = destination - state.Position;
                state.Start = state.Position;
                state.Destination = destination;
                state.Progress = 0f;
                state.Moving = true;
                state.GoalYaw = Mathf.Atan2(direction.X, direction.Y);
            }
            // A terrain brush can change the surface under a standing
            // citizen; authoritative updates invalidate the cached height.
            state.GroundDirty = true;
            state.HasRendered = false;
            // Only the first citizen uses the real skeletal preview.
            // All other citizens retain the existing 15 MultiMesh batches.
            if (citizen.Id != _riggedCitizenId)
                _groups[(citizen.Id - 1) % Looks].Add(state);
        }

        foreach (int id in new List<int>(_poses.Keys))
            if (!alive.Contains(id)) _poses.Remove(id);
        if (_riggedPreview is not null && !alive.Contains(_riggedCitizenId))
        {
            _riggedCitizenId = -1;
            _riggedPreview.Visible = false;
        }

        for (int look = 0; look < Looks; look++)
        for (int part = 0; part < Parts; part++)
            if (_instances[look, part].InstanceCount != _groups[look].Count)
                _instances[look, part].InstanceCount = _groups[look].Count;

        Render(0, 1);
    }

    /// <summary>
    /// Cosmetic only: smooth positional interpolation, idle breathing/looking,
    /// gentle body sway and alternating steps/arm swings. Authoritative citizen
    /// movement and terrain state are never changed.
    /// </summary>
    public void Render(double delta, int simulationSpeed)
    {
        if (_terrain is null) return;
        float dt = Mathf.Clamp((float)delta, 0f, 0.1f);
        float rate = Math.Max(1, simulationSpeed);
        for (int look = 0; look < Looks; look++)
        {
            List<Pose> people = _groups[look];
            for (int i = 0; i < people.Count; i++)
            {
                Pose state = people[i];
                bool movedThisFrame = AdvancePose(state, dt, rate, simulationSpeed);

                // Run idle poses at ~15 FPS instead of uploading all five
                // MultiMesh transforms for every standing person each frame.
                // Walking stays at full frame cadence.
                bool groundChanged = state.GroundDirty;
                float px = state.Position.X, pz = state.Position.Y;
                if (groundChanged || movedThisFrame)
                {
                    int cellX = (int)MathF.Floor(px), cellZ = (int)MathF.Floor(pz);
                    state.GroundHeight = _terrain.SurfaceHeightAt(cellX, cellZ,
                        px - cellX, pz - cellZ);
                    state.GroundDirty = false;
                }
                state.IdleRefresh += dt;
                if (state.HasRendered && !movedThisFrame && !groundChanged &&
                    state.WalkWeight < .015f && state.IdleRefresh < 1f / 15f)
                    continue;
                state.IdleRefresh = 0f;

                float walk = state.WalkWeight;
                float idle = 1f - walk;
                float step = MathF.Sin(state.Stride);
                float stride = step * walk;
                float breathing = MathF.Sin(state.IdleClock * 2.1f + state.IdlePhase);
                float gaze = MathF.Sin(state.IdleClock * .53f + state.IdlePhase * 1.4f);
                float sway = MathF.Sin(state.IdleClock * 1.1f + state.IdlePhase);
                float bob = .028f * MathF.Abs(step) * walk +
                    .009f * breathing * idle;

                // The whole character glances around while idle, and the
                // torso subtly leans into travel without changing the mesh.
                var baseTransform = new Transform3D(
                    new Basis(Vector3.Up, state.Yaw + .095f * gaze * idle),
                    new Vector3(px, state.GroundHeight + bob, pz));
                float bodyRoll = .018f * stride + .012f * sway * idle;
                var bodyTransform = baseTransform * new Transform3D(
                    new Basis(Vector3.Right, .035f * walk) *
                        new Basis(Vector3.Forward, bodyRoll),
                    new Vector3(0, .006f * breathing * idle, 0));

                // A mild counter-swing of the hands plus alternating lifted
                // boots makes stepping readable at isometric game zoom.
                float idleArm = .029f *
                    MathF.Sin(state.IdleClock * 1.55f + state.IdlePhase) * idle;
                float leftLift = .077f * MathF.Max(0f, step) * walk;
                float rightLift = .077f * MathF.Max(0f, -step) * walk;

                _instances[look, Body].SetInstanceTransform(i, bodyTransform);
                _instances[look, LeftArm].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(-.305f, 1.035f, 0f),
                        -.49f * stride + idleArm));
                _instances[look, RightArm].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(.305f, 1.035f, 0f),
                        .49f * stride - idleArm));
                _instances[look, LeftLeg].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(-.132f, .58f + leftLift, 0f),
                        .36f * stride));
                _instances[look, RightLeg].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(.132f, .58f + rightLift, 0f),
                        -.36f * stride));
                state.HasRendered = true;
            }
        }

        if (_riggedPreview is not null && _riggedCitizenId > 0 &&
            _poses.TryGetValue(_riggedCitizenId, out Pose? selected))
        {
            bool moved = AdvancePose(selected, dt, rate, simulationSpeed);
            float px = selected.Position.X, pz = selected.Position.Y;
            if (selected.GroundDirty || moved)
            {
                int x = (int)MathF.Floor(px), z = (int)MathF.Floor(pz);
                selected.GroundHeight = _terrain.SurfaceHeightAt(x, z,
                    px - x, pz - z);
                selected.GroundDirty = false;
            }
            _riggedPreview.Visible = true;
            _riggedPreview.Present(selected.Position,selected.Yaw,
                selected.GroundHeight,selected.WalkWeight,dt,simulationSpeed);
        }
    }

    private static bool AdvancePose(Pose state,float dt,float rate,int simulationSpeed)
    {
        // Cosmetic interpolation is the same for the approved batches and
        // the one optional skeleton. Neither renderer changes WorldState.
        bool moved = state.Moving && simulationSpeed > 0;
        if (simulationSpeed > 0) state.IdleClock += dt;
        if (moved)
        {
            state.Progress = MathF.Min(1f,state.Progress + dt * rate / SimulationStep);
            state.Position = state.Start.Lerp(state.Destination,
                state.Progress * state.Progress * (3f - 2f * state.Progress));
            if (state.Progress >= 1f) state.Moving = false;
            state.Stride += dt * 14f * MathF.Sqrt(rate);
        }
        state.Yaw = Mathf.LerpAngle(state.Yaw,state.GoalYaw,MathF.Min(1f,dt*12f));
        state.WalkWeight = Mathf.Lerp(state.WalkWeight,moved ? 1f : 0f,
            MathF.Min(1f,dt*10f));
        return moved;
    }

    private static Transform3D Limb(Vector3 anchor, float angle) =>
        new(new Basis(Vector3.Right, angle), anchor);

    // Keep exactly the previous complete body mesh for every ordinary
    // MultiMesh citizen. The one optional rigged preview attaches the same
    // authored torso/head geometry to separate spine and neck bones.
    private static Mesh BuildBody(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        AddTorso(b, p);
        AddHead(b, p);
        return b.Finish();
    }

    internal static Mesh BuildRigTorso(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        AddTorso(b, p);
        return b.Finish();
    }

    internal static Mesh BuildRigHead(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        AddHead(b, p);
        return b.Finish();
    }

    private static void AddTorso(Sculpt b, Palette p)
    {
        // Tunic: wide shoulders, tapered waist, contrasting leather vest.
        b.Frustum(new Vector3(0, .59f, 0), new Vector2(.235f, .17f),
            new Vector3(0, 1.08f, 0), new Vector2(.265f, .19f), 10, p.Shirt);
        b.Frustum(new Vector3(0, .57f, 0), new Vector2(.255f, .19f),
            new Vector3(0, .735f, 0), new Vector2(.245f, .182f), 10, p.Tunic);
        b.Box(new Vector3(-.205f, .86f, .205f), new Vector3(.082f, .48f, .052f), p.Tunic);
        b.Box(new Vector3(.205f, .86f, .205f), new Vector3(.082f, .48f, .052f), p.Tunic);
        b.Box(new Vector3(0, .69f, .185f), new Vector3(.53f, .09f, .075f), p.Leather);
        b.Box(new Vector3(0, .693f, .225f), new Vector3(.092f, .112f, .029f), p.Trim);
        b.Box(new Vector3(0, .693f, .248f), new Vector3(.048f, .064f, .014f), p.Leather);
        b.Beam(new Vector3(-.205f, 1.05f, .226f),
            new Vector3(.185f, .74f, .226f), .034f, .034f, p.Leather, 6);
        b.Box(new Vector3(.293f, .61f, .02f), new Vector3(.16f, .22f, .17f), p.Leather);
        b.Box(new Vector3(.293f, .67f, .11f), new Vector3(.075f, .045f, .025f), p.Trim);

    }

    private static void AddHead(Sculpt b, Palette p)
    {
        // Skin and head: a deliberately legible large face at strategy zoom.
        b.Frustum(new Vector3(0, 1.04f, 0), new Vector2(.078f, .075f),
            new Vector3(0, 1.20f, 0), new Vector2(.075f, .072f), 8, p.Skin);
        b.Ellipsoid(new Vector3(0, 1.365f, 0), new Vector3(.25f, .277f, .222f),
            p.Skin, 10, 8);
        b.Ellipsoid(new Vector3(-.252f, 1.36f, .015f), new Vector3(.051f, .075f, .055f),
            p.Skin.Darkened(.07f), 8, 5);
        b.Ellipsoid(new Vector3(.252f, 1.36f, .015f), new Vector3(.051f, .075f, .055f),
            p.Skin.Darkened(.07f), 8, 5);
        b.Ellipsoid(new Vector3(0, 1.335f, .228f), new Vector3(.037f, .055f, .028f),
            p.Skin.Darkened(.075f), 8, 5);
        for (int side = -1; side <= 1; side += 2)
        {
            b.Ellipsoid(new Vector3(side * .104f, 1.387f, .21f),
                new Vector3(.035f, .045f, .027f), new Color("#29292a"), 8, 5);
            b.Ellipsoid(new Vector3(side * .092f, 1.4f, .237f),
                new Vector3(.012f, .012f, .009f), new Color("#f5e8c8"), 6, 4);
            b.Beam(new Vector3(side * .065f, 1.465f, .216f),
                new Vector3(side * .142f, 1.457f, .201f), .012f, .013f, p.Hair, 5);
        }
        b.Box(new Vector3(0, 1.249f, .205f), new Vector3(.064f, .012f, .015f),
            new Color("#9c695e"));

        // Low-poly swept hair, dark side locks and three asymmetric bangs.
        b.Ellipsoid(new Vector3(0, 1.39f, -.016f),
            new Vector3(.274f, .292f, .243f), p.Hair, 12, 6, 0f, 1.12f);
        b.Ellipsoid(new Vector3(-.209f, 1.49f, -.025f),
            new Vector3(.079f, .142f, .172f), p.Hair.Darkened(.065f), 7, 5);
        b.Ellipsoid(new Vector3(.207f, 1.49f, -.025f),
            new Vector3(.077f, .135f, .169f), p.Hair, 7, 5);
        b.Beam(new Vector3(-.135f, 1.61f, .087f),
            new Vector3(-.145f, 1.475f, .216f), .085f, .019f, p.Hair, 5);
        b.Beam(new Vector3(.006f, 1.635f, .08f),
            new Vector3(.059f, 1.49f, .215f), .079f, .023f, p.Hair.Lightened(.07f), 5);
        b.Beam(new Vector3(.15f, 1.598f, .053f),
            new Vector3(.187f, 1.505f, .174f), .064f, .016f, p.Hair, 5);
        b.Beam(new Vector3(-.16f, 1.59f, -.115f),
            new Vector3(-.235f, 1.68f, -.12f), .08f, 0f, p.Hair.Darkened(.03f), 5);

        // Recognizable cloth scarf with triangular hanging tail.
        b.Frustum(new Vector3(0, 1.07f, 0), new Vector2(.21f, .197f),
            new Vector3(0, 1.17f, 0), new Vector2(.235f, .208f), 10, p.Scarf);
        b.Box(new Vector3(.15f, 1.022f, .216f),
            new Vector3(.128f, .22f, .056f), p.Scarf.Darkened(.08f));
        b.Beam(new Vector3(.16f, .94f, .221f),
            new Vector3(.20f, .87f, .208f), .066f, .012f, p.Scarf, 4);
    }

    private static Mesh BuildArm(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        // Local shoulder pivot at (0,0,0), down toward a distinct hand.
        b.Frustum(new Vector3(0, -.31f, .004f), new Vector2(.095f, .097f),
            new Vector3(0, -.015f, 0), new Vector2(.12f, .117f), 8, p.Shirt);
        b.Frustum(new Vector3(0, -.38f, .006f), new Vector2(.082f, .082f),
            new Vector3(0, -.28f, .005f), new Vector2(.105f, .105f), 8, p.Tunic);
        b.Box(new Vector3(0, -.36f, .084f),
            new Vector3(.175f, .045f, .034f), p.Leather);
        b.Ellipsoid(new Vector3(0, -.465f, .017f),
            new Vector3(.084f, .112f, .083f), p.Skin, 8, 6);
        return b.Finish();
    }

    private static Mesh BuildLeg(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        // Local hip pivot at (0,0,0), boot toe points toward local +Z.
        b.Frustum(new Vector3(0, -.425f, .01f), new Vector2(.098f, .102f),
            new Vector3(0, -.022f, 0), new Vector2(.117f, .119f), 8, p.Pants);
        b.Frustum(new Vector3(0, -.5f, .009f), new Vector2(.113f, .119f),
            new Vector3(0, -.36f, .005f), new Vector2(.11f, .107f), 8, p.Boots);
        b.Box(new Vector3(0, -.465f, .075f),
            new Vector3(.22f, .11f, .285f), p.Boots);
        b.Box(new Vector3(0, -.49f, .155f),
            new Vector3(.219f, .058f, .10f), p.Boots.Darkened(.16f));
        b.Box(new Vector3(0, -.375f, .107f),
            new Vector3(.235f, .052f, .034f), p.Leather);
        return b.Finish();
    }

    // Only the opt-in first villager uses these segmented pieces. They reuse
    // the exact same palette, cloth/leather, faceting and material as the
    // approved instanced citizen; only the elbow/knee pivots are new.
    internal static Mesh BuildRigUpperArm(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        b.Frustum(new Vector3(0, -.245f, 0), new Vector2(.09f,.094f),
            new Vector3(0, -.012f, 0), new Vector2(.12f,.116f), 8, p.Shirt);
        b.Frustum(new Vector3(0, -.267f, .003f), new Vector2(.097f,.099f),
            new Vector3(0, -.218f, 0), new Vector2(.106f,.102f), 8, p.Tunic);
        return b.Finish();
    }

    internal static Mesh BuildRigForearm(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        b.Frustum(new Vector3(0, -.155f, .003f), new Vector2(.081f,.08f),
            new Vector3(0, 0, 0), new Vector2(.1f,.095f), 8, p.Shirt);
        b.Frustum(new Vector3(0, -.128f, .005f), new Vector2(.088f,.085f),
            new Vector3(0, -.087f, 0), new Vector2(.104f,.099f), 8, p.Tunic);
        b.Box(new Vector3(0,-.123f,.088f),new Vector3(.172f,.032f,.035f),p.Leather);
        b.Ellipsoid(new Vector3(0,-.22f,.017f),
            new Vector3(.084f,.108f,.083f),p.Skin,8,6);
        return b.Finish();
    }

    internal static Mesh BuildRigThigh(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        b.Frustum(new Vector3(0,-.29f,0),new Vector2(.098f,.10f),
            new Vector3(0,-.018f,0),new Vector2(.117f,.119f),8,p.Pants);
        return b.Finish();
    }

    internal static Mesh BuildRigShin(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        b.Frustum(new Vector3(0,-.22f,.008f),new Vector2(.096f,.099f),
            new Vector3(0,-.008f,0),new Vector2(.101f,.103f),8,p.Pants);
        b.Frustum(new Vector3(0,-.23f,.008f),new Vector2(.109f,.113f),
            new Vector3(0,-.155f,0),new Vector2(.107f,.106f),8,p.Boots);
        b.Box(new Vector3(0,-.185f,.099f),
            new Vector3(.23f,.045f,.03f),p.Leather);
        return b.Finish();
    }

    internal static Mesh BuildRigFoot(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        b.Box(new Vector3(0,-.058f,.075f),
            new Vector3(.22f,.116f,.285f),p.Boots);
        b.Box(new Vector3(0,-.108f,.142f),
            new Vector3(.22f,.035f,.105f),p.Boots.Darkened(.16f));
        return b.Finish();
    }

    /// <summary>Shared lightweight faceted surface authoring; each face owns a normal.</summary>
    private sealed class Sculpt(Material material) : IDisposable
    {
        private readonly SurfaceTool _surface = Start(material);
        private static SurfaceTool Start(Material material)
        {
            var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            tool.SetMaterial(material);
            return tool;
        }

        private void Face(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward)
        {
            Vector3 normal = (b - a).Cross(c - a);
            if (normal.LengthSquared() < 1e-12f) return;
            if (normal.Dot(outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            normal = normal.Normalized();
            _surface.SetNormal(normal);
            _surface.SetColor(color); _surface.AddVertex(a);
            _surface.SetColor(color); _surface.AddVertex(b);
            _surface.SetColor(color); _surface.AddVertex(c);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Color color, Vector3 center)
        {
            Vector3 outward = ((a + b + c + d) * .25f) - center;
            Face(a, b, c, color, outward);
            Face(a, c, d, color, outward);
        }

        public void Box(Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * .5f;
            Vector3[] v =
            [
                center + new Vector3(-h.X, -h.Y, -h.Z),
                center + new Vector3(h.X, -h.Y, -h.Z),
                center + new Vector3(h.X, h.Y, -h.Z),
                center + new Vector3(-h.X, h.Y, -h.Z),
                center + new Vector3(-h.X, -h.Y, h.Z),
                center + new Vector3(h.X, -h.Y, h.Z),
                center + new Vector3(h.X, h.Y, h.Z),
                center + new Vector3(-h.X, h.Y, h.Z)
            ];
            Quad(v[0], v[1], v[2], v[3], color.Darkened(.12f), center);
            Quad(v[5], v[4], v[7], v[6], color, center);
            Quad(v[4], v[0], v[3], v[7], color.Darkened(.04f), center);
            Quad(v[1], v[5], v[6], v[2], color.Darkened(.04f), center);
            Quad(v[3], v[2], v[6], v[7], color.Lightened(.08f), center);
            Quad(v[4], v[5], v[1], v[0], color.Darkened(.16f), center);
        }

        public void Ellipsoid(Vector3 center, Vector3 radii, Color color,
            int segments, int rings, float start = 0f, float end = Mathf.Pi)
        {
            for (int row = 0; row < rings; row++)
            {
                float theta0 = Mathf.Lerp(start, end, row / (float)rings);
                float theta1 = Mathf.Lerp(start, end, (row + 1f) / rings);
                for (int side = 0; side < segments; side++)
                {
                    float phi0 = Mathf.Tau * side / segments;
                    float phi1 = Mathf.Tau * (side + 1f) / segments;
                    Vector3 a = Point(theta0, phi0);
                    Vector3 b = Point(theta0, phi1);
                    Vector3 c = Point(theta1, phi1);
                    Vector3 d = Point(theta1, phi0);
                    Color shade = ((side + row) & 3) == 0
                        ? color.Darkened(.045f) : color;
                    Quad(a, b, c, d, shade, center);
                }
            }

            Vector3 Point(float theta, float phi) =>
                center + new Vector3(
                    radii.X * Mathf.Sin(theta) * Mathf.Cos(phi),
                    radii.Y * Mathf.Cos(theta),
                    radii.Z * Mathf.Sin(theta) * Mathf.Sin(phi));
        }

        public void Frustum(Vector3 bottom, Vector2 bottomRadii,
            Vector3 top, Vector2 topRadii, int segments, Color color)
        {
            Vector3 middle = (bottom + top) * .5f;
            for (int side = 0; side < segments; side++)
            {
                float a0 = Mathf.Tau * side / segments;
                float a1 = Mathf.Tau * (side + 1f) / segments;
                Vector3 a = Ring(bottom, bottomRadii, a0);
                Vector3 b = Ring(bottom, bottomRadii, a1);
                Vector3 c = Ring(top, topRadii, a1);
                Vector3 d = Ring(top, topRadii, a0);
                Quad(a, b, c, d, (side % 4 == 0)
                    ? color.Lightened(.035f) : color, middle);
                Face(bottom, b, a, color.Darkened(.10f), Vector3.Down);
                Face(top, d, c, color.Lightened(.05f), Vector3.Up);
            }

            static Vector3 Ring(Vector3 c, Vector2 r, float angle) =>
                c + new Vector3(Mathf.Cos(angle) * r.X, 0,
                    Mathf.Sin(angle) * r.Y);
        }

        public void Beam(Vector3 from, Vector3 to, float fromRadius, float toRadius,
            Color color, int segments)
        {
            Vector3 axis = (to - from).Normalized();
            Vector3 u = axis.Cross(MathF.Abs(axis.Dot(Vector3.Up)) < .92f
                ? Vector3.Up : Vector3.Forward).Normalized();
            Vector3 v = axis.Cross(u).Normalized();
            Vector3 middle = (from + to) * .5f;
            for (int side = 0; side < segments; side++)
            {
                float a = Mathf.Tau * side / segments;
                float b = Mathf.Tau * (side + 1f) / segments;
                Vector3 ra = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vector3 rb = u * Mathf.Cos(b) + v * Mathf.Sin(b);
                Vector3 p = from + ra * fromRadius;
                Vector3 q = from + rb * fromRadius;
                Vector3 r = to + rb * toRadius;
                Vector3 s = to + ra * toRadius;
                Quad(p, q, r, s, color, middle);
                Face(from, q, p, color.Darkened(.05f), -axis);
                Face(to, s, r, color.Lightened(.04f), axis);
            }
        }

        public Mesh Finish()
        {
            Mesh? mesh = _surface.Commit();
            return mesh ?? throw new InvalidOperationException("Citizen art mesh did not build.");
        }

        public void Dispose() => _surface.Dispose();
    }
}
