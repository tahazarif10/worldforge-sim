using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Continuous meadow presentation for the existing manual 3D scene. Actual
/// editable terrain/picking remain in the V2 terrain adapter; no biome,
/// resource, citizen, save schema, water or building rules are changed here.
/// </summary>
public partial class VisualTerrain3D
{
    private ShaderMaterial? _naturalGround;
    private ImageTexture? _naturalBiomeTexture;
    private ImageTexture? _naturalHeightTexture;
    private ImageTexture? _naturalDetailTexture;
    private byte[] _naturalBiomeBytes=Array.Empty<byte>();
    private byte[] _naturalHeightBytes=Array.Empty<byte>();
    private float[] _naturalCornerHeights=Array.Empty<float>();
    private WorldState? _naturalBoundWorld;
    private int _naturalPaintStamp=int.MinValue;
    private (bool Smooth,bool Facets,int Regions) _naturalSurfaceFormat;
    private readonly Dictionary<(int X,int Y),int> _naturalObservedStamps=new();
    private readonly Vector4[] _naturalHousePlots=new Vector4[WorldState.MaxHouseCount];
    private static readonly string[] NaturalHouseUniforms={"house_a","house_b","house_c"};

    public override void _Ready()
    {
        // Parent VisualLab publishes live/final meshes first. Explicitly
        // enable this adapter instead of relying on a house already existing.
        ProcessPriority=20;
        SetProcess(NaturalGroundCover.Enabled);
    }

    internal void ConfigureNaturalLandscape(WorldState world)
    {
        if(!NaturalGroundCover.Enabled)return;
        if(!ReferenceEquals(world,_world))return;
        SetProcess(true);
        bool firstWorld=!ReferenceEquals(_naturalBoundWorld,world);
        bool rebuild=false;
        if(firstWorld)
        {
            // The old tiny pyramid at EVERY tile center creates a repeating
            // diamond even under a new texture. Turn off this decorative
            // relief in the manual natural-landscape view. Shared corners,
            // mountains' base heights and all stored sculpt offsets remain.
            // ReliefAt, drawing, citizens and ray picking share this flag.
            rebuild=SculptedFacets && _chunks.Count>0;
            SculptedFacets=false;
            _naturalBoundWorld=world;
            _naturalObservedStamps.Clear();
            _naturalPaintStamp=int.MinValue;
            _naturalBiomeBytes=new byte[checked(world.Width*world.Height*4)];
            int corners=checked((world.Width+1)*(world.Height+1));
            _naturalCornerHeights=new float[corners];
            _naturalHeightBytes=new byte[checked(corners*4)];
            _naturalBiomeTexture=null;
            _naturalHeightTexture=null;
        }
        if(_naturalGround is null)
        {
            Shader shader=GD.Load<Shader>("res://Assets/Shaders/natural_meadow.gdshader") ??
                throw new InvalidOperationException("Natural meadow shader is missing.");
            _naturalGround=new ShaderMaterial{Shader=shader};
            _naturalDetailTexture=MeadowSurfaceTexture.Create();
            _naturalGround.SetShaderParameter("meadow_detail",_naturalDetailTexture);
        }
        if(firstWorld)
        {
            _naturalGround.SetShaderParameter("map_size",new Vector2(world.Width,world.Height));
            _naturalGround.SetShaderParameter("seed_offset",(float)(world.Seed&4095));
            for(int i=0;i<_naturalHousePlots.Length;i++)
            {
                _naturalHousePlots[i]=Vector4.Zero;
                _naturalGround.SetShaderParameter(NaturalHouseUniforms[i],Vector4.Zero);
            }
        }
        _natureArt?.ConfigureNaturalGround(world,this);
        UpdateNaturalMaps(firstWorld);
        if(rebuild)RebuildResidentGeometry();
        UpdateNaturalHouseUniforms();
        ApplyNaturalGroundMaterials();
        if(firstWorld)
            GD.Print("MEADOW V2: continuous layered grass, corner-height lighting and live terrain binding active.");
    }

    public override void _Process(double delta)=>RefreshNaturalLandscape();

    internal void RefreshNaturalLandscape()
    {
        if(!NaturalGroundCover.Enabled || _world is null)return;
        if(_naturalGround is null || !ReferenceEquals(_naturalBoundWorld,_world))
        {
            ConfigureNaturalLandscape(_world);
            return;
        }
        UpdateNaturalMaps(false);
        UpdateNaturalHouseUniforms();
        ApplyNaturalGroundMaterials();
    }

