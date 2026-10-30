using System;
using System.Collections.Generic;

namespace WorldForge.Streaming;

/// <summary>
/// Godot-independent conservative visibility planner for a tile-based world.
/// Enumerates only chunk coordinates intersecting the camera's ground footprint,
/// not all chunks in the whole world. Does not construct or stream world data.
/// </summary>
public static class VisibleChunkPlanner
{
    public static HashSet<(int X, int Y)> Plan(
        int worldWidth, int worldHeight, int chunkSize,
        float minX, float minY, float maxX, float maxY, int marginChunks = 1)
    {
        if (worldWidth <= 0 || worldHeight <= 0 || chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "World dimensions and chunk size must be positive.");
        if (marginChunks is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(marginChunks));
        if (!float.IsFinite(minX) || !float.IsFinite(minY) ||
            !float.IsFinite(maxX) || !float.IsFinite(maxY))
            throw new ArgumentException("Camera bounds must be finite.");
        if (minX > maxX || minY > maxY) throw new ArgumentException("Inverted viewport bounds.");

        var result = new HashSet<(int X, int Y)>();
        if (maxX < 0 || maxY < 0 || minX >= worldWidth || minY >= worldHeight)
            return result;

        int chunksX = (worldWidth - 1) / chunkSize + 1;
        int chunksY = (worldHeight - 1) / chunkSize + 1;
        int left = Math.Clamp((int)MathF.Floor(MathF.Max(0, minX) / chunkSize) - marginChunks, 0, chunksX - 1);
        int top = Math.Clamp((int)MathF.Floor(MathF.Max(0, minY) / chunkSize) - marginChunks, 0, chunksY - 1);
        int right = Math.Clamp((int)MathF.Floor(MathF.Min(worldWidth - 0.001f, maxX) / chunkSize) + marginChunks, 0, chunksX - 1);
        int bottom = Math.Clamp((int)MathF.Floor(MathF.Min(worldHeight - 0.001f, maxY) / chunkSize) + marginChunks, 0, chunksY - 1);
        for (int cy = top; cy <= bottom; cy++)
        for (int cx = left; cx <= right; cx++)
            result.Add((cx, cy));
        return result;
    }
}
