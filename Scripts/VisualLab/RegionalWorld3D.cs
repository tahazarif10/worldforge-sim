using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using WorldSim;
using WorldSim.Regions;
using WorldForge.Streaming;

namespace WorldForge.VisualLab;

/// <summary>
/// Separate V3 regional-world proof, NOT a replacement for VisualLab3D.
/// An 8192x8192 LOGICAL address space with bounded visible render chunks
/// and bounded resident 64x64 biome-region arrays. No global terrain array.
/// Simulation citizens and old full-world water mask are deliberately absent
/// until those systems are region-aware too.
/// </summary>
public partial class RegionalWorld3D : Node3D
{
    private const int WorldSize = 8192;
    private const int ChunkSize = 16;
    private const int Seed = 20261030;
    private const int MaxVisibleChunks = 90;
    private readonly RegionStore _biomes = new(Seed, maxResidentRegions: 24);
    private readonly Dictionary<(int X, int Y), Node3D> _renderChunks = new();
    private readonly HashSet<(int X, int Y)> _dirtyChunks = new();
    private readonly WorldForge.Instrumentation.FrameTimeProbe _frames = new(7200);
    private readonly StandardMaterial3D _landMaterial = new()
    {
        VertexColorUseAsAlbedo = true,
        AlbedoColor = Colors.White,
        Roughness = 0.9f,
        Metallic = 0f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private NatureArtLibrary _art = null!;
    private ShaderMaterial _waterMaterial = null!;
    private Godot.Camera3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private WorldForge.Camera3D.StrategyCameraRig _rig = null!;
    private Label _legend = null!;
    private double _refresh;
    private bool _eco = true;
    private Terrain _paint = Terrain.Forest;
    private int _pending;
    private long _builds;

    public override void _Ready()
    {
        Name = "WorldForge — 8192 Regional World Lab (data/render proof)";
        var sky = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#bfd9df"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#d9eddf"),
                AmbientLightEnergy = .79f
            }
        };
        AddChild(sky);
        _sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -30, 13),
            LightColor = new Color("#ffe8c4"),
            LightEnergy = 1.12f,
            ShadowEnabled = false
        };
        AddChild(_sun);
        _camera = new Godot.Camera3D
        {
            Projection = Godot.Camera3D.ProjectionType.Orthogonal,
            Size = 79f,
            Near = .05f,
            Far = 550f,
            Current = true
        };
        AddChild(_camera);
        _rig = new WorldForge.Camera3D.StrategyCameraRig(
            _camera, (x,z) => HeightAt(x,z),
            WorldSize, WorldSize,
            (x,z) => LiftAt(x,z),
            () => true,
            (x,z,u,v) => SurfaceAt(x,z,u,v));
        _art = new NatureArtLibrary();
        _waterMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://Assets/Shaders/regional_water.gdshader")
        };

        var hud = new CanvasLayer { Name = "RegionalDebugHud" };
        AddChild(hud);
        _legend = new Label
        {
            Position = new Vector2(18, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hud.AddChild(_legend);
        _legend.AddThemeColorOverride("font_color", Colors.White);
        _legend.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _legend.AddThemeConstantOverride("shadow_offset_x", 2);
        _legend.AddThemeConstantOverride("shadow_offset_y", 2);
        RefreshVisible(16);
        RefreshLegend();
    }

    private Terrain At(int x, int z) => _biomes.Tile(new CellCoord(x,z));

    private float HeightAt(int x, int z)
    {
        Terrain tile = At(Math.Clamp(x,0,WorldSize-1), Math.Clamp(z,0,WorldSize-1));
        return tile switch
        {
            Terrain.DeepWater or Terrain.ShallowWater => .12f,
            Terrain.Sand => .29f,
            Terrain.Grass => .61f,
            Terrain.Forest => .68f,
            Terrain.Burned => .53f,
            Terrain.Mountain => 2.6f +
                (EnvironmentArtMath.Hash(x,z,Seed) % 11) * .17f,
            _ => .6f
        };
    }

    private float LiftAt(int x, int z) =>
        TerrainReliefMath.CenterLift(At(x,z),x,z,Seed);

    private float SurfaceAt(int x,int z,float u,float v)
    {
        Terrain kind = At(x,z);
        if (kind is Terrain.DeepWater or Terrain.ShallowWater) return .12f;
        return SharedCornerHeightfield.Tile(x,z,WorldSize,WorldSize,
            HeightAt,LiftAt(x,z)).Sample(u,v);
    }

    private static Color PaintColor(Terrain tile) => tile switch
    {
        Terrain.Sand => new Color("#e0c996"),
        Terrain.Grass => new Color("#86ba64"),
        Terrain.Forest => new Color("#426e51"),
        Terrain.Mountain => new Color("#89979f"),
        Terrain.Burned => new Color("#63534c"),
        _ => new Color("#337b9c")
    };

    private void BuildChunk(int cx,int cz)
    {
        var key=(cx,cz);
        var chunk=new Node3D { Name=$"RegionalChunk_{cx}_{cz}" };
        using var ground=new SurfaceTool();
        using var sea=new SurfaceTool();
        ground.Begin(Mesh.PrimitiveType.Triangles);
        sea.Begin(Mesh.PrimitiveType.Triangles);
        ground.SetMaterial(_landMaterial);
        sea.SetMaterial(_waterMaterial);
        bool landCount=false, waterCount=false;
        var plants=new List<NatureArtLibrary.Placement>();
        int xStart=cx*ChunkSize,zStart=cz*ChunkSize;
        for(int z=zStart;z<Math.Min(WorldSize,zStart+ChunkSize);z++)
        for(int x=xStart;x<Math.Min(WorldSize,xStart+ChunkSize);x++)
        {
            Terrain type=At(x,z);
            if(type is Terrain.DeepWater or Terrain.ShallowWater)
            {
                waterCount=true;
                Vector3 a=new(x,.12f,z),b=new(x,.12f,z+1);
                Vector3 c=new(x+1,.12f,z),d=new(x+1,.12f,z+1);
                Color tint=type==Terrain.DeepWater
                    ?new Color("#28718a"):new Color("#4aafae");
                DrawTriangle(sea,a,b,c,tint,tint,tint);
                DrawTriangle(sea,b,d,c,tint,tint,tint);
                continue;
            }
            landCount=true;
            SharedCornerHeightfield.Fan f=SharedCornerHeightfield.Tile(
                x,z,WorldSize,WorldSize,HeightAt,LiftAt(x,z));
            Vector3 p0=new(x,f.A,z),p1=new(x,f.B,z+1);
            Vector3 p2=new(x+1,f.C,z),p3=new(x+1,f.D,z+1);
            Vector3 center=new(x+.5f,f.Center,z+.5f);
            Color col=PaintColor(type);
            Color c0=ToColor(EnvironmentArtMath.CornerColor(x,z,0,0,WorldSize,WorldSize,At,Seed));
            Color c1=ToColor(EnvironmentArtMath.CornerColor(x,z,0,1,WorldSize,WorldSize,At,Seed));
            Color c2=ToColor(EnvironmentArtMath.CornerColor(x,z,1,0,WorldSize,WorldSize,At,Seed));
            Color c3=ToColor(EnvironmentArtMath.CornerColor(x,z,1,1,WorldSize,WorldSize,At,Seed));
            DrawTriangle(ground,p0,p1,center,c0,c1,col);
            DrawTriangle(ground,p1,p3,center,c1,c3,col);
            DrawTriangle(ground,p3,p2,center,c3,c2,col);
            DrawTriangle(ground,p2,p0,center,c2,c0,col);

            // Forest scatter remains authored GLB meshes, not spheres;
            // only in camera-resident chunks, bounded by deterministic test.
            uint h=EnvironmentArtMath.Hash(x,z,Seed+72);
            int species=NatureScatter.Pick(type,h);
            if(species>=0 && plants.Count<100 &&
               _art.Has((NatureArtLibrary.Species)species))
            {
                float u=.25f+(h%49)*.01f;
                float v=.25f+((h>>8)%49)*.01f;
                float y=SurfaceAt(x,z,u,v);
                float baseHeight=type==Terrain.Mountain?.90f:
                    type==Terrain.Forest?2.4f:.48f;
                plants.Add(new NatureArtLibrary.Placement(
                    (NatureArtLibrary.Species)species,
                    new Vector3(x+u,y,z+v),
                    baseHeight*NatureScatter.HeightFactor(h),
                    NatureScatter.RotationRadians(h)));
            }
        }
        if(landCount)
        {
            var terrain=new MeshInstance3D
            {
                Name="Land",
                Mesh=ground.Commit()
            };
            chunk.AddChild(terrain);
        }
        if(waterCount)
        {
            var ocean=new MeshInstance3D
            {
                Name="RegionalWater",
                Mesh=sea.Commit(),
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off
            };
            chunk.AddChild(ocean);
        }
        _art.Populate(chunk,plants);
        _renderChunks[key]=chunk;
        AddChild(chunk);
        _builds++;
    }

    private static Color ToColor(EnvironmentArtMath.Rgb rgb) =>
        new(rgb.R/255f,rgb.G/255f,rgb.B/255f);

    private static void DrawTriangle(SurfaceTool surface,
        Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
    {
        Vector3 normal=(b-a).Cross(c-a).Normalized();
        surface.SetNormal(normal);
        surface.SetColor(ca);surface.AddVertex(a);
        surface.SetColor(cb);surface.AddVertex(b);
        surface.SetColor(cc);surface.AddVertex(c);
    }

    private void RefreshVisible(int budget)
    {
        Vector2 viewport=_camera.GetViewport().GetVisibleRect().Size;
        if(viewport.X<1||viewport.Y<1)return;
        float left=float.PositiveInfinity,top=float.PositiveInfinity;
        float right=float.NegativeInfinity,bottom=float.NegativeInfinity;
        foreach(Vector2 pixel in new Vector2[]{Vector2.Zero,
            new(viewport.X,0),new(0,viewport.Y),viewport})
        {
            Vector3 origin=_camera.ProjectRayOrigin(pixel);
            Vector3 ray=_camera.ProjectRayNormal(pixel);
            if(Mathf.Abs(ray.Y)<.00001f)continue;
            foreach(float elevation in new float[]{.12f,5f})
            {
                float distance=(elevation-origin.Y)/ray.Y;
                if(!float.IsFinite(distance)||distance<0)continue;
                Vector3 position=origin+distance*ray;
                left=MathF.Min(left,position.X);
                right=MathF.Max(right,position.X);
                top=MathF.Min(top,position.Z);
                bottom=MathF.Max(bottom,position.Z);
            }
        }
        if(!float.IsFinite(left))return;
        var desired=VisibleChunkPlanner.Plan(WorldSize,WorldSize,ChunkSize,
            left,top,right,bottom,marginChunks:1);
        // The limiting count is for the entire view, not just each update.
        // In the 8192 logical world we refuse to allocate an unbounded
        // dense panorama even if the user resizes the viewport.
        if(desired.Count>MaxVisibleChunks)
        {
            var ordered=new List<(int X,int Y)>(desired);
            float cx=(left+right)*.5f,cz=(top+bottom)*.5f;
            ordered.Sort((a,b)=>
            {
                float da=(a.X*ChunkSize-cx)*(a.X*ChunkSize-cx)+
                         (a.Y*ChunkSize-cz)*(a.Y*ChunkSize-cz);
                float db=(b.X*ChunkSize-cx)*(b.X*ChunkSize-cx)+
                         (b.Y*ChunkSize-cz)*(b.Y*ChunkSize-cz);
                return da.CompareTo(db);
            });
            desired=new HashSet<(int X,int Y)>(ordered.GetRange(0,MaxVisibleChunks));
        }

        foreach(var key in new List<(int X,int Y)>(_renderChunks.Keys))
        {
            if(desired.Contains(key))continue;
            _renderChunks[key].QueueFree();
            _renderChunks.Remove(key);
            _dirtyChunks.Remove(key);
        }
        var current=new HashSet<(int X,int Y)>(_renderChunks.Keys);
        foreach(var dirty in _dirtyChunks)current.Remove(dirty);
        var missing=ChunkLoadBudget.Choose(desired,current,
            (left+right)*.5f,(top+bottom)*.5f,ChunkSize,budget);
        foreach(var key in missing)
        {
            if(_renderChunks.Remove(key,out Node3D? old)) old.QueueFree();
            BuildChunk(key.X,key.Y);
            _dirtyChunks.Remove(key);
        }
        _pending=desired.Count-_renderChunks.Count+_dirtyChunks.Count;
    }

    private void Invalidate(int x,int z,int radius)
    {
        int l=Math.Max(0,x-radius-1),r=Math.Min(WorldSize-1,x+radius+1);
        int t=Math.Max(0,z-radius-1),b=Math.Min(WorldSize-1,z+radius+1);
        for(int cz=t/ChunkSize;cz<=b/ChunkSize;cz++)
        for(int cx=l/ChunkSize;cx<=r/ChunkSize;cx++)
        {
            var key=(cx,cz);
            if(_renderChunks.ContainsKey(key))_dirtyChunks.Add(key);
        }
    }

    public override void _Process(double delta)
    {
        _frames.Record(delta*1000);
        Vector2 keys=Vector2.Zero;
        if(Input.IsKeyPressed(Key.A)||Input.IsKeyPressed(Key.Left))keys.X-=1;
        if(Input.IsKeyPressed(Key.D)||Input.IsKeyPressed(Key.Right))keys.X+=1;
        if(Input.IsKeyPressed(Key.W)||Input.IsKeyPressed(Key.Up))keys.Y+=1;
        if(Input.IsKeyPressed(Key.S)||Input.IsKeyPressed(Key.Down))keys.Y-=1;
        if(keys.LengthSquared()>.01f)_rig.PanByKeyboard(keys,delta);
        _refresh+=delta;
        if(_refresh>=.05)
        {
            _refresh=0;
            RefreshVisible(_eco?2:4);
            RefreshLegend();
        }
    }

    private void RefreshLegend()
    {
        if(_legend is null)return;
        _legend.Text=
            $"V3 REGION LAB  |  Logical world {WorldSize} x {WorldSize} | " +
            $"seed {Seed}\n" +
            $"Resident render chunks {_renderChunks.Count}/{MaxVisibleChunks}  " +
            $"pending {_pending}  |  biome regions in RAM {_biomes.ResidentRegions}/24\n" +
            $"Edited regions {_biomes.ModifiedRegions} | mesh builds {_builds}  " +
            $"mode {(_eco?"Eco":"Balanced")} | brush {_paint}\n" +
            "Right-drag/WASD: move  | Wheel: zoom | Left-click: paint\n" +
            "1 Grass, 2 Forest, 3 Mountain, 4 Ocean, 5 Sand | F4 Eco\n" +
            "Home / End / PageUp / PageDown: teleport across large map\n" +
            "F11: save edited biomes | F12: load | F8 screenshot | F9 frame CSV | Esc: exit";
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(input is InputEventMouseButton mouse)
        {
            if(mouse.ButtonIndex==MouseButton.WheelUp&&mouse.Pressed)
                _rig.ZoomAt(mouse.Position,1f/1.14f);
            else if(mouse.ButtonIndex==MouseButton.WheelDown&&mouse.Pressed)
                _rig.ZoomAt(mouse.Position,1.14f);
            else if(mouse.ButtonIndex==MouseButton.Right)
            {
                if(mouse.Pressed)_rig.BeginDrag(mouse.Position);
                else _rig.EndDrag();
            }
            else if(mouse.ButtonIndex==MouseButton.Left&&mouse.Pressed)
                PaintAt(mouse.Position);
        }
        else if(input is InputEventMouseMotion motion)
        {
            if(Input.IsMouseButtonPressed(MouseButton.Right))
                _rig.DragTo(motion.Position);
            else if(Input.IsMouseButtonPressed(MouseButton.Left))
                PaintAt(motion.Position);
        }
        else if(input is InputEventKey key&&key.Pressed&&!key.Echo)
        {
            if(key.Keycode==Key.Key1)_paint=Terrain.Grass;
            if(key.Keycode==Key.Key2)_paint=Terrain.Forest;
            if(key.Keycode==Key.Key3)_paint=Terrain.Mountain;
            if(key.Keycode==Key.Key4)_paint=Terrain.DeepWater;
            if(key.Keycode==Key.Key5)_paint=Terrain.Sand;
            if(key.Keycode==Key.Home)_rig.FocusAt(96,96);
            if(key.Keycode==Key.End)_rig.FocusAt(WorldSize-96,WorldSize-96);
            if(key.Keycode==Key.Pageup)_rig.FocusAt(WorldSize-96,96);
            if(key.Keycode==Key.Pagedown)_rig.FocusAt(96,WorldSize-96);
            if(key.Keycode==Key.F4)
            {
                _eco=!_eco;
                _sun.ShadowEnabled=!_eco;
                GD.Print("Regional lab quality: "+(_eco?"Eco":"Balanced"));
            }
            if(key.Keycode==Key.F11)SaveBiomes();
            if(key.Keycode==Key.F12)LoadBiomes();
            if(key.Keycode==Key.F8)SaveArtifact(false);
            if(key.Keycode==Key.F9)SaveArtifact(true);
            if(key.Keycode==Key.F10)
                GD.Print($"Active {_renderChunks.Count}; pending {_pending}; " +
                    $"resident biome regions {_biomes.ResidentRegions}; " +
                    $"edited {_biomes.ModifiedRegions}; builds {_builds}.");
            if(key.Keycode==Key.Escape)GetTree().Quit();
            RefreshLegend();
        }
    }

    private void PaintAt(Vector2 pixel)
    {
        if(!_rig.TryPickTerrain(pixel,out var hit))return;
        const int radius=2;
        _biomes.PaintCircle(new CellCoord(hit.X,hit.Y),radius,_paint);
        Invalidate(hit.X,hit.Y,radius);
    }

    private string? OutputFolder(string type)
    {
        string root=ProjectSettings.GlobalizePath("res://");
        if(!root.StartsWith("H:\\",StringComparison.OrdinalIgnoreCase))
        {
            GD.PushError("H: portable-project policy: disk export refused.");
            return null;
        }
        return Path.Combine(root,"artifacts","regional-world",
            $"seed_{Seed}",type);
    }

    private void SaveBiomes()
    {
        string? folder=OutputFolder("biomes");
        if(folder is null)return;
        try {GD.Print($"Saved {BiomeRegionFiles.SaveModified(_biomes,folder)} " +
                       $"biome regions under {folder}");}
        catch(Exception error){GD.PushError("Region save failed: "+error.Message);}
    }
    private void LoadBiomes()
    {
        string? folder=OutputFolder("biomes");
        if(folder is null)return;
        try
        {
            int count=BiomeRegionFiles.LoadAll(_biomes,folder);
            foreach(var key in _renderChunks.Keys)_dirtyChunks.Add(key);
            GD.Print($"Loaded {count} edited biome regions from {folder}");
        }
        catch(Exception error){GD.PushError("Region load refused: "+error.Message);}
    }
    private void SaveArtifact(bool metrics)
    {
        string? folder=OutputFolder(metrics?"performance":"screenshots");
        if(folder is null)return;
        try
        {
            Directory.CreateDirectory(folder);
            string filename=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string path=Path.Combine(folder,"regional-"+filename+
                (metrics?".csv":".png"));
            if(metrics)
                File.WriteAllText(path,_frames.ExportCsv());
            else
            {
                Error result=GetViewport().GetTexture().GetImage().SavePng(path);
                if(result!=Error.Ok)throw new IOException("Godot screenshot error: "+result);
            }
            GD.Print("Saved regional "+(metrics?"frame timing":"screenshot")+
                " to "+path);
        }
        catch(Exception error){GD.PushError("Regional export failed: "+error.Message);}
    }
}