    private void UpdateNaturalMaps(bool force)
    {
        if(_naturalGround is null || _naturalBoundWorld is null)return;
        WorldState world=_naturalBoundWorld;
        var format=(SmoothLandEnabled,SculptedFacets,ModifiedElevationRegions);
        force|=_naturalSurfaceFormat!=format || _naturalBiomeTexture is null ||
            _naturalHeightTexture is null;
        if(!force && _naturalPaintStamp==_nextSurfaceEditStamp)return;
        _naturalSurfaceFormat=format;
        bool biomeChanged=false,heightChanged=false;
        if(force)
        {
            UpdateNaturalMapArea(0,0,world.Width,world.Height,
                ref biomeChanged,ref heightChanged);
        }
        else
        {
            // Read only edited display chunks and a 1-cell normal halo.
            // No full-world SurfaceHeightAt scan on every brush frame.
            foreach(var pair in _surfaceEditStamps)
            {
                if(_naturalObservedStamps.TryGetValue(pair.Key,out int old) && old==pair.Value)
                    continue;
                int x=pair.Key.X*ChunkSize,z=pair.Key.Y*ChunkSize;
                UpdateNaturalMapArea(x-1,z-1,x+ChunkSize+1,z+ChunkSize+1,
                    ref biomeChanged,ref heightChanged);
            }
        }
        foreach(var pair in _surfaceEditStamps)_naturalObservedStamps[pair.Key]=pair.Value;
        _naturalPaintStamp=_nextSurfaceEditStamp;
        if(biomeChanged || _naturalBiomeTexture is null)
        {
            using var image=Image.CreateFromData(world.Width,world.Height,false,
                Image.Format.Rgba8,_naturalBiomeBytes);
            if(_naturalBiomeTexture is null)
                _naturalBiomeTexture=ImageTexture.CreateFromImage(image);
            else _naturalBiomeTexture.Update(image);
            _naturalGround.SetShaderParameter("biome_map",_naturalBiomeTexture);
        }
        if(heightChanged || _naturalHeightTexture is null)
        {
            using var image=Image.CreateFromData(world.Width+1,world.Height+1,false,
                Image.Format.Rf,_naturalHeightBytes);
            if(_naturalHeightTexture is null)
                _naturalHeightTexture=ImageTexture.CreateFromImage(image);
            else _naturalHeightTexture.Update(image);
            _naturalGround.SetShaderParameter("surface_heights",_naturalHeightTexture);
        }
    }

    private void UpdateNaturalMapArea(int left,int top,int right,int bottom,
        ref bool biomeChanged,ref bool heightChanged)
    {
        WorldState world=_naturalBoundWorld!;
        left=Math.Clamp(left,0,world.Width);right=Math.Clamp(right,0,world.Width);
        top=Math.Clamp(top,0,world.Height);bottom=Math.Clamp(bottom,0,world.Height);
        for(int z=top;z<=bottom;z++)
        for(int x=left;x<=right;x++)
        {
            if(x<world.Width && z<world.Height)
            {
                Terrain tile=world.Tile(x,z);
                byte r=tile==Terrain.Grass?(byte)255:(byte)0;
                byte g=tile==Terrain.Forest?(byte)255:(byte)0;
                byte b=tile==Terrain.Sand?(byte)255:(byte)0;
                int at=(z*world.Width+x)*4;
                if(_naturalBiomeBytes[at]!=r || _naturalBiomeBytes[at+1]!=g ||
                    _naturalBiomeBytes[at+2]!=b)biomeChanged=true;
                _naturalBiomeBytes[at]=r;_naturalBiomeBytes[at+1]=g;
                _naturalBiomeBytes[at+2]=b;_naturalBiomeBytes[at+3]=255;
            }
            int tx=Math.Min(x,world.Width-1),tz=Math.Min(z,world.Height-1);
            float height=SurfaceHeightAt(tx,tz,x-tx,z-tz);
            int corner=z*(world.Width+1)+x;
            if(_naturalCornerHeights[corner]==height)continue;
            _naturalCornerHeights[corner]=height;
            BitConverter.TryWriteBytes(_naturalHeightBytes.AsSpan(corner*4,4),height);
            heightChanged=true;
        }
    }

    internal void UpdateNaturalHouseUniforms()
    {
        if(_naturalGround is null || _naturalBoundWorld is null)return;
        var houses=_naturalBoundWorld.Houses;
        for(int i=0;i<_naturalHousePlots.Length;i++)
        {
            Vector4 next=Vector4.Zero;
            if(i<houses.Count && houses[i].Stage>=HouseStage.Foundation)
            {
                WoodenHouse h=houses[i];
                next=new Vector4(h.X,h.Y,1f,h.Id);
            }
            if(next==_naturalHousePlots[i])continue;
            _naturalHousePlots[i]=next;
            _naturalGround.SetShaderParameter(NaturalHouseUniforms[i],next);
        }
    }

    private void ApplyNaturalGroundMaterials()
    {
        if(_naturalGround is null)return;
        foreach(MeshInstance3D node in _chunks.Values)BindNaturalSurface(node);
        foreach(MeshInstance3D node in _liveBrushMeshes.Values)BindNaturalSurface(node);
        foreach(var fallback in _viewportFallbacks.Values)
            if(fallback.Node is MeshInstance3D node)BindNaturalSurface(node);
    }

    private void BindNaturalSurface(MeshInstance3D node)
    {
        // Binding at the native instance level survives mesh replacements.
        // Full chunks, worker fallback and held-brush previews never diverge.
        if(node.MaterialOverride!=_naturalGround)node.MaterialOverride=_naturalGround;
    }

    internal bool NaturalPlantAllowedAt(int x,int z)
    {
        if(_world is null || !_world.Contains(x,z) ||
            SuppressHouseDecorativeGroundCover(x,z))return false;
        if(_world.Resources.At(x,z).Kind==ResourceKind.Food)return false;
        foreach(WoodenHouse h in _world.Houses)
        {
            if(h.Stage<HouseStage.Foundation)continue;
            if(x>=h.X && x<=h.X+1 && z>=h.Y && z<=h.Y+2)return false;
            if(x==h.EntranceX && z>=h.EntranceY && z<=h.EntranceY+2)return false;
        }
        return true;
    }
}
