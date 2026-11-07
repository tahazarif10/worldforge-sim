using System;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Yard lifetime coordinator. The rejected tessellated overlay is removed:
/// soil and entry paths are now continuous colors on the real terrain material.
/// Only decorative foliage is invalidated on construction/demolition.
/// </summary>
public sealed partial class HouseYardArt3D : Node3D
{
    private readonly int[] _ids=new int[WorldState.MaxHouseCount];
    private readonly int[] _xs=new int[WorldState.MaxHouseCount];
    private readonly int[] _zs=new int[WorldState.MaxHouseCount];
    private WorldState? _world;
    private VisualTerrain3D? _terrain;

    public void BindWorld(WorldState world,VisualTerrain3D terrain,Godot.Camera3D camera)
    {
        _world=world;
        _terrain=terrain;
        Array.Clear(_ids,0,_ids.Length);
        terrain.ConfigureNaturalLandscape(world);
        RefreshNow();
    }

    public void RefreshNow()
    {
        if(_world is null || _terrain is null || !NaturalGroundCover.Enabled)return;
        for(int i=0;i<_ids.Length;i++)
        {
            WoodenHouse? house=i<_world.Houses.Count?_world.Houses[i]:null;
            bool active=house is not null && house.Stage>=HouseStage.Foundation;
            int id=active?house!.Id:0;
            int x=active?house!.X:0,z=active?house!.Y:0;
            if(id==_ids[i] && (!active || (x==_xs[i] && z==_zs[i])))continue;
            if(_ids[i]!=0)_terrain.InvalidateHouseYardFoliage(_xs[i],_zs[i]);
            if(active)_terrain.InvalidateHouseYardFoliage(x,z);
            _ids[i]=id;_xs[i]=x;_zs[i]=z;
        }
        _terrain.UpdateNaturalHouseUniforms();
    }
}
