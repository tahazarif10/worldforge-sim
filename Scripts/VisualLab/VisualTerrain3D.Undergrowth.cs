using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.VisualLab;

public partial class VisualTerrain3D
{
    /// <summary>
    /// Resolve the exact display-cell domain of a foliage publication. Normal
    /// chunks use their existing registry, not a parsed SceneTree node name.
    /// Staged worker roots may be unpublished; their immutable placement
    /// sites supply the same bounds. Neither path scans the whole world.
    /// </summary>
    internal bool TryGetUndergrowthBounds(Node3D root,
        IReadOnlyList<NatureArtLibrary.Placement> placements,out Rect2I bounds)
    {
        bounds=default;
        if(_world is null)return false;
        int cx=-1,cz=-1;
        foreach(var entry in _foliageChunks)
            if(ReferenceEquals(entry.Value,root))
            {
                cx=entry.Key.X;cz=entry.Key.Y;
                break;
            }
        if(cx<0 && placements.Count>0)
        {
            cx=Mathf.FloorToInt(placements[0].Position.X)/ChunkSize;
            cz=Mathf.FloorToInt(placements[0].Position.Z)/ChunkSize;
            // A future caller with a mixed-region placement list must not
            // accidentally generate another region's undergrowth here.
            foreach(var p in placements)
                if(Mathf.FloorToInt(p.Position.X)/ChunkSize!=cx ||
                    Mathf.FloorToInt(p.Position.Z)/ChunkSize!=cz)return false;
        }
        int left=cx*ChunkSize,top=cz*ChunkSize;
        if(cx<0 || cz<0 || left>=_world.Width || top>=_world.Height)return false;
        bounds=new Rect2I(left,top,Math.Min(ChunkSize,_world.Width-left),
            Math.Min(ChunkSize,_world.Height-top));
        return true;
    }
}
