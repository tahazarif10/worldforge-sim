using System;
using System.Numerics;

namespace WorldForge.Streaming;

public enum NumericPropKind { Authored, ImportedTree, ImportedRock, Trunk, LowerCanopy, UpperCanopy, Stone, Shrub }
public readonly record struct NumericProp(NumericPropKind Kind,int Species,
    Vector3 Position,Vector3 Scale,float Height,float Angle,Vector4 Color,uint Variant);

/// <summary>Worker-owned buffers transfer to the owner only after Task completion.</summary>
public sealed class ChunkMeshData
{
    private readonly Vector3[] _positions, _normals;
    private readonly Vector4[] _colors;
    private readonly NumericProp[] _props;
    public ReadOnlySpan<Vector3> Positions => _positions;
    public ReadOnlySpan<Vector3> Normals => _normals;
    public ReadOnlySpan<Vector4> Colors => _colors;
    public ReadOnlySpan<NumericProp> Props => _props;
    public int LandCells { get; }
    public int PropCount { get; }
    public int CellsVisited { get; }
    public long TerrainPayloadBytes => (long)_positions.Length*40;
    public long FoliagePayloadBytes => (long)_props.Length*64;
    public long PayloadBytes => TerrainPayloadBytes+FoliagePayloadBytes;
    public const int MaximumVertices = 16*16*(12+72+24);
    public const int MaximumProps = 16*16*3;
    public const long MaximumPayloadBytes = (long)MaximumVertices*40+(long)MaximumProps*64;

    internal ChunkMeshData(Vector3[] positions,Vector3[] normals,Vector4[] colors,
        NumericProp[] props,int land,int count,int visited)
    {
        if(positions.Length>MaximumVertices || props.Length>MaximumProps ||
            normals.Length!=positions.Length || colors.Length!=positions.Length)
            throw new ArgumentException("Numeric chunk output exceeds its declared bounds.");
        _positions=positions;_normals=normals;_colors=colors;_props=props;
        LandCells=land;PropCount=count;CellsVisited=visited;
    }
}
