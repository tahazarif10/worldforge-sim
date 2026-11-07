using System;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Pure presentation for a small, first-generation cottage yard. The actual
/// WorldState tiles (including grass, food, walkability and house footprint)
/// are never painted or modified by this art. Soil is projected onto the
/// existing, editable V2 surface, so there is no hovering flat decal.
/// </summary>
public sealed partial class HouseYardArt3D : Node3D
{
    private static readonly Color Grass = new("#86ba64");
    private static readonly Color Sand = new("#e0c996");
    private static readonly Color Soil = new("#977954");
    private static readonly Color DarkSoil = new("#745a40");
    private static readonly Color DrySoil = new("#ad9168");
    private static readonly Color Stone = new("#8c8470");
    private static readonly Color Moss = new("#769367");

    private readonly StandardMaterial3D _material = new()
    {
        VertexColorUseAsAlbedo = true,
        AlbedoColor = Colors.White,
        Roughness = 1f,
        Metallic = 0f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };
    private readonly MeshInstance3D[] _yards = new MeshInstance3D[WorldState.MaxHouseCount];
    private readonly int[] _houseIds = new int[WorldState.MaxHouseCount];
    private readonly int[] _plotXs = new int[WorldState.MaxHouseCount];
    private readonly int[] _plotYs = new int[WorldState.MaxHouseCount];
    private readonly int[] _surfaceRevision = new int[WorldState.MaxHouseCount];
    private readonly bool[] _landscaped = new bool[WorldState.MaxHouseCount];
    private WorldState? _world;
    private VisualTerrain3D? _terrain;
    private Godot.Camera3D? _camera;

