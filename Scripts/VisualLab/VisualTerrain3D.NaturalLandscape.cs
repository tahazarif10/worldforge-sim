using System;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Manual V2 landscape presentation. Changes neither simulation tiles nor
/// picked geometry. Final and live brush meshes use the same material.
/// </summary>
public partial class VisualTerrain3D
{
    private ShaderMaterial? _naturalGround;
    private ImageTexture? _naturalBiomeTexture;
    private byte[] _naturalBiomeBytes=Array.Empty<byte>();
    private WorldState? _naturalBoundWorld;
    private int _naturalPaintStamp=int.MinValue;
    private readonly Vector4[] _naturalHousePlots=new Vector4[WorldState.MaxHouseCount];
    private static readonly string[] NaturalHouseUniforms={"house_a","house_b","house_c"};

    internal void ConfigureNaturalLandscape(WorldState world)
    {
        if(!NaturalGroundCover.Enabled)
        {
            SetProcess(false);
            return;
        }
        SetProcess(true);
        _naturalGround ??= new ShaderMaterial
        {
            Shader=GD.Load<Shader>("res://Assets/Shaders/natural_meadow.gdshader")
        };
        if(!ReferenceEquals(_naturalBoundWorld,world))
        {
            _naturalBoundWorld=world;
            _naturalBiomeBytes=new byte[checked(world.Width*world.Height*4)];
            _naturalBiomeTexture=null;
            _naturalPaintStamp=int.MinValue;
            _naturalGround.SetShaderParameter("map_size",new Vector2(world.Width,world.Height));
            _naturalGround.SetShaderParameter("seed_offset",(float)(world.Seed&4095));
            for(int i=0;i<_naturalHousePlots.Length;i++)
            {
                _naturalHousePlots[i]=Vector4.Zero;
                _naturalGround.SetShaderParameter(NaturalHouseUniforms[i],Vector4.Zero);
            }
        }
        _natureArt?.ConfigureNaturalGround(world,this);
        UpdateNaturalBiomeMap();
        UpdateNaturalHouseUniforms();
        ApplyNaturalGroundMaterials();
    }

    // Parent VisualLab still owns simulation and mesh publication. Run after
    // it to bind the material to new/live native surfaces before rendering.
    public override void _Process(double delta)
    {
        if(_naturalGround is null || _naturalBoundWorld is null ||
            !ReferenceEquals(_naturalBoundWorld,_world))return;
        UpdateNaturalBiomeMap();
        UpdateNaturalHouseUniforms();
        ApplyNaturalGroundMaterials();
    }

    private void UpdateNaturalBiomeMap()
    {
        if(_naturalGround is null || _naturalBoundWorld is null)return;
        if(_naturalPaintStamp==_nextSurfaceEditStamp && _naturalBiomeTexture is not null)return;
        _naturalPaintStamp=_nextSurfaceEditStamp;
        WorldState world=_naturalBoundWorld;
        bool changed=_naturalBiomeTexture is null;
        // Reuse the small biome-data buffer. Only scan on terrain edits;
        // upload only when biome bytes actually differ (not merely a sculpt).
        for(int z=0;z<world.Height;z++)
        for(int x=0;x<world.Width;x++)
        {
            int offset=(z*world.Width+x)*4;
            Terrain tile=world.Tile(x,z);
            byte r=tile==Terrain.Grass ? (byte)255 : (byte)0;
            byte g=tile==Terrain.Forest ? (byte)255 : (byte)0;
            byte b=tile==Terrain.Sand ? (byte)255 : (byte)0;
            if(_naturalBiomeBytes[offset]!=r || _naturalBiomeBytes[offset+1]!=g ||
                _naturalBiomeBytes[offset+2]!=b)changed=true;
            _naturalBiomeBytes[offset]=r;
            _naturalBiomeBytes[offset+1]=g;
            _naturalBiomeBytes[offset+2]=b;
            _naturalBiomeBytes[offset+3]=255;
        }
        if(!changed)return;
        using Image image=Image.CreateFromData(world.Width,world.Height,false,
            Image.Format.Rgba8,_naturalBiomeBytes);
        if(_naturalBiomeTexture is null)
        {
            _naturalBiomeTexture=ImageTexture.CreateFromImage(image);
            _naturalGround.SetShaderParameter("biome_map",_naturalBiomeTexture);
        }
        else _naturalBiomeTexture.Update(image);
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
        foreach(MeshInstance3D node in _chunks.Values)
            if(node.MaterialOverride!=_naturalGround)node.MaterialOverride=_naturalGround;
        foreach(MeshInstance3D node in _liveBrushMeshes.Values)
            if(node.MaterialOverride!=_naturalGround)node.MaterialOverride=_naturalGround;
        foreach(var fallback in _viewportFallbacks.Values)
            if(fallback.Node is MeshInstance3D node && node.MaterialOverride!=_naturalGround)
                node.MaterialOverride=_naturalGround;
    }

    internal bool NaturalPlantAllowedAt(int x,int z)
    {
        if(_world is null || !_world.Contains(x,z) ||
            SuppressHouseDecorativeGroundCover(x,z))return false;
        // Do not stack a decorative flower inside a real berry bush.
        if(_world.Resources.At(x,z).Kind==ResourceKind.Food)return false;
        foreach(WoodenHouse h in _world.Houses)
        {
            if(h.Stage<HouseStage.Foundation)continue;
            if(x>=h.X && x<=h.X+1 && z>=h.Y && z<=h.Y+2)return false;
            if(x==h.EntranceX && z<=h.EntranceY+2 && z>=h.EntranceY)return false;
        }
        return true;
    }
}
