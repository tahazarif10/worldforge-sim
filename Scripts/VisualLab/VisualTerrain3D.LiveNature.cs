using System;
using System.Collections.Generic;
using Godot;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// GPU-instanced, per-cell editable nature while a V2 brush is held.
/// The SAME imported Kenney meshes, native materials, deterministic scatter
/// and terrain height samples as the final foliage are reused here.
/// No GLB loading, packed-scene instantiation, or full 16x16 foliage rebuild
/// happens on a mouse move or after a touched chunk has been initialized.
/// </summary>
public partial class VisualTerrain3D
{
    private readonly Dictionary<(int X,int Y),LiveNatureChunk> _liveNature=new();

    private readonly record struct NatureEntry(int Cell,NatureArtLibrary.Placement Placement);

    private sealed class LiveNatureChunk(Node3D root)
    {
        internal readonly Node3D Root=root;
        internal readonly NatureArtLibrary.Placement?[] Cells=
            new NatureArtLibrary.Placement?[ChunkSize*ChunkSize];
        internal readonly Dictionary<NatureArtLibrary.Species,LiveSpeciesBatch> Batches=new();
    }

    /// <summary>
    /// One set of MultiMeshes per authored species. All parts share the same
    /// compact instance-slot table; on removal the last instance replaces
    /// the removed slot (O(number of model mesh parts), never 256 meshes).
    /// Growth is exponential and extremely rare after initial creation.
    /// </summary>
    private sealed class LiveSpeciesBatch
    {
        private readonly NatureArtLibrary _library;
        private readonly NatureArtLibrary.Species _species;
        private readonly MultiMesh[] _meshes;
        private readonly MultiMeshInstance3D[] _nodes;
        private readonly List<NatureEntry> _entries=new();
        private readonly Dictionary<int,int> _slotByCell=new();
        private int _capacity;

        internal LiveSpeciesBatch(NatureArtLibrary library,Node3D parent,
            NatureArtLibrary.Species species,int initialCapacity,bool eco)
        {
            _library=library;
            _species=species;
            int partCount=library.PartCount(species);
            _capacity=CapacityFor(initialCapacity);
            _meshes=new MultiMesh[partCount];
            _nodes=new MultiMeshInstance3D[partCount];
            for(int p=0;p<partCount;p++)
            {
                var part=library.Part(species,p);
                var mesh=new MultiMesh
                {
                    TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh=part.Mesh,
                    InstanceCount=_capacity,
                    VisibleInstanceCount=0
                };
                var node=new MultiMeshInstance3D
                {
                    Name=$"LiveCC0_{species}_{p}",
                    Multimesh=mesh,
                    MaterialOverride=part.Override,
                    CastShadow=eco || species is
                        NatureArtLibrary.Species.Grass or NatureArtLibrary.Species.Flower
                        ? GeometryInstance3D.ShadowCastingSetting.Off
                        : GeometryInstance3D.ShadowCastingSetting.On
                };
                parent.AddChild(node);
                _meshes[p]=mesh;
                _nodes[p]=node;
            }
        }

        private static int CapacityFor(int required)
        {
            int capacity=4;
            while(capacity<required && capacity<ChunkSize*ChunkSize)
                capacity*=2;
            return Math.Min(capacity,ChunkSize*ChunkSize);
        }

        private void Place(int slot,NatureArtLibrary.Placement placement)
        {
            for(int part=0;part<_meshes.Length;part++)
                _meshes[part].SetInstanceTransform(slot,
                    _library.PlacementTransform(placement,part));
        }

        private void UpdateVisibleCount()
        {
            foreach(MultiMesh mesh in _meshes)
                mesh.VisibleInstanceCount=_entries.Count;
        }

        private void EnsureCapacity(int desired)
        {
            if(desired<=_capacity)return;
            _capacity=CapacityFor(desired);
            if(_capacity<desired)
                throw new InvalidOperationException("Exceeded one editable nature site per cell.");
            // Godot clears MultiMesh transform storage on InstanceCount
            // expansion. Re-upload the small, packed existing list exactly.
            foreach(MultiMesh mesh in _meshes)
            {
                mesh.VisibleInstanceCount=0;
                mesh.InstanceCount=_capacity;
            }
            for(int index=0;index<_entries.Count;index++)
                Place(index,_entries[index].Placement);
            UpdateVisibleCount();
        }

        internal void PopulateInitial(IReadOnlyList<NatureEntry> initial)
        {
            // Bulk-upload first-touched chunks with only one native visible-
            // count update per mesh part, rather than one call per tree.
            EnsureCapacity(initial.Count);
            for(int i=0;i<initial.Count;i++)
            {
                NatureEntry entry=initial[i];
                _slotByCell.Add(entry.Cell,i);
                _entries.Add(entry);
                Place(i,entry.Placement);
            }
            UpdateVisibleCount();
        }