    public override void _Ready()
    {
        Name = "GenerationOneYards";
        for(int i=0;i<_yards.Length;i++)
        {
            var node = new MeshInstance3D
            {
                Name = $"Yard_{i+1}",
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _yards[i] = node;
            AddChild(node);
        }
    }

    public void BindWorld(WorldState world,VisualTerrain3D terrain,
        Godot.Camera3D camera)
    {
        _world = world;
        _terrain = terrain;
        _camera = camera;
        for(int i=0;i<_yards.Length;i++)
        {
            _yards[i].Visible = false;
            _yards[i].Mesh = null;
            _houseIds[i] = 0;
            _surfaceRevision[i] = -1;
            _landscaped[i] = false;
        }
        RefreshNow();
    }

    public void RefreshNow()
    {
        if(_world is null || _terrain is null || _camera is null)return;
        for(int i=0;i<_yards.Length;i++)
        {
            WoodenHouse? house = i < _world.Houses.Count
                ? _world.Houses[i] : null;
            bool active = house is not null &&
                house.Stage >= HouseStage.Foundation;

            if(!active)
            {
                if(_landscaped[i])
                    _terrain.InvalidateHouseYardFoliage(_plotXs[i],_plotYs[i]);
                _yards[i].Visible = false;
                _yards[i].Mesh = null;
                _landscaped[i] = false;
                _houseIds[i] = 0;
                continue;
            }

            // All paths point to the exact same 2x2 authoritative house plot;
            // decoration begins only after the real foundation exists.
            WoodenHouse site = house!;
            bool moved = _houseIds[i]!=site.Id ||
                _plotXs[i]!=site.X || _plotYs[i]!=site.Y;
            int visualRevision = _terrain.SurfaceRevisionInArea(
                site.X-1,site.Y-1,site.X+3,site.Y+5);

            if(moved || !_landscaped[i] ||
                _surfaceRevision[i]!=visualRevision)
            {
                if(moved && _landscaped[i])
                    _terrain.InvalidateHouseYardFoliage(_plotXs[i],_plotYs[i]);
                // No native geometry work on simulation ticks: this bounded
                // mesh rebuild occurs only when a house is created/removed,
                // or the nearby underlying terrain is actually edited.
                _yards[i].Mesh = BuildYard(site);
                if(moved || !_landscaped[i])
                    _terrain.InvalidateHouseYardFoliage(site.X,site.Y);

                _houseIds[i] = site.Id;
                _plotXs[i] = site.X;
                _plotYs[i] = site.Y;
                _landscaped[i] = true;
                // Invalidation itself can mark nearby display chunks, so
                // read the new stamp after it to avoid redundant rebuilding.
                _surfaceRevision[i] = _terrain.SurfaceRevisionInArea(
                    site.X-1,site.Y-1,site.X+3,site.Y+5);
            }

            var center=new Vector3(site.X+1f,site.GroundHeight,site.Y+1f);
            if(_camera.IsPositionBehind(center))
            {
                _yards[i].Visible = false;
                continue;
            }
            Vector2 projected=_camera.UnprojectPosition(center);
            Vector2 view=_camera.GetViewport().GetVisibleRect().Size;
            _yards[i].Visible = projected.X>=-125f &&
                projected.Y>=-125f && projected.X<=view.X+125f &&
                projected.Y<=view.Y+125f;
        }
    }

    private ArrayMesh BuildYard(WoodenHouse site)
    {
        // 0.20 unit squares: the whole small yard requires at most ~800
        // quads (rather than a new texture or one object for every pebble).
        const float step = .20f;
        const int xSegments = 17, zSegments = 27;
        float startX=site.X-.45f,startZ=site.Y-.46f;
        using var surface=new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(_material);
        for(int iz=0;iz<zSegments;iz++)
        for(int ix=0;ix<xSegments;ix++)
        {
            float wx=startX+ix*step,wz=startZ+iz*step;
            float cx=wx+step*.5f,cz=wz+step*.5f;
            if(!CanDraw(cx,cz,site))continue;
            float strength=EarthStrength(cx,cz,site);
            if(strength<.13f)continue;

            Vector3 a=GroundPoint(wx,wz);
            Vector3 b=GroundPoint(wx,wz+step);
            Vector3 c=GroundPoint(wx+step,wz);
            Vector3 d=GroundPoint(wx+step,wz+step);
            Color ca=EarthColor(wx,wz,site);
            Color cb=EarthColor(wx,wz+step,site);
            Color cc=EarthColor(wx+step,wz,site);
            Color cd=EarthColor(wx+step,wz+step,site);
            Triangle(surface,a,b,c,ca,cb,cc);
            Triangle(surface,c,b,d,cc,cb,cd);
        }

        // Flat, slightly irregular pebbles pressed into the entry approach.
        // Kept within the same one-draw ground mesh and away from the door.
        for(int i=0;i<23;i++)
        {
            float along=.20f+i*.105f;
            float z=site.Y+1.95f+along;
            float bend=.10f*MathF.Sin(along*1.75f+.2f);
            float offset=((i*11)%9-4)*.055f;
            float x=site.X+.5f+bend+offset;
            if(!CanDraw(x,z,site) || EarthStrength(x,z,site)<.20f)
                continue;
            float radius=.025f+(i%5)*.009f;
            Color color=i%5==0?Moss:i%3==0?Stone:DrySoil;
            Pebble(surface,x,z,radius,color);
        }

        // Short wear marks along the doorstep: no fence, new collider or
        // landscape changes that could block a builder's approach.
        for(int i=0;i<7;i++)
        {
            float x=site.X+.26f+i*.086f;
            float z=site.Y+2.05f+((i*3)%5)*.037f;
            if(!CanDraw(x,z,site))continue;
            Pebble(surface,x,z,.027f,i%2==0?DarkSoil:Stone);
        }
        return surface.Commit();
    }

    private bool CanDraw(float wx,float wz,WoodenHouse site)
    {
        if(_world is null)return false;
        int x=(int)MathF.Floor(wx),y=(int)MathF.Floor(wz);
        if(!_world.Contains(x,y))return false;
        Terrain kind=_world.Tile(x,y);
        if(kind is not (Terrain.Grass or Terrain.Sand))return false;

        // Keep the footpath off another house's logical footprint.
        foreach(WoodenHouse other in _world.Houses)
            if(other.Id!=site.Id &&
                wx>other.X-.12f && wx<other.X+2.12f &&
                wz>other.Y-.12f && wz<other.Y+2.12f)
                return false;
        return true;
    }

    private float EarthStrength(float wx,float wz,WoodenHouse site)
    {
        float lx=wx-(site.X+1f),lz=wz-(site.Y+1f);
        float xEdge=MathF.Max(0f,MathF.Abs(lx)-.77f);
        float zEdge=MathF.Max(0f,MathF.Abs(lz)-.74f);
        float ring=MathF.Sqrt(xEdge*xEdge+zEdge*zEdge);
        float irregular=.075f*MathF.Sin(wx*9.3f+wz*2.1f)+
            .045f*MathF.Cos(wz*11.2f-wx*4.5f);
        float apron=1f-Smooth(.13f,.82f,ring+irregular);

        float along=wz-(site.Y+1.82f);
        float center=site.X+.5f+.12f*MathF.Sin(along*1.6f+site.Id*.21f);
        float pathWidth=MathF.Abs(wx-center)+.05f*MathF.Sin(wz*13f+wx*5f);
        float path=(1f-Smooth(.24f,.53f,pathWidth))*
            Smooth(-.25f,.22f,along)*(1f-Smooth(2.05f,2.77f,along));

        // A readable worn soil lane extending out of the doorway, surrounded
        // by gradually greener soil rather than a rectangular painted tile.
        return Mathf.Clamp(MathF.Max(apron*.88f,path),0f,1f);
    }

    private Color EarthColor(float wx,float wz,WoodenHouse site)
    {
        float strength=EarthStrength(wx,wz,site);
        int ix=(int)MathF.Floor(wx*9f),iz=(int)MathF.Floor(wz*9f);
        uint hash=EnvironmentArtMath.Hash(ix,iz,site.Id*37);
        float grain=((hash&31u)/31f-.5f)*.11f;
        bool sand=_world!.Tile(
            Math.Clamp((int)MathF.Floor(wx),0,_world.Width-1),
            Math.Clamp((int)MathF.Floor(wz),0,_world.Height-1))==Terrain.Sand;
        Color baseColor=sand?Sand:Grass;
        Color earth=(hash%11u) switch
        {
            0u or 1u=>DrySoil,
            2u=>DarkSoil,
            _=>Soil
        };
        return baseColor.Lerp(earth,
            Mathf.Clamp(strength*.82f+grain*strength,0f,1f));
    }

    private Vector3 GroundPoint(float wx,float wz)
    {
        int x=(int)MathF.Floor(wx),z=(int)MathF.Floor(wz);
        float height=_terrain!.SurfaceHeightAt(
            x,z,wx-x,wz-z);
        return new Vector3(wx,height+.021f,wz);
    }

    private void Pebble(SurfaceTool surface,float x,float z,
        float radius,Color color)
    {
        Vector3 center=GroundPoint(x,z)+Vector3.Up*.016f;
        Vector3 a=GroundPoint(x-radius,z-radius*.38f)+Vector3.Up*.016f;
        Vector3 b=GroundPoint(x+radius,z-radius*.27f)+Vector3.Up*.016f;
        Vector3 c=GroundPoint(x+radius*.63f,z+radius*.80f)+Vector3.Up*.016f;
        Vector3 d=GroundPoint(x-radius*.78f,z+radius*.57f)+Vector3.Up*.016f;
        // Upward-facing winding matches the existing terrain normals.
        Triangle(surface,a,center,b,color,color,color);
        Triangle(surface,b,center,c,color,color,color);
        Triangle(surface,c,center,d,color,color,color);
        Triangle(surface,d,center,a,color,color,color);
    }

    private static float Smooth(float a,float b,float value)
    {
        float t=Mathf.Clamp((value-a)/(b-a),0f,1f);
        return t*t*(3f-2f*t);
    }

    private static void Triangle(SurfaceTool surface,Vector3 a,Vector3 b,
        Vector3 c,Color ca,Color cb,Color cc)
    {
        Vector3 n=(b-a).Cross(c-a).Normalized();
        surface.SetNormal(n);
        surface.SetColor(ca);surface.AddVertex(a);
        surface.SetColor(cb);surface.AddVertex(b);
        surface.SetColor(cc);surface.AddVertex(c);
    }
}
