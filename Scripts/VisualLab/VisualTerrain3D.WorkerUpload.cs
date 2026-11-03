using System;
using System.Collections.Generic;
using Godot;
using WorldForge.Streaming;
using Num3=System.Numerics.Vector3;
using Num4=System.Numerics.Vector4;

namespace WorldForge.VisualLab;

public partial class VisualTerrain3D
{
    private static Vector3 Native(Num3 v)=>new(v.X,v.Y,v.Z);
    private static Color Native(Num4 v)=>new(v.X,v.Y,v.Z,v.W);

    private Node3D BuildWorkerFoliage(ChunkCoord key,ChunkMeshData data)
    {
        var chunk=new Node3D {Name=$"FoliageChunk_{key.X}_{key.Z}",Visible=false};
        try
        {
            var trunks=new List<(Transform3D Transform,Color Color)>();
            var lower=new List<(Transform3D Transform,Color Color)>();
            var upper=new List<(Transform3D Transform,Color Color)>();
            var stones=new List<(Transform3D Transform,Color Color)>();
            var shrubs=new List<(Transform3D Transform,Color Color)>();
            var authored=new List<NatureArtLibrary.Placement>();
            foreach(NumericProp p in data.Props)
            {
                Vector3 position=Native(p.Position);
                if(p.Kind==NumericPropKind.Authored)
                    authored.Add(new((NatureArtLibrary.Species)p.Species,position,p.Height,p.Angle));
                else if(p.Kind is NumericPropKind.ImportedTree or NumericPropKind.ImportedRock)
                {
                    PackedScene? scene=p.Kind==NumericPropKind.ImportedTree?_treePrefab:_rockPrefab;
                    if(scene is null)throw new InvalidOperationException("Captured imported art is unavailable.");
                    AddImportedModel(chunk,scene,position,p.Height,p.Variant);
                }
                else
                {
                    var placement=(MakeTransform(position,Native(p.Scale)),Native(p.Color));
                    switch(p.Kind)
                    {
                        case NumericPropKind.Trunk:trunks.Add(placement);break;
                        case NumericPropKind.LowerCanopy:lower.Add(placement);break;
                        case NumericPropKind.UpperCanopy:upper.Add(placement);break;
                        case NumericPropKind.Stone:stones.Add(placement);break;
                        case NumericPropKind.Shrub:shrubs.Add(placement);break;
                        default:throw new InvalidOperationException("Unknown captured prop.");
                    }
                }
            }
            var trunkMesh=new CylinderMesh {Height=1.2f,TopRadius=.4f,BottomRadius=.5f,RadialSegments=5};
            var foliageMesh=new SphereMesh {Radius=.5f,Height=1f,RadialSegments=7,Rings=4};
            var rockMesh=new SphereMesh {Radius=.5f,Height=1f,RadialSegments=6,Rings=3};
            AddBatched(chunk,"TreeTrunks",trunkMesh,trunks);
            AddBatched(chunk,"TreeCrownsLower",foliageMesh,lower);
            AddBatched(chunk,"TreeCrownsUpper",foliageMesh,upper);
            AddBatched(chunk,"Rocks",rockMesh,stones);
            AddBatched(chunk,"FlowersShrubs",foliageMesh,shrubs);
            _natureArt?.Populate(chunk,authored);
            ApplyShadowTier(chunk);
            return chunk;
        }
        catch { chunk.Free(); throw; }
    }

    private bool UploadWorkerChunk(ChunkRequest request,ChunkMeshData data)
    {
        AssertWorkerOwner();
        if(!_viewportQueue.IsCurrent(request))return false;
        ArrayMesh? mesh=null;
        MeshInstance3D? terrain=null;
        Node3D? foliage=null;
        bool published=false;
        var key=(request.Key.X,request.Key.Z);
        _chunks.TryGetValue(key,out MeshInstance3D? oldTerrain);
        _foliageChunks.TryGetValue(key,out Node3D? oldFoliage);
        _viewportDetails.TryGetValue(request.Key,out ViewportSurface? oldSurface);
        int oldCount=_propCounts.GetValueOrDefault(key);
        try
        {
            if(data.LandCells>0)
            {
                var vertices=new Vector3[data.Positions.Length];
                var normals=new Vector3[vertices.Length];
                var colors=new Color[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {vertices[i]=Native(data.Positions[i]);normals[i]=Native(data.Normals[i]);colors[i]=Native(data.Colors[i]);}
                using var arrays=new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex]=vertices;
                arrays[(int)Mesh.ArrayType.Normal]=normals;
                arrays[(int)Mesh.ArrayType.Color]=colors;
                mesh=new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
                if(mesh.GetSurfaceCount()!=1)throw new InvalidOperationException("Worker mesh upload failed.");
                mesh.SurfaceSetMaterial(0,_earthMaterial);
            }
            terrain=new MeshInstance3D {Name=$"TerrainChunk_{key.Item1}_{key.Item2}",Mesh=mesh,Visible=false};
            foliage=BuildWorkerFoliage(request.Key,data);
            if(!_viewportQueue.IsCurrent(request))return false;
            AddChild(terrain);_vegetation.AddChild(foliage);
            _chunks[key]=terrain;_foliageChunks[key]=foliage;
            _propCounts[key]=data.PropCount;
            RecordViewportDetail(key.Item1,key.Item2,terrain,data.LandCells);
            CompleteViewportDetail(request);
            published=true;
            LastPropCount+=data.PropCount-oldCount;
            FoliageCellsVisited+=data.CellsVisited;FoliageChunksRebuilt++;TotalTerrainChunkBuilds++;
            if(oldTerrain is not null){oldTerrain.Visible=false;oldTerrain.QueueFree();}
            if(oldFoliage is not null){oldFoliage.Visible=false;oldFoliage.QueueFree();}
            return true;
        }
        finally
        {
            if(!published)
            {
                if(oldTerrain is null)_chunks.Remove(key);else _chunks[key]=oldTerrain;
                if(oldFoliage is null)_foliageChunks.Remove(key);else _foliageChunks[key]=oldFoliage;
                if(oldSurface is null)_viewportDetails.Remove(request.Key);else _viewportDetails[request.Key]=oldSurface;
                if(oldTerrain is null)_propCounts.Remove(key);else _propCounts[key]=oldCount;
                terrain?.Free();foliage?.Free();
                // A staged mesh has no remaining scene owner on failure.
                mesh?.Dispose();
            }
        }
    }

    private void UploadWorkerCoast(VisualBuildResult result)
    {
        AssertWorkerOwner();
        if(result.Mesh is not null || result.Width!=_world.Width || result.Height!=_world.Height ||
            result.Rgba.Length!=checked(_world.Width*_world.Height*4) ||
            _water.MaterialOverride is not ShaderMaterial material)
            throw new InvalidOperationException("Coast result does not match the current world.");
        using Image image=Image.CreateFromData(result.Width,result.Height,false,Image.Format.Rgba8,result.Rgba.ToArray());
        if(_coastMaskTexture is null)_coastMaskTexture=ImageTexture.CreateFromImage(image);
        else _coastMaskTexture.Update(image);
        material.SetShaderParameter("shore_mask",_coastMaskTexture);
        material.SetShaderParameter("map_size",new Vector2(_world.Width,_world.Height));
        _lastCoastPayloadSha256=result.CoastPayloadSha256;
        _coastPublications++;
        _coastDirty=false;
    }
}