        internal void Insert(int cell,NatureArtLibrary.Placement placement)
        {
            if(_slotByCell.TryGetValue(cell,out int slot))
            {
                _entries[slot]=new NatureEntry(cell,placement);
                Place(slot,placement);
                return;
            }
            EnsureCapacity(_entries.Count+1);
            int index=_entries.Count;
            _entries.Add(new NatureEntry(cell,placement));
            _slotByCell.Add(cell,index);
            Place(index,placement);
            UpdateVisibleCount();
        }

        internal void Remove(int cell)
        {
            if(!_slotByCell.TryGetValue(cell,out int index))return;
            int lastIndex=_entries.Count-1;
            NatureEntry last=_entries[lastIndex];
            if(index!=lastIndex)
            {
                _entries[index]=last;
                _slotByCell[last.Cell]=index;
                Place(index,last.Placement);
            }
            _entries.RemoveAt(lastIndex);
            _slotByCell.Remove(cell);
            UpdateVisibleCount();
        }

        internal void SetEco(bool eco)
        {
            bool small=_species is NatureArtLibrary.Species.Grass or
                NatureArtLibrary.Species.Flower;
            foreach(MultiMeshInstance3D node in _nodes)
                node.CastShadow=(eco||small)
                    ? GeometryInstance3D.ShadowCastingSetting.Off
                    : GeometryInstance3D.ShadowCastingSetting.On;
        }
    }

    /// <summary>
    /// Deterministic authored placement for one logical cell, matching the
    /// active native V2 nature scatter. Missing GLBs are never fabricated:
    /// those rare optional/failed imports retain the established fallback
    /// path when the full foliage is built.
    /// </summary>
    private NatureArtLibrary.Placement? LiveNaturePlacement(int x,int y)
    {
        if(_natureArt is null || !_world.Contains(x,y))return null;
        Terrain kind=_tiles.Tile(x,y);
        if(kind==Terrain.Grass && SuppressHouseDecorativeGroundCover(x,y))
            return null;
        uint hash=Hash(x,y,_world.Seed+119);
        int species=NatureScatter.Pick(kind,hash);
        if(species<0)return null;

        float desiredHeight;
        float embed=0f;
        if(kind==Terrain.Forest && species<=5)
        {
            if(_world.Resources.HasStump(x,y))return null;
            desiredHeight=2.4f*NatureScatter.HeightFactor(hash);
        }
        else if(kind==Terrain.Mountain)
        {
            int step=(int)(Hash(x,y,_world.Seed)%11);
            if(!MountainSummitArtMath.MayHaveAccent(hash,step))return null;
            int exposed=0;
            if(x==0 || _tiles.Tile(x-1,y)!=Terrain.Mountain)exposed++;
            if(y==0 || _tiles.Tile(x,y-1)!=Terrain.Mountain)exposed++;
            if(x+1==_world.Width ||
                _tiles.Tile(x+1,y)!=Terrain.Mountain)exposed++;
            if(y+1==_world.Height ||
                _tiles.Tile(x,y+1)!=Terrain.Mountain)exposed++;
            MountainSummitArtMath.Accent accent=
                MountainSummitArtMath.Choose(hash,step,exposed);
            if(!accent.Visible)return null;
            species=(int)accent.Species;
            desiredHeight=accent.Height*NatureScatter.HeightFactor(hash);
            embed=accent.Embed;
        }
        else if(kind==Terrain.Sand && species is 6 or 7)
            desiredHeight=.55f*NatureScatter.HeightFactor(hash);
        else if(kind==Terrain.Grass && species is 8 or 9)
            desiredHeight=(species==9?.42f:.52f)*
                NatureScatter.HeightFactor(hash);
        else return null;

        var type=(NatureArtLibrary.Species)species;
        if(!_natureArt.Has(type))return null;
        float offsetX=((hash>>10)%100)/310f+.34f;
        float offsetY=((hash>>18)%100)/310f+.34f;
        Vector3 position=new(x+offsetX,
            SurfaceHeightAt(x,y,offsetX,offsetY)-embed,y+offsetY);
        return new NatureArtLibrary.Placement(type,position,desiredHeight,
            NatureScatter.RotationRadians(hash));
    }

