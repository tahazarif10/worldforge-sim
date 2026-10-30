using WorldSim;

static void Assert(bool ok, string description)
{
    if (!ok) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}

WorldState a = new(160, 120, 1234);
WorldState b = new(160, 120, 1234);
WorldState c = new(160, 120, 9999);
Assert(Enumerable.Range(0, a.Width * a.Height).All(i => a.Tile(i % a.Width, i / a.Width) == b.Tile(i % b.Width, i / b.Width)), "same seed same map");
Assert(Enumerable.Range(0, a.Width * a.Height).Any(i => a.Tile(i % a.Width, i / a.Width) != c.Tile(i % c.Width, i / c.Width)), "different seeds change map");
a.Paint(20, 20, 2, Terrain.Grass);
Assert(a.Tile(20, 20) == Terrain.Grass, "paint land");
Assert(a.Spawn(20, 20), "spawn on land");
Assert(!a.Spawn(-1, 3), "reject out-of-bounds spawn");
a.Meteor(20, 20, 2);
Assert(a.People.Count == 0 && a.Tile(20, 20) == Terrain.Burned, "meteor destroys citizens and burns land");
for (int i = 0; i < 60; i++) a.Advance();
Assert(a.Tick == 60, "advance fixed ticks");
Console.WriteLine("PASS ALL simulation smoke tests");
