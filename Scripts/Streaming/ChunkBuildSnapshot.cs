using System;
using WorldSim;
using WorldForge.VisualLab;

namespace WorldForge.Streaming;

/// <summary>Owned immutable raw chunk data. Delegates are used only during capture.</summary>
public sealed class ChunkBuildSnapshot
{
    private readonly Terrain[] _tiles;
    private readonly float[] _offsets;
    private readonly int _left, _top, _stride, _depth;
    public ChunkRequest Request { get; }
    public int WorldWidth { get; }
    public int WorldHeight { get; }
    public int Seed { get; }
    public int X => Request.Key.X * 16;
    public int Z => Request.Key.Z * 16;
    public int Width { get; }
    public int Depth { get; }
    public bool Rich { get; }
    public bool Facets { get; }
    public bool Smooth { get; }
    public bool SmoothLand => Rich && Smooth;
    public int AssetMask { get; }
    public bool ImportedTree { get; }
    public bool ImportedRock { get; }
    public int ArrayPayloadBytes => _tiles.Length + _offsets.Length * sizeof(float);

    public ChunkBuildSnapshot(ChunkRequest request, int width, int height, int seed,
        bool rich, bool facets, bool smooth, int assetMask, bool importedTree, bool importedRock,
        Func<int,int,Terrain> tileAt, Func<int,int,float> offsetAt)
    {
        if (width is < 1 or > 1024 || height is < 1 or > 1024 || request.Key.X < 0 ||
            request.Key.Z < 0 || (long)request.Key.X * 16 >= width ||
            (long)request.Key.Z * 16 >= height || request.Ticket < 1 ||
            request.Generation.WorldEpoch < 1 || request.Generation.DataRevision < 0 ||
            request.Generation.FormatVersion < 1 || (assetMask & ~1023) != 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentNullException.ThrowIfNull(tileAt);
        ArgumentNullException.ThrowIfNull(offsetAt);
        Request=request; WorldWidth=width; WorldHeight=height; Seed=seed;
        Rich=rich; Facets=facets; Smooth=smooth; AssetMask=assetMask;
        ImportedTree=importedTree; ImportedRock=importedRock;
        Width=Math.Min(16,width-X); Depth=Math.Min(16,height-Z);
        _left=Math.Max(0,X-1); _top=Math.Max(0,Z-1);
        _stride=Math.Min(width,X+Width+1)-_left;
        _depth=Math.Min(height,Z+Depth+1)-_top;
        _tiles=new Terrain[_stride*_depth];
        for(int z=0;z<_depth;z++)
        for(int x=0;x<_stride;x++)
        {
            Terrain t=tileAt(_left+x,_top+z);
            if((byte)t>(byte)Terrain.Burned) throw new ArgumentException("Invalid biome snapshot.");
            _tiles[z*_stride+x]=t;
        }
        _offsets=new float[(Width+1)*(Depth+1)];
        for(int z=0;z<=Depth;z++)
        for(int x=0;x<=Width;x++)
        {
            float h=offsetAt(X+x,Z+z);
            if(!float.IsFinite(h) || Math.Abs(h)>8f) throw new ArgumentException("Invalid elevation snapshot.");
            _offsets[z*(Width+1)+x]=h;
        }
    }

    public bool Contains(int x,int z) => x>=0 && z>=0 && x<WorldWidth && z<WorldHeight;
    public Terrain Tile(int x,int z)
    {
        if(x<_left || z<_top || x>=_left+_stride || z>=_top+_depth)
            throw new ArgumentOutOfRangeException(nameof(x),"Read exceeded captured one-cell halo.");
        return _tiles[(z-_top)*_stride+x-_left];
    }
    public float Offset(int x,int z)
    {
        if(x<X || z<Z || x>X+Width || z>Z+Depth)
            throw new ArgumentOutOfRangeException(nameof(x),"Read exceeded owned corner lattice.");
        return _offsets[(z-Z)*(Width+1)+x-X];
    }
    public float Height(int x,int z)
    {
        if(!Contains(x,z)) return .12f;
        return Tile(x,z) switch
        {
            Terrain.DeepWater or Terrain.ShallowWater => .12f,
            Terrain.Sand => .29f, Terrain.Grass => .61f, Terrain.Forest => .68f,
            Terrain.Burned => .53f,
            Terrain.Mountain => 2.6f+(EnvironmentArtMath.Hash(x,z,Seed)%11)*.17f,
            _ => throw new InvalidOperationException("Invalid captured terrain.")
        };
    }
    public float Relief(int x,int z) => Rich && Facets && Contains(x,z)
        ? TerrainReliefMath.CenterLift(Tile(x,z),x,z,Seed) : 0f;
    public SharedCornerHeightfield.Fan Fan(int x,int z)
    {
        float h=Height(x,z);
        if(!SmoothLand) return new(h,h,h,h,h+Relief(x,z));
        var f=SharedCornerHeightfield.Tile(x,z,WorldWidth,WorldHeight,Height,Relief(x,z));
        float a=Offset(x,z),b=Offset(x,z+1),c=Offset(x+1,z),d=Offset(x+1,z+1);
        return new(f.A+a,f.B+b,f.C+c,f.D+d,f.Center+(a+b+c+d)*.25f);
    }
    public float Surface(int x,int z,float u,float v) =>
        SmoothLand && !EnvironmentArtMath.Water(Tile(x,z))
            ? Fan(x,z).Sample(u,v)
            : TerrainReliefMath.SurfaceHeight(Height(x,z),Relief(x,z),u,v);
    public bool HasAsset(int species) => species is >=0 and <10 && (AssetMask & (1<<species))!=0;
}
