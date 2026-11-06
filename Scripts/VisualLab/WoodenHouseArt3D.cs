using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Generation 1: a hand-built, old-world wattle-and-timber cottage with a
/// fieldstone base and worn straw/shingle roof. This is one architectural
/// archetype reused by all existing village houses, not a new home tier.
/// Pure presentation: construction stages and placement come only from
/// WorldSim, never from the camera or visual meshes.
/// </summary>
public sealed partial class WoodenHouseArt3D : Node3D
{
    // Modules are individual authored geometry batches, not one Node3D per
    // stone, nail, plank, roof tile or straw bundle. Vertex colors supply the
    // palette, so every module can share ONE cached opaque material.
    private readonly List<(MeshInstance3D Part,HouseStage From,HouseStage Through)> _parts=new();
    private readonly List<Node3D> _extraModels=new();
    private readonly List<MeshInstance3D[]> _extraParts=new();
    private readonly List<HouseStage> _extraShownStages=new();

    private readonly StandardMaterial3D _houseMaterial=new()
    {
        VertexColorUseAsAlbedo=true,
        AlbedoColor=Colors.White,
        Roughness=.94f,
        Metallic=0f,
        CullMode=BaseMaterial3D.CullModeEnum.Disabled
    };
    private Node3D _house=new();
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private Godot.Camera3D? _camera;
    private int _shownHouseId;
    private HouseStage _shownStage;
    private double _elapsed;
    private const float ViewMarginPixels=120f;

    private static readonly Color Stone=new("#807B6A");
    private static readonly Color StoneLight=new("#A39C85");
    private static readonly Color StoneDark=new("#625E52");
    private static readonly Color Mortar=new("#615447");
    private static readonly Color Timber=new("#725039");
    private static readonly Color TimberDark=new("#4D3428");
    private static readonly Color TimberLight=new("#95704F");
    private static readonly Color Plaster=new("#B5A17C");
    private static readonly Color PlasterDark=new("#9C8765");
    private static readonly Color Thatch=new("#AA8652");
    private static readonly Color ThatchDark=new("#836342");
    private static readonly Color ThatchLight=new("#C0A16B");
    private static readonly Color Glass=new("#D5AF69");
    private static readonly Color Iron=new("#45433E");

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
        Name="FirstWoodenHouse"; // Preserve existing scene/test references.
        _house=new Node3D{Name="HouseModel",Visible=false};
        AddChild(_house);
        BuildGenerationOne();
        SetStage(HouseStage.Planned);

