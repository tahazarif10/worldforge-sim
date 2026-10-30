using System;

namespace WorldForge.VisualLab;

/// <summary>
/// Render-only continuous land heightfield. Each lattice vertex samples the
/// same four adjacent authoritative cell heights, so independently rebuilt
/// 16x16 chunks share exact vertex positions along their border.
/// No world-sized height array, extra top triangles or GPU texture reads.
/// </summary>
public static class SharedCornerHeightfield
{
    public readonly record struct Fan(float A, float B, float C, float D, float Center)
    {
        /// <summary>Exact barycentric height on a,b,center / b,d,center /
        /// d,c,center / c,a,center, including shared edges and corners.</summary>
        public float Sample(float u, float v)
        {
            u = Math.Clamp(u, 0f, 1f);
            v = Math.Clamp(v, 0f, 1f);
            float du = u - .5f, dv = v - .5f;
            if (MathF.Abs(du) > MathF.Abs(dv))
            {
                if (du < 0f)
                    return A * (1f - u - v) + B * (v - u) + Center * (2f * u);
                return C * (u - v) + D * (u + v - 1f) +
                       Center * (2f * (1f - u));
            }
            if (dv < 0f)
                return A * (1f - u - v) + C * (u - v) + Center * (2f * v);
            return B * (v - u) + D * (u + v - 1f) +
                   Center * (2f * (1f - v));
        }
    }

    /// <summary>
    /// Symmetric four-center average at a vertex. Clamping the world edge
    /// extends the boundary smoothly instead of sampling nonexistent land.
    /// Water centers remain at sea elevation via the supplied height sampler.
    /// </summary>
    public static float Corner(int vx, int vz, int width, int height,
        Func<int, int, float> sampleCell)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(sampleCell);
        int x0 = Math.Clamp(vx - 1, 0, width - 1);
        int x1 = Math.Clamp(vx, 0, width - 1);
        int z0 = Math.Clamp(vz - 1, 0, height - 1);
        int z1 = Math.Clamp(vz, 0, height - 1);
        return (sampleCell(x0,z0) + sampleCell(x1,z0) +
                sampleCell(x0,z1) + sampleCell(x1,z1)) * .25f;
    }

    public static Fan Tile(int x, int z, int width, int height,
        Func<int, int, float> sampleCell, float centerLift)
    {
        if (x < 0 || x >= width || z < 0 || z >= height)
            throw new ArgumentOutOfRangeException(nameof(x));
        return new Fan(
            Corner(x, z, width, height, sampleCell),
            Corner(x, z + 1, width, height, sampleCell),
            Corner(x + 1, z, width, height, sampleCell),
            Corner(x + 1, z + 1, width, height, sampleCell),
            sampleCell(x,z) + centerLift);
    }
}
