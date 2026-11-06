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
    private static readonly Color PlasterWear=new("#8F795B");
    private static readonly Color PlasterLight=new("#C9B790");
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
                // Nonidentical stones, slightly staggered between courses.
                float t=-.79f+i*.176f;
                float w=.157f+(i%4)*.018f;
                float h=.18f+(i%5)*.017f;
                float offset=((i*7)%5-2)*.008f;
                float tilt=((i%3)-1)*.028f;
                Color color=i%3==0?StoneLight:i%3==1?Stone:StoneDark;
                b.Box(new(w,h,.18f),new(t+offset,h*.5f,-.82f),color,tilt);
                b.Box(new(w,h,.18f),new(t-offset,h*.5f,.82f),
                    i%2==0?Stone:StoneLight,-tilt);
                b.Box(new(.18f,h,w),new(-.82f,h*.5f,t+offset),color);
                b.Box(new(.18f,h,w),new(.82f,h*.5f,t-offset),
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
            // Gen1.1: overlapping, irregular tapered straw bundles replace
            // a regular grid of identical rectangular roof tiles. Each
            // textured-looking bundle is still just opaque colored geometry,
            // baked into this ONE mesh (no shaders or per-bundle draw calls).
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<5;row++)
            for(int col=0;col<7;col++)
            {
                int seed=17+row*37+col*19+(side+1)*23;
                float wobble=((seed%11)-5)*.004f;
                float ax=.13f+row*.21f;
                float z=-.91f+col*.303f+
                    (row%2==0?-.012f:.017f)+wobble;
                float y=2.17f-ax*.625f+.056f+wobble*.4f;
                int tone=(row*13+col*7+(side+1)*3)%9;
                Color straw=tone switch
                {
                    0 or 7=>ThatchDark,
                    2 or 6=>ThatchLight,
                    _=>Thatch
                };
                float width=.29f+((seed%5)-2)*.011f;
                float length=.34f+((seed%7)-3)*.008f;
                float fringe=.022f+((seed%6)*.008f);
                b.ThatchBundle(new(side*ax,y,z),
                    Mathf.DegToRad(side<0?32f:-32f),
                    width,length,fringe,side,straw);
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
            // A broken fringe prevents an unnaturally razor-straight
            // roof silhouette at both isometric distance and close zoom.
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<15;i++)
            {
                float z=-.99f+i*.142f;
                float fall=.075f+((i*7+side+3)%5)*.019f;
                float x=side*(1.043f+((i*3+5)%4)*.006f);
                b.Box(new(.025f,.07f+fall,.052f),
                    new(x,1.505f-fall*.44f,z),
                    i%4==0?ThatchLight:i%3==0?ThatchDark:Thatch);
            }
            // Irregular hand-bound straw tied around the ridge.
            for(int i=0;i<12;i++)
            {
                float z=-1.03f+i*.185f;
                float h=.032f+(i%4)*.011f;
                b.Box(new(.13f,.045f+h,.14f),
                    new((i%3-1)*.008f,2.235f+h*.2f,z),
                    i%3==0?ThatchLight:Thatch);
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

            // Thin irregular color patches break perfectly flat plaster
            // without normal maps, transparency or additional draw calls.
            b.Triangle(new(.31f,.76f,.78f),new(.56f,.81f,.78f),
                new(.47f,1.00f,.78f),PlasterLight);
            b.Triangle(new(.40f,1.42f,.783f),new(.56f,1.38f,.783f),
                new(.51f,1.58f,.783f),PlasterWear);
            b.Triangle(new(-.62f,.80f,-.777f),new(-.45f,.87f,-.777f),
                new(-.54f,1.06f,-.777f),PlasterWear);
            b.Triangle(new(.25f,1.20f,-.777f),new(.46f,1.18f,-.777f),
                new(.32f,1.41f,-.777f),PlasterLight);
        });
        Module("Walls_Gable_Handhewn_Timbers",HouseStage.Complete,
            HouseStage.Complete,false,b=>
        {
            // Exposed oak outlines the wattle-filled gables and makes the
            // roof triangle visible at isometric game distance.
            foreach(float z in new[]{-.815f,.815f})
            {
                b.Beam(new(-.72f,1.74f,z),new(0,2.14f,z),
                    .083f,TimberDark);
                b.Beam(new(.72f,1.74f,z),new(0,2.14f,z),
                    .083f,Timber);
                b.Box(new(.068f,.37f,.07f),
                    new(0,1.905f,z),TimberLight);
                b.Box(new(.21f,.065f,.07f),
                    new(0,1.79f,z),TimberDark);
            }
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
            // A few uneven laths show how this early house was hand built.
            for(int i=0;i<4;i++)
            {
                float z=-.53f+i*.34f;
                b.Box(new(.052f,.47f+((i%3)*.05f),.07f),
                    new(-.81f,.93f,z),i%2==0?TimberLight:Timber);
            }
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
            // Rough lintel, two slightly uneven wooden door jambs, and
            // pinned iron hinges: legible shapes without a bright UI marker.
            foreach(float x in new[]{-.69f,-.09f})
                b.Box(new(.095f,1.24f,.10f),
                    new(x,1.07f,.854f),TimberDark);
            b.Box(new(.76f,.12f,.12f),new(-.39f,1.72f,.84f),TimberLight);
            foreach(float y in new[]{.68f,1.17f})
                b.Box(new(.17f,.038f,.043f),
                    new(-.57f,y,.904f),Iron);
            b.Box(new(.12f,.04f,.05f),new(-.21f,.97f,.910f),TimberLight);
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
            b.Box(new(.46f,.07f,.17f),new(.39f,.91f,.88f),StoneLight);
            b.Box(new(.10f,.40f,.075f),new(.12f,1.15f,.86f),TimberDark);
            // Small wooden pegs holding the shutters in place.
            foreach(float y in new[]{1.03f,1.29f})
                b.Box(new(.029f,.027f,.035f),new(.67f,y,.871f),Iron);
            // Matching side window.
            b.Box(new(.032f,.32f,.32f),new(.794f,1.20f,-.10f),Glass);
            foreach(float z in new[]{-.31f,.11f})
                b.Box(new(.07f,.43f,.055f),new(.817f,1.20f,z),Timber);
            foreach(float y in new[]{1.0f,1.4f})
                b.Box(new(.07f,.055f,.44f),new(.817f,y,-.10f),TimberDark);
            b.Box(new(.19f,.07f,.52f),new(.83f,.98f,-.10f),Stone);
            b.Box(new(.035f,.33f,.05f),new(.85f,1.20f,-.10f),TimberLight);
        });
        Module("Village_One_Quiet_Details",HouseStage.Complete,
            HouseStage.Complete,false,b=>
        {
            // Layered stone flue with uneven mortar lines and dark throat;
            // modest enough for an early peasant hearth, not a manor.
            b.Box(new(.28f,.61f,.29f),new(.45f,2.13f,-.45f),StoneDark);
            for(int i=0;i<4;i++)
            {
                float y=1.91f+i*.16f;
                b.Box(new(.30f,.037f,.32f),new(.45f,y,-.45f),
                    i%2==0?Stone:StoneLight);
                b.Box(new(.10f,.12f,.045f),
                    new(.40f+(i%2)*.10f,y+.071f,-.29f),
                    i%2==0?StoneLight:Stone);
            }
            b.Box(new(.36f,.095f,.36f),new(.45f,2.49f,-.45f),StoneLight);
            b.Box(new(.18f,.024f,.18f),new(.45f,2.544f,-.45f),Mortar);

            // Firewood moved from behind the walls to an exposed dry stack.
            // Small, readable silhouettes, no individual loose physics props.
            for(int i=0;i<5;i++)
            {
                float z=-.34f+(i%3)*.19f;
                float y=.37f+(i/3)*.115f;
                b.Box(new(.29f,.095f,.13f),
                    new(.92f,y,z),i%3==0?TimberLight:TimberDark);
                b.Box(new(.025f,.075f,.07f),
                    new(1.078f,y,z),i%2==0?Timber:TimberLight);
            }
            // A low storage chest sits beside, not in front of, the door.
            b.Box(new(.33f,.24f,.31f),new(.56f,.43f,.95f),TimberDark);
            b.Box(new(.37f,.058f,.34f),new(.56f,.58f,.95f),TimberLight);
            b.Box(new(.035f,.075f,.05f),new(.56f,.48f,1.13f),Iron);

            // Rough stepping stones link the door to the entrance tile.
            for(int i=0;i<3;i++)
            {
                float z=1.03f+i*.12f;
                b.Box(new(.27f+(i%2)*.05f,.045f,.11f),
                    new(-.39f+(i%2)*.025f,.028f,z),
                    i%2==0?StoneLight:StoneDark);
            }
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

        /// <summary>
        /// Closed, slightly drooping straw patch. The far edge has its own
        /// uneven silhouette; both the upper face and its shallow underside
        /// use real normals. All patches share the parent module surface.
        /// </summary>
        internal void ThatchBundle(Vector3 center,float slope,float width,
            float depth,float fringe,int side,Color color)
        {
            float c=MathF.Cos(slope),sn=MathF.Sin(slope);
            Vector3 Point(float u,float v,float z)=>
                new(center.X+u*c-v*sn,
                    center.Y+u*sn+v*c,center.Z+z);
            float w=width*.5f,d=depth*.5f;
            bool outerLeft=side<0;
            float droop=fringe;
            Vector3 a=Point(-w,.024f-(outerLeft?droop:0f),-d);
            Vector3 b=Point(w,.032f-(!outerLeft?droop:0f),-d+.006f);
            Vector3 c1=Point(w,.033f-(!outerLeft?droop*.76f:0f),d-.009f);
            Vector3 d1=Point(-w,.027f-(outerLeft?droop*.78f:0f),d);
            Vector3 down=Vector3.Down*.047f;
            // Winding follows the outward normal of a roof face. The
            // underside is reversed, avoiding accidentally dark tops.
            Quad(d1,c1,b,a,color);
            Quad(a+down,b+down,c1+down,d1+down,Shade(color,.66f));
            Quad(a,a+down,b+down,b,Shade(color,.82f));
            Quad(b,b+down,c1+down,c1,Shade(color,.78f));
            Quad(c1,c1+down,d1+down,d1,Shade(color,.76f));
            Quad(d1,d1+down,a+down,a,Shade(color,.73f));
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
