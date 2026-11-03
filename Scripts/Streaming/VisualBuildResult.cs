using System;
using System.Threading;
using System.Security.Cryptography;
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
        // Exercise bounded patches while preserving the small-world publication contract.
        // The retained V2 global mask remains the independent whole-world parity oracle.
        byte[] bytes=new byte[checked(Width*Height*4)];
        for(int z=0;z<Height;z+=CoastPatchSnapshot.InteriorLimit)
        for(int x=0;x<Width;x+=CoastPatchSnapshot.InteriorLimit)
        {
            cancel.ThrowIfCancellationRequested();
            var patch=new CoastPatchSnapshot(Width,Height,x,z,
                Math.Min(CoastPatchSnapshot.InteriorLimit,Width-x),
                Math.Min(CoastPatchSnapshot.InteriorLimit,Height-z),Seed,
                (tx,tz)=>_tiles[tz*Width+tx]);
            byte[] rgba=patch.Build(cancel);
            for(int row=0;row<patch.InteriorHeight;row++)
                rgba.AsSpan(((z-patch.MaskZ+row)*patch.MaskWidth+x-patch.MaskX)*4,
                    patch.InteriorWidth*4).CopyTo(bytes.AsSpan(((z+row)*Width+x)*4));
        }
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
    public string? CoastPayloadSha256 { get; }
    public long PayloadBytes => Mesh?.PayloadBytes ?? _rgba.Length;
    private VisualBuildResult(ChunkMeshData? mesh,int width,int height,byte[] rgba)
    { Mesh=mesh;Width=width;Height=height;_rgba=rgba;
        CoastPayloadSha256=mesh is null?Convert.ToHexString(SHA256.HashData(rgba)):null; }
    public static VisualBuildResult Chunk(ChunkMeshData mesh)=>new(mesh,0,0,[]);
    internal static VisualBuildResult Coast(int width,int height,byte[] rgba)=>new(null,width,height,rgba);
}
