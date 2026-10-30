using System;
using System.Collections.Generic;

namespace WorldForge.Streaming;

/// <summary>
/// Build only the nearest missing visible chunks each update. Pure, bounded,
/// reproducible scheduler; mesh and Godot SceneTree changes are on main thread.
/// A hard count cap prevents a camera teleport from creating the entire world
/// during one frame. Future streaming will use measured elapsed time as well.
/// </summary>
public static class ChunkLoadBudget
{
    public static List<(int X, int Y)> Choose(
        IReadOnlyCollection<(int X, int Y)> desired,
        IReadOnlyDictionary<(int X, int Y), bool> resident,
        float focusX, float focusY, int chunkSize, int maxBuilds)
    {
        if (chunkSize < 1 || maxBuilds < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBuilds));
        var missing = new List<(int X, int Y)>();
        foreach (var key in desired)
            if (!resident.ContainsKey(key)) missing.Add(key);

        missing.Sort((a, b) =>
        {
            double ax = (a.X + 0.5) * chunkSize - focusX;
            double ay = (a.Y + 0.5) * chunkSize - focusY;
            double bx = (b.X + 0.5) * chunkSize - focusX;
            double by = (b.Y + 0.5) * chunkSize - focusY;
            int order = (ax * ax + ay * ay).CompareTo(bx * bx + by * by);
            if (order != 0) return order;
            order = a.Y.CompareTo(b.Y);
            return order != 0 ? order : a.X.CompareTo(b.X);
        });
        if (missing.Count > maxBuilds)
            missing.RemoveRange(maxBuilds, missing.Count - maxBuilds);
        return missing;
    }
}
