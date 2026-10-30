using System;
using System.Linq;
using WorldSim;
using WorldSim.Regions;
using WorldForge.VisualLab;

static void Check(bool yes, string message)
{
    if (!yes) throw new InvalidOperationException("FAIL " + message);
    Console.WriteLine("PASS " + message);
}
static void AllTiles(ITerrainTileSource reference, ITerrainTileSource mirrored)
{
    for (int y = 0; y < reference.Height; y++)
    for (int x = 0; x < reference.Width; x++)
        if (reference.Tile(x,y) != mirrored.Tile(x,y))
            throw new InvalidOperationException($"Visual mismatch at {x},{y}: " +
                $"{reference.Tile(x,y)} vs {mirrored.Tile(x,y)}");
}
static void VisualData(ITerrainTileSource direct, ITerrainTileSource mirror)
{
    AllTiles(direct, mirror);
    byte[] a = WaterCoastMath.BuildMask(direct.Width, direct.Height,
        direct.Tile, direct.Seed);
    byte[] b = WaterCoastMath.BuildMask(mirror.Width, mirror.Height,
        mirror.Tile, mirror.Seed);
    Check(a.SequenceEqual(b), "coast foam, deep/shallow water shader mask byte-identical");

    for (int y=0; y<direct.Height; y+=3)
    for (int x=0; x<direct.Width; x+=3)
    {
        foreach (int u in new[]{0,1})
        foreach (int v in new[]{0,1})
            if (EnvironmentArtMath.CornerColor(x,y,u,v,direct.Width,
                direct.Height,direct.Tile,direct.Seed) !=
                EnvironmentArtMath.CornerColor(x,y,u,v,mirror.Width,
                mirror.Height,mirror.Tile,mirror.Seed))
                throw new InvalidOperationException("Biome vertex color mismatch.");
    }
    Check(true, "deterministic biome vertex palette identical");
    static float Height(ITerrainTileSource s,int x,int y)
    {
        if (x<0 || y<0 || x>=s.Width || y>=s.Height) return .12f;
        return s.Tile(x,y) switch
        {
            Terrain.DeepWater or Terrain.ShallowWater => .12f,
            Terrain.Sand => .29f,
            Terrain.Grass => .61f,
            Terrain.Forest => .68f,
            Terrain.Burned => .53f,
            Terrain.Mountain => 2.6f +
               (EnvironmentArtMath.Hash(x,y,s.Seed)%11)*.17f,
            _ => .6f
        };
    }
    for (int y=0; y<direct.Height; y+=2)
    for (int x=0; x<direct.Width; x+=2)
    {
        ITerrainTileSource[] sources=[direct,mirror];
        float[] heights=new float[2];
        for (int i=0;i<2;i++)
        {
            var src=sources[i];
            if(src.Tile(x,y) is Terrain.DeepWater or Terrain.ShallowWater)
            { heights[i]=.12f; continue; }
            var fan=SharedCornerHeightfield.Tile(x,y,src.Width,src.Height,
                (xx,yy)=>Height(src,xx,yy),
                TerrainReliefMath.CenterLift(src.Tile(x,y),x,y,src.Seed));
            heights[i]=fan.Sample(.31f,.67f);
        }
        if(MathF.Abs(heights[0]-heights[1]) > .0000001f)
            throw new InvalidOperationException("Same renderer terrain surface mismatch.");
    }
    Check(true, "shared-corner heightfields and pick heights identical");
}

foreach(int seed in new[]{20261030, 99, -123456})
{
    var world=new WorldState(120,88,seed);
    var direct=new DirectWorldTerrainSource(world);
    // Only one cache region; repeated full sweeps force eviction/recreation,
    // revealing bugs where the procedural RegionGenerator leaks into V2.
    var mirror=new RegionMirrorTerrainSource(world, maxResidentRegions:1);
    Check(mirror.ResidentRegions==0, "mirror lazily starts with zero cached regions");
    VisualData(direct,mirror);
    Check(mirror.ResidentRegions<=1, "region-cache residency bounded to one");
    world.Paint(63,63,5,Terrain.Forest);
    mirror.SynchronizeArea(63,63,5);
    world.Paint(64,64,6,Terrain.DeepWater);
    mirror.SynchronizeArea(64,64,6);
    world.Meteor(31,32,8);
    mirror.SynchronizeArea(31,32,8);
    world.Paint(119,87,2,Terrain.Sand);
    mirror.SynchronizeArea(119,87,2);
    VisualData(direct,mirror);
    for(int y=0;y<88;y+=8)
    for(int x=0;x<120;x+=8)
        _=mirror.Tile(x,y);
    VisualData(direct,mirror);
    Check(mirror.ResidentRegions<=1, "no changed terrain lost after region LRU eviction");
    // A fresh generator from a different worldseed must not masquerade as V2.
    bool different = false;
    for(int y=0;y<88 && !different;y+=7)
    for(int x=0;x<120 && !different;x+=7)
        if(RegionGenerator.TerrainAt(new CellCoord(x,y),seed)!=world.Tile(x,y))
            different=true;
    Check(different, "separate V3 generator differs: never replace V2 biomes");
}
// No simulated people data is touched by the terrain adapter; each world
// remains the single authoritative owner of agents, movement and RNG.
var game=new WorldState(120,88,20261030);
var mirrorGameplay=new RegionMirrorTerrainSource(game,2);
var baseline=new WorldState(120,88,20261030);
for(int y=0;y<baseline.Height;y++)
for(int x=0;x<baseline.Width;x++)
{
    if(baseline.Spawn(x,y))
    {
        Check(game.Spawn(x,y),"same person spawn success in source-parity game");
        y=baseline.Height; break;
    }
}
for(int tick=0;tick<150;tick++)
{
    if(tick%9==0)
       _=mirrorGameplay.Tile(tick%120,(tick*11)%88);
    baseline.Advance();game.Advance();
}
Check(baseline.Tick==game.Tick &&
      baseline.People.Select(p=>(p.Id,p.X,p.Y)).SequenceEqual(
        game.People.Select(p=>(p.Id,p.X,p.Y))),
      "region visual cache does not change citizen simulation or RNG");
Console.WriteLine("PASS ALL exact V2 visual data, region eviction, water, geometry and game parity tests");
