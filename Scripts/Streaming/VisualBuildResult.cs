using System;
using System.Threading;
using WorldSim;
using WorldForge.VisualLab;

namespace WorldForge.Streaming;

public sealed class CoastBuildSnapshot
{
    public const int MaximumCells=128*128;
    private readonly Terrain[] _tiles;
    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public CoastBuildSnapshot(int width,int height,int seed,Func<int,int,Terrain> tileAt)
    {
        if(width<1 || height<1 || (long)width*height>MaximumCells)
            throw new ArgumentOutOfRangeException(nameof(width),"Small-world coast worker limit exceeded.");
        ArgumentNullException.ThrowIfNull(tileAt);
        Width=width;Height=height;Seed=seed;_tiles=new Terrain[width*height];
        for(int z=0;z<height;z++)
        for(int x=0;x<width;x++)
        {
            Terrain t=tileAt(x,z);
            if((byte)t>(byte)Terrain.Burned)throw new ArgumentException("Invalid coast biome.");
            _tiles[z*width+x]=t;
        }
    }
    public VisualBuildResult Build(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        byte[] bytes=WaterCoastMath.BuildMask(Width,Height,(x,z)=>
        {
            if(x==0)cancel.ThrowIfCancellationRequested();
            return _tiles[z*Width+x];
        },Seed);
        cancel.ThrowIfCancellationRequested();
        return VisualBuildResult.Coast(Width,Height,bytes);
    }
}

public sealed class VisualBuildResult
{
    private readonly byte[] _rgba;
    public ChunkMeshData? Mesh { get; }
    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Rgba => _rgba;
    public long PayloadBytes => Mesh?.PayloadBytes ?? _rgba.Length;
    private VisualBuildResult(ChunkMeshData? mesh,int width,int height,byte[] rgba)
    { Mesh=mesh;Width=width;Height=height;_rgba=rgba; }
    public static VisualBuildResult Chunk(ChunkMeshData mesh)=>new(mesh,0,0,[]);
    internal static VisualBuildResult Coast(int width,int height,byte[] rgba)=>new(null,width,height,rgba);
}