        // WorldSim may build up to three houses. Every house displays this
        // exact first-generation archetype. Geometry and material resources
        // are shared across copies; upgrades can swap modules later.
        for(int i=1;i<WorldState.MaxHouseCount;i++)
        {
            var copy=(Node3D)_house.Duplicate();
            copy.Name=$"HouseModel{i+1}";
            copy.Visible=false;
            AddChild(copy);
            _extraModels.Add(copy);
            var parts=new MeshInstance3D[_parts.Count];
            for(int p=0;p<parts.Length;p++)
                parts[p]=(MeshInstance3D)copy.GetChild(p);
            _extraParts.Add(parts);
            _extraShownStages.Add(HouseStage.Planned);
        }
    }

    private void BuildGenerationOne()
    {
        // 2x2 simulation footprint is fixed. Art, including the slight eaves
        // overhang, never changes the logical walkable area or the door tile.

        // PLANNED: rough survey pegs, twine and cleared earth.
        Module("Survey_Corner_Pegs",HouseStage.Planned,HouseStage.Planned,true,b=>
        {
            foreach(float x in new[]{-.80f,.80f})
            foreach(float z in new[]{-.80f,.80f})
            {
                b.Box(new(.075f,.42f,.075f),new(x,.22f,z),Timber);
                b.Box(new(.14f,.035f,.14f),new(x,.41f,z),TimberLight);
            }
        });
        Module("Survey_Rope_Sides",HouseStage.Planned,HouseStage.Planned,false,b=>
        {
            foreach(float x in new[]{-.80f,.80f})
                b.Box(new(.022f,.024f,1.63f),new(x,.15f,0),ThatchLight);
        });
        Module("Survey_Rope_Ends",HouseStage.Planned,HouseStage.Planned,false,b=>
        {
            foreach(float z in new[]{-.80f,.80f})
                b.Box(new(1.63f,.024f,.022f),new(0,.15f,z),ThatchLight);
        });
        Module("Survey_Ground_Marks",HouseStage.Planned,HouseStage.Planned,false,b=>
        {
            for(int i=0;i<8;i++)
            {
                float x=-.63f+(i%4)*.40f;
                float z=i<4?-.74f:.74f;
                b.Box(new(.13f,.065f,.10f),new(x,.04f,z),
                    i%2==0?StoneLight:Stone);
            }
        });

        // FOUNDATION: individually shaped fieldstone courses and real timber
        // floor, not a perfectly smooth concrete-looking box.
        Module("Foundation_Fieldstone_Outer",HouseStage.Foundation,
            HouseStage.Complete,true,b=>
        {
            for(int i=0;i<10;i++)
            {
                float t=-.79f+i*.176f;
                float w=.16f+(i%3)*.018f;
                float h=.19f+(i%4)*.022f;
                Color color=i%3==0?StoneLight:i%3==1?Stone:StoneDark;
                b.Box(new(w,h,.18f),new(t,h*.5f,-.82f),color);
                b.Box(new(w,h,.18f),new(t,h*.5f,.82f),
                    i%2==0?Stone:StoneLight);
                b.Box(new(.18f,h,w),new(-.82f,h*.5f,t),color);
                b.Box(new(.18f,h,w),new(.82f,h*.5f,t),
                    i%2==0?StoneDark:Stone);
            }
        });
        Module("Foundation_Stone_Cap",HouseStage.Foundation,
            HouseStage.Complete,true,b=>
        {
            b.Box(new(1.68f,.11f,1.68f),new(0,.23f,0),StoneDark);
            for(int i=0;i<8;i++)
            {
                float x=-.67f+i*.19f;
                b.Box(new(.17f,.07f,.13f),new(x,.29f,-.78f),
                    i%2==0?StoneLight:Stone);
                b.Box(new(.17f,.07f,.13f),new(x,.29f,.78f),
                    i%3==0?StoneDark:StoneLight);
            }
        });
        Module("Foundation_Plank_Floor",HouseStage.Foundation,
            HouseStage.Complete,false,b=>
        {
            for(int i=0;i<9;i++)
            {
                float x=-.70f+i*.175f;
                b.Box(new(.167f,.055f,1.48f),new(x,.33f,0),
                    i%3==0?TimberLight:i%3==1?Timber:TimberDark);
            }
        });
        Module("Foundation_Timber_Sills",HouseStage.Foundation,
            HouseStage.Complete,true,b=>
        {
            foreach(float x in new[]{-.76f,.76f})
                b.Box(new(.12f,.16f,1.70f),new(x,.41f,0),TimberDark);
            foreach(float z in new[]{-.77f,.77f})
                b.Box(new(1.65f,.16f,.12f),new(0,.41f,z),Timber);
        });
        Module("Foundation_Doorstep",HouseStage.Foundation,
            HouseStage.Complete,false,b=>
        {
            // The entrance work tile is on the south (positive Z) edge.
            // These low stones remain decorative rather than collidable.
            b.Box(new(.72f,.13f,.27f),new(-.39f,.10f,.92f),StoneLight);
            b.Box(new(.66f,.055f,.17f),new(-.39f,.20f,.82f),TimberDark);
            b.Box(new(.14f,.10f,.20f),new(-.78f,.065f,.95f),Stone);
            b.Box(new(.15f,.09f,.19f),new(.04f,.06f,.95f),Stone);
        });

        // FRAME: real posts, headers, braces and pairs of sloping rafters.
        Module("Frame_Old_Oak_Posts",HouseStage.Frame,
            HouseStage.Complete,true,b=>
        {
            foreach(float x in new[]{-.76f,.76f})
            foreach(float z in new[]{-.76f,.76f})
            {
                b.Box(new(.145f,1.26f,.145f),new(x,1.11f,z),TimberDark);
                b.Box(new(.19f,.09f,.19f),new(x,.53f,z),TimberLight);
                b.Box(new(.19f,.08f,.19f),new(x,1.68f,z),Timber);
            }
        });
        Module("Frame_Lintel_and_Wall_Beams",HouseStage.Frame,
            HouseStage.Complete,true,b=>
        {
            foreach(float z in new[]{-.77f,.77f})
            {
                b.Box(new(1.73f,.15f,.15f),new(0,1.72f,z),TimberDark);
                b.Box(new(1.54f,.07f,.09f),new(0,.61f,z),TimberLight);
            }
            foreach(float x in new[]{-.77f,.77f})
            {
                b.Box(new(.15f,.15f,1.70f),new(x,1.72f,0),TimberDark);
                b.Box(new(.08f,.08f,1.56f),new(x,.68f,0),Timber);
            }
        });
        Module("Frame_Diagonal_Braces",HouseStage.Frame,
            HouseStage.Complete,true,b=>
        {
            foreach(float z in new[]{-.79f,.79f})
            {
                b.Beam(new(-.71f,.65f,z),new(-.23f,1.56f,z),.09f,TimberLight);
                b.Beam(new(.71f,.65f,z),new(.23f,1.56f,z),.09f,Timber);
            }
            foreach(float x in new[]{-.78f,.78f})
            {
                b.Box(new(.10f,.45f,.10f),new(x,.87f,-.48f),TimberLight);
                b.Box(new(.10f,.44f,.10f),new(x,.88f,.49f),Timber);
            }
        });
        Module("Frame_Rough_Rafters",HouseStage.Frame,
            HouseStage.Complete,true,b=>
        {
            foreach(float z in new[]{-.82f,0f,.82f})
            {
                b.Box(new(1.24f,.13f,.12f),new(-.49f,1.86f,z),
                    TimberDark,Mathf.DegToRad(32f));
                b.Box(new(1.24f,.13f,.12f),new(.49f,1.86f,z),
                    TimberDark,Mathf.DegToRad(-32f));
            }
            b.Box(new(.13f,.15f,1.94f),new(0,2.14f,0),Timber);
        });

        // ROOF: simple old straw/thatch over crooked timber slats. The first
        // generation does NOT have glazed tiles or luxury metal fittings.
        Module("Roof_Thatch_Underlay",HouseStage.Roof,
            HouseStage.Complete,true,b=>
        {
            b.Box(new(1.26f,.095f,2.16f),new(-.50f,1.85f,0),
                ThatchDark,Mathf.DegToRad(32f));
            b.Box(new(1.26f,.095f,2.16f),new(.50f,1.85f,0),
                ThatchDark,Mathf.DegToRad(-32f));
        });
        Module("Roof_Weathered_Thatch_Courses",HouseStage.Roof,
            HouseStage.Complete,true,b=>
        {
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<4;row++)
            for(int col=0;col<6;col++)
            {
                float ax=.18f+row*.23f;
                float x=side*ax;
                float y=2.17f-ax*.625f+.057f;
                float z=-.875f+col*.35f+(row%2==0?0f:.03f);
                Color color=((col+row*3)%5) switch
                {
                    0=>ThatchDark,
                    1=>ThatchLight,
                    _=>Thatch
                };
                b.Box(new(.272f,.042f,.36f),
                    new(x,y,z),color,Mathf.DegToRad(side==-1?32f:-32f));
            }
        });
        Module("Roof_Ridge_and_Eave_Timbers",HouseStage.Roof,
            HouseStage.Complete,true,b=>
        {
            b.Box(new(.18f,.105f,2.24f),new(0,2.19f,0),TimberDark);
            foreach(float x in new[]{-1.025f,1.025f})
                b.Box(new(.10f,.08f,2.17f),new(x,1.54f,0),Timber);
            foreach(float z in new[]{-1.07f,1.07f})
                b.Box(new(2.04f,.08f,.085f),new(0,1.54f,z),TimberDark);
        });
        Module("Roof_Loose_Straw_Edges",HouseStage.Roof,
            HouseStage.Complete,false,b=>
        {
            for(int i=0;i<10;i++)
            {
                float z=-.96f+i*.211f;
                float drop=.055f+(i%3)*.031f;
                foreach(float x in new[]{-.99f,.99f})
                    b.Box(new(.044f,.11f+drop,.068f),
                        new(x,1.48f-drop*.5f,z),
                        i%3==0?ThatchLight:Thatch);
            }
            for(int i=0;i<7;i++)
            {
                float z=-.90f+i*.30f;
                b.Box(new(.085f,.05f,.13f),new(0,2.25f,z),
                    i%2==0?Thatch:ThatchLight);
            }
        });

        // COMPLETE: imperfect earth plaster, timber trims and small lived-in
        // details. Still an early, modest house: no marble, bright metal, or
        // giant chimney; more elaborate architecture belongs to later tiers.
        Module("Walls_Clay_and_Gables",HouseStage.Complete,
            HouseStage.Complete,true,b=>
        {
            b.Box(new(1.48f,1.10f,.105f),new(0,1.09f,-.71f),Plaster);
            foreach(float x in new[]{-.71f,.71f})
                b.Box(new(.095f,1.10f,1.38f),new(x,1.09f,0),PlasterDark);
            // South face: physical doorway at local X=-.39 aligned with the
            // WorldSim entrance lane rather than centered on the wrong tile.
            b.Box(new(.12f,1.12f,.105f),new(-.665f,1.07f,.72f),Plaster);
            b.Box(new(.86f,1.12f,.105f),new(.34f,1.07f,.72f),Plaster);
            b.Box(new(.54f,.20f,.105f),new(-.39f,1.69f,.72f),PlasterDark);
            b.Triangle(new(-.72f,1.74f,-.72f),new(.72f,1.74f,-.72f),
                new(0,2.13f,-.72f),Plaster);
            b.Triangle(new(.72f,1.74f,.72f),new(-.72f,1.74f,.72f),
                new(0,2.13f,.72f),PlasterDark);
        });
        Module("Walls_Timber_Laths",HouseStage.Complete,
            HouseStage.Complete,false,b=>
        {
            foreach(float z in new[]{-.778f,.778f})
            {
                b.Box(new(1.40f,.07f,.09f),new(0,1.20f,z),TimberLight);
                for(int i=-1;i<=1;i++)
                    b.Box(new(.07f,1.02f,.085f),
                        new(i*.46f,1.08f,z),Timber);
            }
            foreach(float x in new[]{-.78f,.78f})
                b.Box(new(.075f,.08f,1.49f),new(x,1.12f,0),TimberDark);
            b.Box(new(.63f,.16f,.15f),new(-.39f,1.47f,.80f),TimberDark);
        });
        Module("Entrance_Handmade_Oak_Door",HouseStage.Complete,
            HouseStage.Complete,true,b=>
        {
            for(int i=0;i<4;i++)
            {
                float x=-.575f+i*.125f;
                b.Box(new(.12f,1.10f,.052f),new(x,.91f,.797f),
                    i%2==0?TimberDark:Timber);
            }
            b.Box(new(.53f,.08f,.07f),new(-.39f,.56f,.848f),TimberLight);
            b.Box(new(.53f,.08f,.07f),new(-.39f,1.21f,.848f),TimberLight);
            b.Beam(new(-.58f,.59f,.86f),new(-.20f,1.19f,.86f),
                .055f,TimberLight);
            for(float y=.65f;y<=1.24f;y+=.54f)
                b.Box(new(.06f,.075f,.055f),new(-.59f,y,.871f),Iron);
            b.Box(new(.065f,.065f,.05f),new(-.21f,.97f,.875f),Iron);
        });
        Module("Windows_Timber_Shutters",HouseStage.Complete,
            HouseStage.Complete,false,b=>
        {
            // A small amber window looks occupied at isometric zoom, without
            // a real local light, transparent sorting, or emissive cost.
            b.Box(new(.31f,.34f,.035f),new(.39f,1.15f,.803f),Glass);
            b.Box(new(.39f,.06f,.09f),new(.39f,1.37f,.84f),TimberDark);
            b.Box(new(.39f,.06f,.09f),new(.39f,.93f,.84f),TimberDark);
            foreach(float x in new[]{.19f,.59f})
                b.Box(new(.055f,.44f,.085f),new(x,1.15f,.84f),Timber);
            b.Box(new(.035f,.34f,.056f),new(.39f,1.15f,.855f),TimberLight);
            b.Box(new(.31f,.035f,.056f),new(.39f,1.15f,.856f),TimberLight);
            b.Box(new(.105f,.44f,.06f),new(.67f,1.15f,.832f),TimberDark);
            // Matching side window.
            b.Box(new(.032f,.32f,.32f),new(.794f,1.20f,-.10f),Glass);
            foreach(float z in new[]{-.31f,.11f})
                b.Box(new(.07f,.43f,.055f),new(.817f,1.20f,z),Timber);
            foreach(float y in new[]{1.0f,1.4f})
                b.Box(new(.07f,.055f,.44f),new(.817f,y,-.10f),TimberDark);
        });
        Module("Village_One_Quiet_Details",HouseStage.Complete,
            HouseStage.Complete,false,b=>
        {
            // Short rough stone flue: an early hearth, not a developed chimney.
            b.Box(new(.26f,.63f,.28f),new(.45f,2.12f,-.45f),StoneDark);
            b.Box(new(.31f,.09f,.32f),new(.45f,2.47f,-.45f),StoneLight);
            b.Box(new(.17f,.018f,.17f),new(.45f,2.525f,-.45f),Mortar);
            // Firewood stored at the sheltered side of the threshold.
            for(int i=0;i<3;i++)
            {
                float z=-.04f+i*.20f;
                b.Box(new(.42f,.105f,.13f),new(.55f,.43f,z),
                    i==1?TimberDark:TimberLight);
            }
            // A humble dark wooden storage chest by the cottage wall.
            b.Box(new(.33f,.24f,.32f),new(.54f,.50f,-.48f),TimberDark);
            b.Box(new(.36f,.055f,.35f),new(.54f,.65f,-.48f),TimberLight);
        });
    }

    private void Module(string name,HouseStage from,HouseStage through,
        bool castsShadow,Action<ShapeBatch> draw)
    {
        using var batch=new ShapeBatch(_houseMaterial);
        draw(batch);
        var instance=new MeshInstance3D
        {
            Name=name,
            Mesh=batch.Finish(),
            CastShadow=castsShadow
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off
        };
        _house.AddChild(instance);
        _parts.Add((instance,from,through));
    }

    /// <summary>
    /// Single geometry batch per construction module, all native triangles
    /// authored once. No BoxMesh-per-stone or per-frame SceneTree churn.
    /// Faces have real normals and slightly varied vertex colors for an old,
    /// uneven hand-worked appearance. Sloped boards rotate around local Z.
    /// </summary>
    private sealed class ShapeBatch : IDisposable
    {
        private readonly SurfaceTool _surface=new();

        internal ShapeBatch(Material material)
        {
            _surface.Begin(Mesh.PrimitiveType.Triangles);
            _surface.SetMaterial(material);
        }

        internal void Box(Vector3 size,Vector3 center,Color color,float tilt=0f)
        {
            float x=size.X*.5f,y=size.Y*.5f,z=size.Z*.5f;
            float cosine=MathF.Cos(tilt),sine=MathF.Sin(tilt);
            Vector3 Rotate(float px,float py,float pz)=>
                new(center.X+px*cosine-py*sine,
                    center.Y+px*sine+py*cosine,center.Z+pz);
            Vector3[] v=
            {
                Rotate(-x,-y,-z),Rotate(x,-y,-z),
                Rotate(x,y,-z),Rotate(-x,y,-z),
                Rotate(-x,-y,z),Rotate(x,-y,z),
                Rotate(x,y,z),Rotate(-x,y,z)
            };
            Quad(v[4],v[5],v[6],v[7],Shade(color,1f));
            Quad(v[1],v[0],v[3],v[2],Shade(color,.78f));
            Quad(v[5],v[1],v[2],v[6],Shade(color,.86f));
            Quad(v[0],v[4],v[7],v[3],Shade(color,.75f));
            Quad(v[7],v[6],v[2],v[3],Shade(color,1.06f));
            Quad(v[0],v[1],v[5],v[4],Shade(color,.68f));
        }

        internal void Beam(Vector3 from,Vector3 to,float thickness,Color color)
        {
            Vector3 direction=to-from;
            float length=MathF.Sqrt(direction.X*direction.X+
                direction.Y*direction.Y);
            if(length<=.0001f)return;
            float angle=-MathF.Atan2(direction.X,direction.Y);
            Box(new(thickness,length,thickness),(from+to)*.5f,color,angle);
        }

        internal void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            Emit(a,b,c,color);
            Emit(c,b,a,Shade(color,.86f));
        }

        private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            Emit(a,b,c,color);
            Emit(a,c,d,color);
        }

        private void Emit(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            Vector3 normal=(b-a).Cross(c-a).Normalized();
            _surface.SetNormal(normal);
            _surface.SetColor(color);_surface.AddVertex(a);
            _surface.SetColor(color);_surface.AddVertex(b);
            _surface.SetColor(color);_surface.AddVertex(c);
        }

        private static Color Shade(Color c,float gain)=>
            new(MathF.Min(1f,c.R*gain),MathF.Min(1f,c.G*gain),
                MathF.Min(1f,c.B*gain),1f);

        internal ArrayMesh Finish() => _surface.Commit();

        public void Dispose() => _surface.Dispose();
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
        foreach(Node3D extra in _extraModels)extra.Visible=false;
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
        RefreshExtraHouses();
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
        {
            _shownStage=site.Stage;
            SetStage(site.Stage);
            GD.Print($"Generation 1 house {site.Id}: {site.Stage}; lumber {site.WoodDelivered}/{WoodenHouse.RequiredWood}, work {site.WorkDone}, residents {site.Residents}/{WoodenHouse.Capacity}");
        }
        _house.Position=new Vector3(site.X+1f,site.GroundHeight,site.Y+1f);
        _house.Visible=IsHouseVisible(site);
    }

    private void RefreshExtraHouses()
    {
        if(_world is null || _camera is null)return;
        for(int i=0;i<_extraModels.Count;i++)
        {
            Node3D model=_extraModels[i];
            int houseIndex=i+1;
            if(houseIndex>=_world.Houses.Count)
            {
                model.Visible=false;
                continue;
            }
            WoodenHouse site=_world.Houses[houseIndex];
            if(site.Stage!=_extraShownStages[i])
            {
                _extraShownStages[i]=site.Stage;
                MeshInstance3D[] parts=_extraParts[i];
                for(int p=0;p<parts.Length;p++)
                {
                    var rule=_parts[p];
                    parts[p].Visible=site.Stage>=rule.From &&
                        site.Stage<=rule.Through;
                }
            }
            model.Position=new Vector3(site.X+1f,site.GroundHeight,site.Y+1f);
            model.Visible=IsHouseVisible(site);
        }
    }

    private bool IsHouseVisible(WoodenHouse site)
    {
        if(_camera is null)return false;
        Vector3 center=new(site.X+1f,site.GroundHeight+1.1f,site.Y+1f);
        if(_camera.IsPositionBehind(center))return false;
        Vector2 at=_camera.UnprojectPosition(center);
        Vector2 size=_camera.GetViewport().GetVisibleRect().Size;
        return at.X>=-ViewMarginPixels && at.Y>=-ViewMarginPixels &&
            at.X<=size.X+ViewMarginPixels && at.Y<=size.Y+ViewMarginPixels;
    }

    private void SetStage(HouseStage stage)
    {
        foreach(var part in _parts)
            part.Part.Visible=stage>=part.From && stage<=part.Through;
    }
}
