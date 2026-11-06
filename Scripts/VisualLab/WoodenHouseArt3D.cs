using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// A first low-poly wooden hut: foundation, beam frame, pitched roof and
/// completed walls appear as stages unlock from authoritative WorldState.
/// Native meshes and materials are built once, never in the fixed-tick
/// simulation or worker threads. No existing trees, citizens, terrain, camera,
/// Kenney art or rendering materials are modified.
/// </summary>
public sealed partial class WoodenHouseArt3D : Node3D
{
    private readonly List<(Node3D Part,HouseStage From,HouseStage Through)> _parts=new();
    private readonly Dictionary<string,StandardMaterial3D> _materials=new();
    private Node3D _house=new();
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private Godot.Camera3D? _camera;
    private int _shownHouseId;
    private HouseStage _shownStage;
    private double _elapsed;
    private const float ViewMarginPixels=120f;

    public int RenderedHouseId => _shownHouseId;
    public HouseStage RenderedStage => _shownStage;
    public int ActivePartCount
    {
        get
        {
            int count=0;
            foreach(var p in _parts)if(p.Part.Visible)count++;
            return count;
        }
    }

    public override void _Ready()
    {
        Name="FirstWoodenHouse";
        _house=new Node3D {Name="HouseModel",Visible=false};
        AddChild(_house);

        const float edge=.84f;
        // Planned: stakes and a faint foundation outline, no complete house.
        foreach(float x in new[]{-edge,edge})
        foreach(float z in new[]{-edge,edge})
            Box("SurveyStake",new(.07f,.36f,.07f),new(x,.19f,z),
                "#765033",HouseStage.Planned,HouseStage.Planned);
        foreach(float x in new[]{-edge,edge})
            Box("SiteOutlineSide",new(.075f,.055f,1.75f),new(x,.03f,0),
                "#bd9863",HouseStage.Planned,HouseStage.Planned);
        foreach(float z in new[]{-edge,edge})
            Box("SiteOutlineFront",new(1.75f,.055f,.075f),new(0,.03f,z),
                "#bd9863",HouseStage.Planned,HouseStage.Planned);

        // Foundation: timber platform and perimeter base beams.
        Box("FoundationPlatform",new(1.78f,.15f,1.78f),new(0,.12f,0),
            "#a47b51",HouseStage.Foundation);
        foreach(float x in new[]{-edge,edge})
            Box("FoundationRail",new(.15f,.15f,1.86f),new(x,.24f,0),
                "#6d4932",HouseStage.Foundation);
        foreach(float z in new[]{-edge,edge})
            Box("FoundationCrossRail",new(1.72f,.14f,.16f),new(0,.24f,z),
                "#765037",HouseStage.Foundation);

        // Frame: real upright posts and the roof-support beams.
        foreach(float x in new[]{-edge,edge})
        foreach(float z in new[]{-edge,edge})
            Box("FramePost",new(.16f,1.35f,.16f),new(x,.92f,z),
                "#755139",HouseStage.Frame);
        foreach(float x in new[]{-edge,edge})
            Box("TopLongBeam",new(.16f,.16f,1.88f),new(x,1.61f,0),
                "#674530",HouseStage.Frame);
        foreach(float z in new[]{-edge,edge})
            Box("TopCrossBeam",new(1.74f,.15f,.16f),new(0,1.61f,z),
                "#674530",HouseStage.Frame);

        // Roof: two slim opaque pitched panels. No transparent billboard
        // leaves or changes to the previously rejected foliage materials.
        Box("RoofLeft",new(1.13f,.105f,2.07f),
            new(-.46f,1.82f,0),"#a35c45",
            HouseStage.Roof,HouseStage.Complete,
            new(0,0,Mathf.DegToRad(34f)));
        Box("RoofRight",new(1.13f,.105f,2.07f),
            new(.46f,1.82f,0),"#a35c45",
            HouseStage.Roof,HouseStage.Complete,
            new(0,0,Mathf.DegToRad(-34f)));
        Box("RoofRidge",new(.15f,.13f,2.13f),new(0,2.14f,0),
            "#754432",HouseStage.Roof);

        // Completion: warm wooden walls, door and two shuttered windows.
        Box("BackWall",new(1.57f,1.17f,.11f),new(0,.91f,-.81f),
            "#b68b61",HouseStage.Complete);
        foreach(float x in new[]{-edge,edge})
            Box("SideWall",new(.11f,1.17f,1.55f),new(x,.91f,0),
                "#ad815a",HouseStage.Complete);
        Box("FrontWallLeft",new(.56f,1.16f,.11f),new(-.55f,.91f,.81f),
            "#b68b61",HouseStage.Complete);
        Box("FrontWallRight",new(.56f,1.16f,.11f),new(.55f,.91f,.81f),
            "#b68b61",HouseStage.Complete);
        Box("FrontDoor",new(.53f,.89f,.035f),new(0,.70f,.88f),
            "#69432e",HouseStage.Complete);
        Box("DoorHandle",new(.055f,.05f,.055f),new(.17f,.75f,.917f),
            "#e0b969",HouseStage.Complete);
        foreach(float x in new[]{-edge,edge})
        {
            Box("Window",new(.014f,.36f,.42f),
                new(x*.996f,1.03f,-.19f),
                "#e7d8a9",HouseStage.Complete);
            foreach(float z in new[]{-.44f,.06f})
                Box("WindowShutter",new(.035f,.39f,.09f),
                    new(x*.997f,1.03f,z),
                    "#765035",HouseStage.Complete);
        }
        SetStage(HouseStage.Planned);
    }