    /// <summary>
    /// Initialize a touched chunk once. Its old native foliage is retained
    /// untouched until a complete new batch exists; then switch visibility
    /// atomically with the live ground surface.
    /// </summary>
    private void RefreshLiveNature((int X,int Y) key)
    {
        if(_natureArt is null)return;
        if(_liveNature.TryGetValue(key,out LiveNatureChunk? existing))
        {
            int x0=key.X*ChunkSize,y0=key.Y*ChunkSize;
            int xMax=Math.Min(_world.Width,x0+ChunkSize);
            int yMax=Math.Min(_world.Height,y0+ChunkSize);
            for(int y=y0;y<yMax;y++)
            for(int x=x0;x<xMax;x++)
            {
                int cell=(y-y0)*ChunkSize+(x-x0);
                var previous=existing.Cells[cell];
                var replacement=LiveNaturePlacement(x,y);
                if(Nullable.Equals(previous,replacement))continue;
                if(previous.HasValue &&
                    existing.Batches.TryGetValue(previous.Value.Species,out var old))
                    old.Remove(cell);
                if(replacement.HasValue)
                {
                    var next=replacement.Value;
                    if(!existing.Batches.TryGetValue(next.Species,out var batch))
                    {
                        batch=new LiveSpeciesBatch(_natureArt,existing.Root,
                            next.Species,4,_ecoMode);
                        existing.Batches.Add(next.Species,batch);
                    }
                    batch.Insert(cell,next);
                }
                existing.Cells[cell]=replacement;
            }
            return;
        }

        var root=new Node3D
        {
            Name=$"LiveNature_{key.X}_{key.Y}",
            Visible=false
        };
        _vegetation.AddChild(root);
        var chunk=new LiveNatureChunk(root);
        int left=key.X*ChunkSize,top=key.Y*ChunkSize;
        int right=Math.Min(_world.Width,left+ChunkSize);
        int bottom=Math.Min(_world.Height,top+ChunkSize);
        var groups=new Dictionary<NatureArtLibrary.Species,List<NatureEntry>>();
        for(int y=top;y<bottom;y++)
        for(int x=left;x<right;x++)
        {
            int cell=(y-top)*ChunkSize+(x-left);
            var placement=LiveNaturePlacement(x,y);
            chunk.Cells[cell]=placement;
            if(!placement.HasValue)continue;
            var p=placement.Value;
            if(!groups.TryGetValue(p.Species,out var entries))
            {
                entries=new List<NatureEntry>();
                groups.Add(p.Species,entries);
            }
            entries.Add(new NatureEntry(cell,p));
        }
        foreach(var pair in groups)
        {
            var batch=new LiveSpeciesBatch(_natureArt,root,pair.Key,
                Math.Min(ChunkSize*ChunkSize,pair.Value.Count+2),_ecoMode);
            chunk.Batches.Add(pair.Key,batch);
            batch.PopulateInitial(pair.Value);
        }
        _liveNature.Add(key,chunk);
        if(_foliageChunks.TryGetValue(key,out Node3D? original))
            original.Visible=false;
        root.Visible=true;
    }

    /// <summary>Optional local feedback after a tree is consumed while editing.</summary>
    private void UpdateLiveNatureStump(int x,int y)
    {
        var key=(x/ChunkSize,y/ChunkSize);
        if(!_liveNature.TryGetValue(key,out LiveNatureChunk? chunk))return;
        int cell=(y%ChunkSize)*ChunkSize+(x%ChunkSize);
        var previous=chunk.Cells[cell];
        var next=LiveNaturePlacement(x,y);
        if(Nullable.Equals(previous,next))return;
        if(previous.HasValue &&
            chunk.Batches.TryGetValue(previous.Value.Species,out var old))
            old.Remove(cell);
        if(next.HasValue)
        {
            if(!chunk.Batches.TryGetValue(next.Value.Species,out var target))
            {
                if(_natureArt is null)return;
                target=new LiveSpeciesBatch(_natureArt,chunk.Root,
                    next.Value.Species,4,_ecoMode);
                chunk.Batches.Add(next.Value.Species,target);
            }
            target.Insert(cell,next.Value);
        }
        chunk.Cells[cell]=next;
    }

    private void UpdateLiveNatureQuality()
    {
        foreach(LiveNatureChunk chunk in _liveNature.Values)
        foreach(LiveSpeciesBatch batch in chunk.Batches.Values)
            batch.SetEco(_ecoMode);
    }

    private void RemoveLiveNature((int X,int Y) key)
    {
        if(!_liveNature.Remove(key,out LiveNatureChunk? chunk))return;
        chunk.Root.Visible=false;
        chunk.Root.QueueFree();
    }

    private void ResetLiveNature()
    {
        foreach(LiveNatureChunk chunk in _liveNature.Values)
        {
            chunk.Root.Visible=false;
            chunk.Root.QueueFree();
        }
        _liveNature.Clear();
    }

    // Small read-only diagnostics for native in-engine parity tests.
    // Always return the CURRENT active root, not a same-named queued-for-
    // deletion scene node from a previous stroke in this render frame.
    internal Node3D? LiveNatureNodeAt(int x,int y) =>
        _liveNature.TryGetValue((x/ChunkSize,y/ChunkSize),out var chunk)
            ? chunk.Root : null;

    internal int LiveNatureSpeciesAt(int x,int y)
    {
        if(!_liveNature.TryGetValue((x/ChunkSize,y/ChunkSize),out var chunk))
            return -1;
        int cell=(y%ChunkSize)*ChunkSize+(x%ChunkSize);
        return chunk.Cells[cell] is { } p?(int)p.Species:-1;
    }
}
