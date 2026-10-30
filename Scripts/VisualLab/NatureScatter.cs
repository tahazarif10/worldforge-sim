using WorldSim;

namespace WorldForge.VisualLab;

/// <summary>
/// Deterministic species selection independent of Godot and simulation RNG.
/// No random state; identical world seed and cell coordinate yield identical art.
/// </summary>
public static class NatureScatter
{
    // Species 0..5 tree variants, 6..7 rocks, 8 grass, 9 flowers;
    // -1 means no decorative model. Called only for camera-resident cells.
    public static int Pick(Terrain terrain, uint hash) => terrain switch
    {
        Terrain.Forest when hash % 7 < 5 => (int)((hash >> 9) % 6),
        Terrain.Mountain when hash % 5 < 3 => 6 + (int)((hash >> 8) % 2),
        Terrain.Grass when hash % 4 == 0 => 8 + (int)((hash >> 11) % 2),
        Terrain.Sand when hash % 17 == 0 => 6 + (int)((hash >> 8) % 2),
        _ => -1
    };

    public static float HeightFactor(uint hash) => 0.82f + (hash >> 15) % 30 * 0.012f;

    public static float RotationRadians(uint hash) =>
        (hash % 24) * (float)(System.Math.PI * 2.0 / 24.0);
}
