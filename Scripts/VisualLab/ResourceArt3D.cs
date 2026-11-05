using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Small, visually distinct berry bushes only. Original Kenney foliage,
/// mountains, coast and water materials are never modified. Two batched
/// primitive meshes cover all visible bushes and berries; resource ownership,
/// regrowth and harvesting remain exclusively inside WorldSim.
/// </summary>
public sealed partial class ResourceArt3D : Node3D
{
    private readonly List<(ResourceSite Site, Vector3 Ground)> _visible = new();
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private Godot.Camera3D? _camera;
    private MultiMesh _leaves = null!;
    private MultiMesh _berries = null!;
    private double _refreshElapsed;
    private const float MarginPixels = 96f;

    public int VisibleBushCount => _visible.Count;
    public int VisibleBerryCount { get; private set; }

    public override void _Ready()
    {
        Name = "BerryResources";
        var plantMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            AlbedoColor = Colors.White,
            Roughness = 0.94f,
            Metallic = 0f
        };
        // Faceted opaque volume rather than tiny transparent crossing cards:
        // avoids the rejected cyan/unshaded foliage regression entirely.
        var bushMesh = new SphereMesh
        {
            Radius = 0.5f, Height = 1f, RadialSegments = 7, Rings = 3
        };
        var fruitMesh = new SphereMesh
        {
            Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3
        };
        _leaves = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, Mesh = bushMesh, InstanceCount = 0
        };
        _berries = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, Mesh = fruitMesh, InstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D
        {
            Name = "BerryBushLeaves", Multimesh = _leaves,
            MaterialOverride = plantMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        AddChild(new MultiMeshInstance3D
        {
            Name = "BerryFruit", Multimesh = _berries,
            MaterialOverride = plantMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    public void BindWorld(WorldState world, VisualTerrain3D terrain, Godot.Camera3D camera)
    {
        _world = world;
        _terrain = terrain;
        _camera = camera;
        _refreshElapsed = 0;
        RefreshNow();
    }

    /// <summary>Throttled projection: no work on offscreen foliage per frame.</summary>
    public void Tick(double delta)
    {
        _refreshElapsed += Math.Clamp(delta, 0, 0.1);
        if (_refreshElapsed < 0.15) return;
        _refreshElapsed = 0;
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (_world is null || _terrain is null || _camera is null) return;
        Vector2 size = _camera.GetViewport().GetVisibleRect().Size;
        if (size.X < 1f || size.Y < 1f) return;
        _visible.Clear();

        foreach (ResourceSite site in _world.Resources.FoodSites())
        {
            // A small elevated center keeps sparse berry bushes in the view
            // even when the approved isometric camera looks obliquely at them.
            Vector3 approx = new(site.X + .5f, .9f, site.Y + .5f);
            if (_camera.IsPositionBehind(approx)) continue;
            Vector2 projected = _camera.UnprojectPosition(approx);
            if (projected.X < -MarginPixels || projected.Y < -MarginPixels ||
                projected.X > size.X + MarginPixels ||
                projected.Y > size.Y + MarginPixels) continue;

            float ground = _terrain.SurfaceHeightAt(site.X, site.Y, .5f, .5f);
            _visible.Add((site, new Vector3(site.X + .5f, ground, site.Y + .5f)));
        }

        int leafCount = checked(_visible.Count * 3);
        int fruitCount = 0;
        foreach (var item in _visible)
            fruitCount += Math.Min(item.Site.Remaining, 4);

        if (_leaves.InstanceCount != leafCount) _leaves.InstanceCount = leafCount;
        if (_berries.InstanceCount != fruitCount) _berries.InstanceCount = fruitCount;
        VisibleBerryCount = fruitCount;

        Color[] leaves = [new("#3d8051"), new("#549b61"), new("#6ba96a")];
        Color[] fruit = [new("#b93c48"), new("#d94e52"), new("#a52b42"), new("#e47d5b")];
        Vector3[] leafOffsets =
        [
            new(-.13f, .23f, .015f),
            new(.13f, .27f, -.04f),
            new(.015f, .35f, .12f)
        ];
        Vector3[] fruitOffsets =
        [
            new(-.14f, .33f, .18f),
            new(.14f, .40f, .095f),
            new(.08f, .41f, -.17f),
            new(-.07f, .49f, -.045f)
        ];

        int leafIndex = 0, berryIndex = 0;
        foreach (var item in _visible)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 scale = new(.29f, .26f + i * .02f, .29f);
                _leaves.SetInstanceTransform(leafIndex,
                    new Transform3D(Basis.Identity.Scaled(scale),
                        item.Ground + leafOffsets[i]));
                _leaves.SetInstanceColor(leafIndex, leaves[i]);
                leafIndex++;
            }
            for (int i = 0; i < Math.Min(item.Site.Remaining, 4); i++)
            {
                _berries.SetInstanceTransform(berryIndex,
                    new Transform3D(Basis.Identity.Scaled(Vector3.One * .11f),
                        item.Ground + fruitOffsets[i]));
                _berries.SetInstanceColor(berryIndex, fruit[i]);
                berryIndex++;
            }
        }
    }
}
