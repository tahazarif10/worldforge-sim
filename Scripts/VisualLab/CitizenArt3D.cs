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
public sealed class CitizenArt3D : Node3D
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
        public bool Moving;
    }

    private readonly record struct Palette(
        Color Skin, Color Hair, Color Shirt, Color Tunic, Color Scarf,
        Color Pants, Color Leather, Color Boots, Color Trim);

    private static readonly Palette[] Palettes =
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
    }

    public void Bind(WorldState world, VisualTerrain3D terrain)
    {
        _world = world;
        _terrain = terrain;
        _poses.Clear();
        Sync();
    }

    /// <summary>Capture authoritative citizen positions after simulation ticks, spawns and removals.</summary>
    public void Sync()
    {
        if (_world is null || _terrain is null) return;

        foreach (List<Pose> group in _groups) group.Clear();
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
                    Position = destination
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
            _groups[(citizen.Id - 1) % Looks].Add(state);
        }

        foreach (int id in new List<int>(_poses.Keys))
            if (!alive.Contains(id)) _poses.Remove(id);

        for (int look = 0; look < Looks; look++)
        for (int part = 0; part < Parts; part++)
            if (_instances[look, part].InstanceCount != _groups[look].Count)
                _instances[look, part].InstanceCount = _groups[look].Count;

        Render(0, 1);
    }

    /// <summary>
    /// Cosmetic only: smooth positional interpolation, heading, footfall and
    /// opposing arm/leg swings. Simulation speed and entity state are untouched.
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
                if (state.Moving && simulationSpeed > 0)
                {
                    state.Progress = MathF.Min(1f,
                        state.Progress + dt * rate / SimulationStep);
                    state.Position = state.Start.Lerp(state.Destination,
                        state.Progress * state.Progress * (3f - 2f * state.Progress));
                    if (state.Progress >= 1f)
                        state.Moving = false;
                }
                state.Yaw = Mathf.LerpAngle(state.Yaw, state.GoalYaw,
                    MathF.Min(1f, dt * 12f));
                float targetWeight = state.Moving && simulationSpeed > 0 ? 1f : 0f;
                state.WalkWeight = Mathf.Lerp(state.WalkWeight, targetWeight,
                    MathF.Min(1f, dt * 10f));
                if (state.Moving && simulationSpeed > 0)
                    state.Stride += dt * 14f * MathF.Sqrt(rate);

                float stride = Mathf.Sin(state.Stride) * state.WalkWeight;
                float bob = 0.028f * MathF.Abs(Mathf.Sin(state.Stride)) *
                    state.WalkWeight;
                float px = state.Position.X, pz = state.Position.Y;
                int cellX = (int)MathF.Floor(px), cellZ = (int)MathF.Floor(pz);
                float elevation = _terrain.SurfaceHeightAt(cellX, cellZ,
                    px - cellX, pz - cellZ);
                var baseTransform = new Transform3D(
                    new Basis(Vector3.Up, state.Yaw),
                    new Vector3(px, elevation + bob, pz));

                _instances[look, Body].SetInstanceTransform(i, baseTransform);
                _instances[look, LeftArm].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(-.305f, 1.035f, 0f),
                        -.45f * stride));
                _instances[look, RightArm].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(.305f, 1.035f, 0f),
                        .45f * stride));
                _instances[look, LeftLeg].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(-.132f, .58f, 0f),
                        .34f * stride));
                _instances[look, RightLeg].SetInstanceTransform(i,
                    baseTransform * Limb(new Vector3(.132f, .58f, 0f),
                        -.34f * stride));
            }
        }
    }

    private static Transform3D Limb(Vector3 anchor, float angle) =>
        new(new Basis(Vector3.Right, angle), anchor);

    private static Mesh BuildBody(Palette p, Material material)
    {
        using var b = new Sculpt(material);
        // Tunic: wide shoulders, tapered waist, contrasting leather vest.
        b.Frustum(new Vector3(0, .59f, 0), new Vector2(.235f, .17f),
            new Vector3(0, 1.08f, 0), new Vector2(.265f, .19f), 10, p.Shirt);
        b.Frustum(new Vector3(0, .57f, 0), new Vector2(.255f, .19f),
            new Vector3(0, .735f, 0), new Vector2(.245f, .182f), 10, p.Tunic);
        b.Box(new Vector3(-.205f, .86f, .152f), new Vector3(.082f, .48f, .052f), p.Tunic);
        b.Box(new Vector3(.205f, .86f, .152f), new Vector3(.082f, .48f, .052f), p.Tunic);
        b.Box(new Vector3(0, .69f, .185f), new Vector3(.53f, .09f, .075f), p.Leather);
        b.Box(new Vector3(0, .693f, .225f), new Vector3(.092f, .112f, .029f), p.Trim);
        b.Box(new Vector3(0, .693f, .248f), new Vector3(.048f, .064f, .014f), p.Leather);
        b.Beam(new Vector3(-.205f, 1.05f, .183f),
            new Vector3(.185f, .74f, .186f), .034f, .034f, p.Leather, 6);
        b.Box(new Vector3(.293f, .61f, .02f), new Vector3(.16f, .22f, .17f), p.Leather);
        b.Box(new Vector3(.293f, .67f, .11f), new Vector3(.075f, .045f, .025f), p.Trim);

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
        return b.Finish();
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
