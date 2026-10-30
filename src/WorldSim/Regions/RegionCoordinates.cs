using System;

namespace WorldSim.Regions;

public readonly record struct CellCoord(long X, long Y);
public readonly record struct RegionKey(long X, long Y);
public readonly record struct LocalCell(int X, int Y);

/// <summary>
/// 64x64 logical data regions. Floor division, not C# truncated division,
/// ensures cell -1 belongs to region -1 at local coordinate 63.
/// </summary>
public static class RegionCoordinates
{
    public const int Size = 64;
    public const int CellCount = Size * Size;

    public static RegionKey RegionOf(CellCoord cell) =>
        new(FloorDivide(cell.X, Size), FloorDivide(cell.Y, Size));

    public static LocalCell LocalOf(CellCoord cell) =>
        new(FloorMod(cell.X, Size), FloorMod(cell.Y, Size));

    public static CellCoord GlobalOf(RegionKey key, LocalCell local)
    {
        if (local.X is < 0 or >= Size || local.Y is < 0 or >= Size)
            throw new ArgumentOutOfRangeException(nameof(local));
        return new(checked(key.X * Size + local.X), checked(key.Y * Size + local.Y));
    }

    public static int LocalIndex(LocalCell local) =>
        checked(local.Y * Size + local.X);

    public static long FloorDivide(long value, int divisor)
    {
        if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
        long quotient = value / divisor;
        if (value % divisor < 0) quotient--;
        return quotient;
    }

    public static int FloorMod(long value, int divisor)
    {
        if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
        long remainder = value % divisor;
        if (remainder < 0) remainder += divisor;
        return (int)remainder;
    }
}
