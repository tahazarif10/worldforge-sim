using System;
using System.Numerics;

namespace WorldForge.Streaming;

[Flags]
public enum FallbackBanks : byte { None = 0, West = 1, East = 2, North = 4, South = 8 }

/// <summary>One sampled cell. A/B/C/D are NW/SW/NE/SE; floors are W/E/N/S.</summary>
public readonly record struct FallbackCell(
    bool Land, Vector4 Heights, float Center,
    Vector4 ColorA, Vector4 ColorB, Vector4 ColorC, Vector4 ColorD, Vector4 CenterColor,
    FallbackBanks Banks, Vector4 Floors, Vector4 BankColor);

public sealed record FallbackMeshData(
    Vector3[] Positions, Vector3[] Normals, Vector4[] Colors,
    int LandCells, int WaterCells, int TopTriangles, int BankTriangles);

/// <summary>
/// Pure bounded surface-only proxy. Exact four-triangle V2 tops preserve small
/// islands, sculpt peaks and shared edges; banks are unlayered, with no props.
/// No native resources, world reads or retained cache; callers own the arrays.
/// </summary>
public static class TerrainFallbackMesh
{
    public const int MaximumSide = 16;
    public const int MaximumVertices = MaximumSide * MaximumSide * 36;

    public static FallbackMeshData Build(int originX, int originZ, int width, int depth,
        ReadOnlySpan<FallbackCell> cells)
    {
        if (originX < 0 || originZ < 0 || width is < 1 or > MaximumSide ||
            depth is < 1 or > MaximumSide || cells.Length != width * depth ||
            originX > 1_000_000 - width || originZ > 1_000_000 - depth)
            throw new ArgumentOutOfRangeException(nameof(width));
        int land = 0, banks = 0;
        foreach (FallbackCell cell in cells)
        {
            if (!cell.Land) continue;
            if (!Finite(cell.Heights) || !float.IsFinite(cell.Center) ||
                !Finite(cell.Floors) || !Finite(cell.ColorA) || !Finite(cell.ColorB) ||
                !Finite(cell.ColorC) || !Finite(cell.ColorD) ||
                !Finite(cell.CenterColor) || !Finite(cell.BankColor) ||
                ((byte)cell.Banks & ~15) != 0)
                throw new ArgumentException("Invalid fallback cell.", nameof(cells));
            land++;
            banks += BitOperations.PopCount((uint)cell.Banks);
        }
        int vertices = checked(land * 12 + banks * 6);
        var positions = new Vector3[vertices];
        var normals = new Vector3[vertices];
        var colors = new Vector4[vertices];
        int cursor = 0;

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector4 ca, Vector4 cb, Vector4 cc)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            if (!float.IsFinite(normal.X) || !float.IsFinite(normal.Y) ||
                !float.IsFinite(normal.Z) || normal.LengthSquared() < .5f)
                throw new ArgumentException("Fallback normal is not representable.", nameof(cells));
            Vertex(a, normal, ca); Vertex(b, normal, cb); Vertex(c, normal, cc);
        }
        void Vertex(Vector3 p, Vector3 normal, Vector4 color)
        {
            positions[cursor] = p; normals[cursor] = normal; colors[cursor++] = color;
        }
        void Bank(Vector3 highA, Vector3 highB, float floor, Vector3 normal, Vector4 color)
        {
            var lowA = new Vector3(highA.X, floor, highA.Z);
            var lowB = new Vector3(highB.X, floor, highB.Z);
            Vertex(lowA, normal, color); Vertex(lowB, normal, color); Vertex(highA, normal, color);
            Vertex(highA, normal, color); Vertex(lowB, normal, color); Vertex(highB, normal, color);
        }

        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            FallbackCell cell = cells[z * width + x];
            if (!cell.Land) continue;
            float px = originX + x, pz = originZ + z;
            Vector4 h = cell.Heights;
            var a = new Vector3(px, h.X, pz);
            var b = new Vector3(px, h.Y, pz + 1);
            var c = new Vector3(px + 1, h.Z, pz);
            var d = new Vector3(px + 1, h.W, pz + 1);
            var m = new Vector3(px + .5f, cell.Center, pz + .5f);
            Triangle(a, b, m, cell.ColorA, cell.ColorB, cell.CenterColor);
            Triangle(b, d, m, cell.ColorB, cell.ColorD, cell.CenterColor);
            Triangle(d, c, m, cell.ColorD, cell.ColorC, cell.CenterColor);
            Triangle(c, a, m, cell.ColorC, cell.ColorA, cell.CenterColor);
            if (cell.Banks.HasFlag(FallbackBanks.West))
                Bank(a, b, cell.Floors.X, -Vector3.UnitX, cell.BankColor);
            if (cell.Banks.HasFlag(FallbackBanks.East))
                Bank(d, c, cell.Floors.Y, Vector3.UnitX, cell.BankColor);
            if (cell.Banks.HasFlag(FallbackBanks.North))
                Bank(c, a, cell.Floors.Z, Vector3.UnitZ, cell.BankColor);
            if (cell.Banks.HasFlag(FallbackBanks.South))
                Bank(b, d, cell.Floors.W, -Vector3.UnitZ, cell.BankColor);
        }
        return new FallbackMeshData(positions, normals, colors, land,
            cells.Length - land, land * 4, banks * 2);
    }

    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) &&
        float.IsFinite(v.Z) && float.IsFinite(v.W);
}
