using System;
using System.Collections.Generic;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// House-yard visual invalidation only. Nothing here edits biomes, resources,
/// walkability, saved state or the independently simulated people.
/// </summary>
public partial class VisualTerrain3D
{
    private readonly Dictionary<(int X,int Y),int> _surfaceEditStamps=new();
    private int _nextSurfaceEditStamp;

    private void ResetSurfaceEditStamps()
    {
        _surfaceEditStamps.Clear();
        _nextSurfaceEditStamp=0;
    }

    private void StampSurfaceEditChunk(int cx,int cy)
    {
        _surfaceEditStamps[(cx,cy)]=unchecked(++_nextSurfaceEditStamp);
    }

    /// <summary>
    /// A small constant-work fingerprint over the display chunks which touch
    /// a cottage's local apron. Far-away painting never rebuilds its yard.
    /// </summary>
    public int SurfaceRevisionInArea(int left,int top,int right,int bottom)
    {
        if(_world is null)return 0;
        left=Math.Clamp(left,0,_world.Width-1);
        top=Math.Clamp(top,0,_world.Height-1);
        right=Math.Clamp(right,0,_world.Width-1);
        bottom=Math.Clamp(bottom,0,_world.Height-1);
        if(right<left || bottom<top)return 0;
        int stamp=17;
        for(int cy=top/ChunkSize;cy<=bottom/ChunkSize;cy++)
        for(int cx=left/ChunkSize;cx<=right/ChunkSize;cx++)
        {
            _surfaceEditStamps.TryGetValue((cx,cy),out int revision);
            stamp=unchecked(stamp*31+revision);
        }
        return stamp;
    }

    /// <summary>
    /// Grass/flowers are just decorative meshes. Keep the house's existing
    /// walkable entrance clear and reduce only little decorative plants on
    /// its footprint/first few approach tiles. Never suppress actual wood
    /// trees, harvestable berry sources or the biome itself.
    /// </summary>
    private bool SuppressHouseDecorativeGroundCover(int x,int y)
    {
        if(_world is null || !_world.AutonomousHousingEnabled)return false;
        foreach(WoodenHouse house in _world.Houses)
        {
            if(house.Stage<HouseStage.Foundation)continue;
            if(x>=house.X && x<=house.X+1 &&
                y>=house.Y && y<=house.Y+2)
                return true;
            if(x==house.EntranceX && y==house.EntranceY+1)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Stage creation/demolition changes the decorative grass mask even
    /// though the actual terrain biome remains the same. Enqueue an ordinary
    /// bounded 16x16 art refresh once, not during every simulation tick.
    /// </summary>
    public void InvalidateHouseYardFoliage(int x,int y)
    {
        if(_world is null)return;
        MarkChanged(x+1,y+1,3,coastlineChanged:false);
    }
}
