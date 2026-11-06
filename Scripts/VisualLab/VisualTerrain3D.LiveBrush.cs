using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Immediate visual painting for the ordinary V2-compatible H: launcher.
/// A short-lived terrain-only overlay follows authoritative WorldState edits.
/// It is deliberately separate from expensive Kenney foliage and the
/// full terrain/foliage publication queue. All native calls run on Godot's
/// main thread; no tasks, threads or added frame-time allocations per cell.
/// </summary>
public partial class VisualTerrain3D
{
    private readonly Dictionary<(int X,int Y),MeshInstance3D> _liveBrushMeshes=new();
    private readonly HashSet<(int X,int Y)> _liveBrushToRefresh=new();

    /// <summary>Queue already-changed logical chunks; never generate meshes in mouse callbacks.</summary>
    private void QueueLiveBrushPreview(int cx,int cy)
    {
        if(!_interactiveEditBatch || MeshWorkersEnabled || ViewportStreamingEnabled)return;
        _liveBrushToRefresh.Add((cx,cy));
    }

    /// <summary>
    /// Run once per rendered frame. At most a few simple terrain meshes are
    /// published here; foliage remains untouched until separately admitted.
    /// The closest edited chunk is published first, so the cursor never
    /// waits for previous work from the far end of a long stroke.
    /// </summary>
    public void PresentLiveBrushPreview(int maxChunks=3)
    {
        if(_world is null || MeshWorkersEnabled || ViewportStreamingEnabled ||
            _liveBrushToRefresh.Count==0)return;
        int published=0;
        while(published<maxChunks && _liveBrushToRefresh.Count>0)
        {
            (int X,int Y) selected=default;
            int best=int.MaxValue;
            bool found=false;
            foreach(var key in _liveBrushToRefresh)
            {
                int distance=Math.Abs(key.X-_interactiveLastChunkX)+
                    Math.Abs(key.Y-_interactiveLastChunkY);
                // Offscreen chunks cannot contribute to feedback under cursor.
                if(!_chunks.ContainsKey(key))distance+=100000;
                if(found && distance>=best)continue;
                best=distance;
                selected=key;
                found=true;
            }
            if(!found)break;
            _liveBrushToRefresh.Remove(selected);
            if(_chunks.ContainsKey(selected))
                BuildLiveBrushMesh(selected);
            published++;
        }
    }

    /// <summary>
    /// Reuse the exact approved V2 terrain top-fan geometry and vertex colors.
    /// An old terrain chunk is hidden only after its replacement is complete,
    /// so even a newly painted water tile shows through immediately.
    /// Foliage remains on its ordinary bounded publication path.
    /// </summary>
    private void BuildLiveBrushMesh((int X,int Y) key)
    {
        using var surface=new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(_earthMaterial);
        int x0=key.X*ChunkSize,y0=key.Y*ChunkSize;
        int xMax=Math.Min(_world.Width,x0+ChunkSize);
        int yMax=Math.Min(_world.Height,y0+ChunkSize);
        int landCells=0;

        for(int y=y0;y<yMax;y++)
        for(int x=x0;x<xMax;x++)
        {
            Terrain kind=_tiles.Tile(x,y);
            if(kind is Terrain.DeepWater or Terrain.ShallowWater)continue;
            landCells++;
            Color color=TerrainColors[(int)kind];
            float h=HeightAt(x,y);
            if(SmoothLandEnabled)
            {
                AddSmoothTopFace(surface,x,y,color);
                DrawSmoothCoastSkirts(surface,x,y);
            }
            else
            {
                Vector3 a=new(x,h,y),b=new(x,h,y+1);
                Vector3 c=new(x+1,h,y),d=new(x+1,h,y+1);
                if(RichTerrainDetails)
                    AddTopFace(surface,x,y,h,color,a,b,c,d);
                else
                    AddQuad(surface,a,b,c,d,color,Vector3.Up);
                // The old terraced relief can have vertical cliff faces.
                // This matches its terrain silhouette during held painting.
                Color bank=kind==Terrain.Mountain?new Color("#5a5d62"):
                    kind==Terrain.Sand?new Color("#b89c69"):new Color("#77654e");
                float west=x>0?HeightAt(x-1,y):.12f;
                float east=x<_world.Width-1?HeightAt(x+1,y):.12f;
                float north=y>0?HeightAt(x,y-1):.12f;
                float south=y<_world.Height-1?HeightAt(x,y+1):.12f;
                uint variation=Hash(x,y,_world.Seed+309);
                if(h>west+.02f)
                    DrawCliff(surface,new(x,west,y),new(x,west,y+1),
                        new(x,h,y),new(x,h,y+1),bank,Vector3.Left,variation);
                if(h>east+.02f)
                    DrawCliff(surface,new(x+1,east,y+1),new(x+1,east,y),
                        new(x+1,h,y+1),new(x+1,h,y),bank,Vector3.Right,variation);
                if(h>north+.02f)
                    DrawCliff(surface,new(x+1,north,y),new(x,north,y),
                        new(x+1,h,y),new(x,h,y),bank,Vector3.Back,variation);
                if(h>south+.02f)
                    DrawCliff(surface,new(x,south,y+1),new(x+1,south,y+1),
                        new(x,h,y+1),new(x+1,h,y+1),bank,Vector3.Forward,variation);
            }
        }

        Mesh? mesh=landCells>0?surface.Commit():null;
        if(landCells>0 && (mesh is null || mesh.GetSurfaceCount()==0))
            throw new InvalidOperationException("Brush preview terrain mesh failed.");
        if(!_liveBrushMeshes.TryGetValue(key,out MeshInstance3D? preview))
        {
            preview=new MeshInstance3D
            {
                Name=$"LiveBrush_{key.X}_{key.Y}",
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,
                Visible=false
            };
            AddChild(preview);
            _liveBrushMeshes.Add(key,preview);
        }
        preview.Mesh=mesh;
        preview.Visible=landCells>0;
        // Hide only the old ground, not the whole foliage ecosystem or houses.
        if(_chunks.TryGetValue(key,out MeshInstance3D? oldTerrain))
            oldTerrain.Visible=false;
    }

    /// <summary>
    /// Called *after* the authoritative terrain AND vegetation chunk have
    /// been rebuilt. The overlay never disappears while the world still
    /// shows stale pre-stroke geometry; no release-frame flash or holes.
    /// </summary>
    private void CompleteLiveBrushChunk((int X,int Y) key)
    {
        _liveBrushToRefresh.Remove(key);
        if(_liveBrushMeshes.Remove(key,out MeshInstance3D? overlay))
        {
            overlay.Visible=false;
            overlay.QueueFree();
        }
    }

    private void ResetLiveBrushPreviews()
    {
        foreach(MeshInstance3D overlay in _liveBrushMeshes.Values)
        {
            overlay.Visible=false;
            overlay.QueueFree();
        }
        _liveBrushMeshes.Clear();
        _liveBrushToRefresh.Clear();
    }
}