    private void Box(string name,Vector3 size,Vector3 at,string color,
        HouseStage from,HouseStage through=HouseStage.Complete,
        Vector3 rotation=default)
    {
        if(!_materials.TryGetValue(color,out var material))
        {
            material=new StandardMaterial3D
            {
                AlbedoColor=new Color(color),
                Roughness=.94f,Metallic=0f
            };
            _materials.Add(color,material);
        }
        var mesh=new MeshInstance3D
        {
            Name=name, Mesh=new BoxMesh{Size=size},
            Position=at, Rotation=rotation,MaterialOverride=material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.On
        };
        _house.AddChild(mesh);
        _parts.Add((mesh,from,through));
    }

    public void BindWorld(WorldState world,VisualTerrain3D terrain,
        Godot.Camera3D camera)
    {
        _world=world;
        _terrain=terrain;
        _camera=camera;
        _shownHouseId=0;
        _shownStage=HouseStage.Planned;
        _elapsed=0;
        _house.Visible=false;
        RefreshNow();
    }

    public void Tick(double delta)
    {
        _elapsed+=Math.Clamp(delta,0,.10);
        if(_elapsed<.15)return;
        _elapsed=0;
        RefreshNow();
    }

    public void RefreshNow()
    {
        if(_world is null || _terrain is null || _camera is null)return;
        WoodenHouse? site=_world.FirstHouse;
        if(site is null)
        {
            _house.Visible=false;
            _shownHouseId=0;
            return;
        }

        if(site.Id!=_shownHouseId)
        {
            _shownHouseId=site.Id;
            GD.Print($"Wooden house site selected at ({site.X},{site.Y}), entrance ({site.EntranceX},{site.EntranceY})");
        }
        if(site.Stage!=_shownStage)
            GD.Print($"Wooden house {site.Id}: {site.Stage}; lumber {site.WoodDelivered}/{WoodenHouse.RequiredWood}, work {site.WorkDone}, residents {site.Residents}/{WoodenHouse.Capacity}");
        _shownStage=site.Stage;
        SetStage(site.Stage);

        float y=0f;
        for(int dz=0;dz<2;dz++)
        for(int dx=0;dx<2;dx++)
            y+=_terrain.SurfaceHeightAt(site.X+dx,site.Y+dz,.5f,.5f)*.25f;
        _house.Position=new Vector3(site.X+1f,y-.04f,site.Y+1f);

        Vector3 center=new(site.X+1f,y+1.1f,site.Y+1f);
        if(_camera.IsPositionBehind(center))
        {
            _house.Visible=false;
            return;
        }
        Vector2 projected=_camera.UnprojectPosition(center);
        Vector2 view=_camera.GetViewport().GetVisibleRect().Size;
        _house.Visible=projected.X>=-ViewMarginPixels &&
            projected.Y>=-ViewMarginPixels &&
            projected.X<=view.X+ViewMarginPixels &&
            projected.Y<=view.Y+ViewMarginPixels;
    }

    private void SetStage(HouseStage stage)
    {
        foreach(var part in _parts)
            part.Part.Visible=stage>=part.From && stage<=part.Through;
    }
}
