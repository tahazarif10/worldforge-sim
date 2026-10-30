using System;
using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Low-cost deterministic center lift on the four triangles ALREADY drawn
/// around each land cell. Outer tile edge vertices remain at the authoritative
/// plateau height: no mesh crack, waterline move or new cliff-top triangles.
/// Not a replacement for a continuous multi-region elevation data model.
/// </summary>
public static class TerrainReliefMath
{
    public static float CenterLift(Terrain kind, int x, int y, int seed)
    {
        if (kind is Terrain.DeepWater or Terrain.ShallowWater) return 0f;
        uint hash = EnvironmentArtMath.Hash(x, y, seed + 414);
        float variation = (hash & 255u) / 255f;
        return kind switch
        {
            Terrain.Mountain => 0.12f + variation * 0.16f,
            Terrain.Forest => 0.035f + variation * 0.05f,
            Terrain.Grass => 0.018f + variation * 0.032f,
            Terrain.Sand => 0.008f + variation * 0.012f,
            Terrain.Burned => 0.016f + variation * 0.026f,
            _ => 0f
        };
    }

    /// <summary>
    /// Exact height of the four center-fan triangles at a local tile X/Z.
    /// Corners and edges remain unchanged. This can be checked independently
    /// against a ray-triangle intersection and used for people/prop grounding.
    /// </summary>
    public static float SurfaceHeight(float plateau, float lift, float u, float v)
    {
        u = Math.Clamp(u, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);
        float factor = MathF.Max(0f,
            1f - 2f * MathF.Max(MathF.Abs(u - 0.5f), MathF.Abs(v - 0.5f)));
        return plateau + lift * factor;
    }
}
