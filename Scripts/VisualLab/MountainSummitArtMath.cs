namespace WorldForge.VisualLab;

/// <summary>
/// Deterministic, Godot-free mountain-detail policy. The main elevation and
/// shared-corner terrain already form the mountain. Authored accent rocks must
/// NEVER create a flat slab sitting on a high ridge / summit.
/// </summary>
public static class MountainSummitArtMath
{
    public readonly record struct Accent(bool Visible, int Species, float Height, float Embed)
    {
        public static Accent None => new(false, -1, 0f, 0f);
    }

    /// <summary>Cheap conservative reject before any neighbor biome reads:
    /// never return false for a rock that Choose could make visible.</summary>
    public static bool MayHaveAccent(uint hash, int heightStep) =>
        heightStep is >= 0 and < 8 && (hash >> 7) % 100 < 32;

    /// <param name="hash">Existing seeded prop hash for this absolute cell.</param>
    /// <param name="heightStep">Exactly Hash(x,y,worldSeed) % 11 as used
    /// by VisualTerrain3D.HeightAt for mountain top elevation.</param>
    /// <param name="nonMountainNeighbors">Number of cardinal neighbors
    /// outside the mountain biome, from zero to four.</param>
    public static Accent Choose(uint hash, int heightStep, int nonMountainNeighbors)
    {
        if (heightStep is < 0 or > 10)
            throw new System.ArgumentOutOfRangeException(nameof(heightStep));
        if (nonMountainNeighbors is < 0 or > 4)
            throw new System.ArgumentOutOfRangeException(nameof(nonMountainNeighbors));

        // A clear, continuous bare summit silhouette. No giant, flat-topped
        // rock asset can visually cap the actual generated mountain peak.
        if (heightStep >= 8) return Accent.None;

        // Lower exposed flank: a few larger, still modest upright shards.
        // Interior middle: much fewer, smaller detail accents.
        // Near ridge: exceptionally rare and tiny, never a large plate.
        int frequency = heightStep >= 6 ? 5 :
            nonMountainNeighbors >= 2 ? 32 :
            nonMountainNeighbors == 1 ? 26 : 18;
        if ((hash >> 7) % 100 >= frequency) return Accent.None;

        float height = heightStep >= 6
            ? 0.17f + ((hash >> 17) % 5) * 0.010f
            : nonMountainNeighbors > 0
                ? 0.31f + ((hash >> 17) % 9) * 0.015f
                : 0.22f + ((hash >> 17) % 9) * 0.013f;

        // The ONLY eligible authored mountain model is the narrow upright
        // TallRock (species 7). The broad RockLargeA (6) remains available
        // for sand, but no longer forms a flattened lid atop mountains.
        const int uprightRock = 7;
        return new Accent(true, uprightRock, height, Embed: 0.055f);
    }
}
