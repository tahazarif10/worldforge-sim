using System;
using System.Collections.Generic;
using System.Numerics;

namespace WorldForge.Streaming;

public readonly record struct ChunkCoord(int X, int Z);
public readonly record struct GroundBounds(double MinX, double MinZ, double MaxX, double MaxZ);

/// <summary>
/// Conservative X/Z bounds of the finite camera frustum intersected with a
/// terrain height slab. A rectangle may over-request rotated corner areas;
/// it must never silently truncate visible demand to satisfy a capacity.
/// </summary>
public sealed class ViewportChunkPlan
{
    public IReadOnlySet<ChunkCoord> Visible { get; }
    public IReadOnlySet<ChunkCoord> Desired { get; }
    public GroundBounds? Bounds { get; }
    internal ViewportChunkPlan(HashSet<ChunkCoord> visible,
        HashSet<ChunkCoord> desired, GroundBounds? bounds)
    {
        Visible = visible;
        Desired = desired;
        Bounds = bounds;
    }
}

public static class ViewportChunkPlanner
{
    // Near TL, TR, BR, BL followed by matching far corners.
    private static readonly (int A, int B)[] Edges =
    [
        (0,1), (1,2), (2,3), (3,0), (4,5), (5,6),
        (6,7), (7,4), (0,4), (1,5), (2,6), (3,7)
    ];
    private const double EdgePadding = 0.01; // Conservative float projection tolerance.

    public static ViewportChunkPlan Plan(int worldWidth, int worldHeight,
        int chunkSize, ReadOnlySpan<Vector3> corners, double minimumY,
        double maximumY, int marginChunks = 1, int capacity = 2048)
    {
        if (worldWidth < 1 || worldHeight < 1 || chunkSize < 1 ||
            capacity < 1 || capacity > 65536 || marginChunks is < 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (corners.Length != 8 || !double.IsFinite(minimumY) ||
            !double.IsFinite(maximumY) || minimumY > maximumY)
            throw new ArgumentException("Eight finite frustum corners and an ordered height slab are required.");
        foreach (Vector3 point in corners)
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                throw new ArgumentException("Nonfinite camera corner.", nameof(corners));

        double minX = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        void Include(double x, double z)
        {
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
        }
        foreach (Vector3 point in corners)
            if (point.Y >= minimumY && point.Y <= maximumY)
                Include(point.X, point.Z);

        // Vertices of the clipped convex volume are original vertices inside
        // the slab or intersections of original edges with either slab plane.
        foreach (var edge in Edges)
        {
            Vector3 a = corners[edge.A], b = corners[edge.B];
            double dy = (double)b.Y - a.Y;
            if (dy == 0) continue;
            for (int plane = 0; plane < 2; plane++)
            {
                double y = plane == 0 ? minimumY : maximumY;
                double t = (y - a.Y) / dy;
                if (t < 0 || t > 1) continue;
                Include(a.X + t * ((double)b.X - a.X),
                    a.Z + t * ((double)b.Z - a.Z));
            }
        }

        if (!double.IsFinite(minX) || maxX < -EdgePadding ||
            maxZ < -EdgePadding || minX > worldWidth + EdgePadding ||
            minZ > worldHeight + EdgePadding)
            return new ViewportChunkPlan(new(), new(), null);

        minX = Math.Clamp(minX - EdgePadding, 0, worldWidth);
        minZ = Math.Clamp(minZ - EdgePadding, 0, worldHeight);
        maxX = Math.Clamp(maxX + EdgePadding, 0, worldWidth);
        maxZ = Math.Clamp(maxZ + EdgePadding, 0, worldHeight);
        int columns = (worldWidth - 1) / chunkSize + 1;
        int rows = (worldHeight - 1) / chunkSize + 1;
        int left = Math.Min(columns - 1, (int)Math.Floor(minX / chunkSize));
        int right = Math.Min(columns - 1, (int)Math.Floor(maxX / chunkSize));
        int top = Math.Min(rows - 1, (int)Math.Floor(minZ / chunkSize));
        int bottom = Math.Min(rows - 1, (int)Math.Floor(maxZ / chunkSize));
        int demandLeft = Math.Max(0, left - marginChunks);
        int demandTop = Math.Max(0, top - marginChunks);
        int demandRight = (int)Math.Min(columns - 1L, (long)right + marginChunks);
        int demandBottom = (int)Math.Min(rows - 1L, (long)bottom + marginChunks);
        long count = (demandRight - demandLeft + 1L) * (demandBottom - demandTop + 1L);
        if (count > capacity)
            throw new InvalidOperationException(
                $"Viewport requires {count} chunks including margin; capacity is {capacity}. " +
                "Refusing partial coverage. A coarser fallback is required.");

        var visible = new HashSet<ChunkCoord>();
        var desired = new HashSet<ChunkCoord>();
        for (int z = demandTop; z <= demandBottom; z++)
        for (int x = demandLeft; x <= demandRight; x++)
        {
            var key = new ChunkCoord(x, z);
            desired.Add(key);
            if (x >= left && x <= right && z >= top && z <= bottom)
                visible.Add(key);
        }
        return new ViewportChunkPlan(visible, desired,
            new GroundBounds(minX, minZ, maxX, maxZ));
    }
}
