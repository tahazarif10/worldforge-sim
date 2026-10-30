using System;
using WorldSim;
using WorldForge.VisualLab;

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

for (uint h = 0; h < 12000; h += 37)
{
    foreach (Terrain t in Enum.GetValues<Terrain>())
    {
        int id = NatureScatter.Pick(t, h);
        Check(id >= -1 && id < 10, "valid species index");
        if (t is Terrain.DeepWater or Terrain.ShallowWater or Terrain.Burned)
            Check(id == -1, "no trees or rocks on open water and burned tiles");
        Check(id == NatureScatter.Pick(t, h), "deterministic art variant");
        Check(NatureScatter.HeightFactor(h) >= 0.82f &&
              NatureScatter.HeightFactor(h) <= 1.18f,
              "bounded art height variation");
    }
}
var forest = new HashSet<int>();
var mountain = new HashSet<int>();
for (uint h = 0; h < 20000; h++)
{
    forest.Add(NatureScatter.Pick(Terrain.Forest, h));
    mountain.Add(NatureScatter.Pick(Terrain.Mountain, h));
}
Check(forest.Contains(-1) && Enumerable.Range(0, 6).All(forest.Contains),
    "all six authored tree species selected across forest");
Check(mountain.Contains(6) && mountain.Contains(7),
    "both authored rock species selected in mountains");
Console.WriteLine("PASS ALL authored nature scatter tests");
