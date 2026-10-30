using System;
using System.Linq;
using WorldSim;
using WorldForge.VisualLab;

static void Check(bool yes, string label)
{
    if (!yes) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}

var map = new Terrain[8, 8];
for (int y=0; y<8; y++)
for (int x=0; x<8; x++) map[x,y] = Terrain.DeepWater;
map[3, 3] = Terrain.Sand;
map[3, 4] = Terrain.Grass;
map[4, 4] = Terrain.Forest;
map[5, 4] = Terrain.Mountain;
static int Red(byte[] img, int w, int x, int y) => img[(y*w+x)*4];
static int Green(byte[] img, int w, int x, int y) => img[(y*w+x)*4+1];
byte[] first = EnvironmentArtMath.BuildShoreMask(8,8,(x,y)=>map[x,y],20261030);
byte[] again = EnvironmentArtMath.BuildShoreMask(8,8,(x,y)=>map[x,y],20261030);
Check(first.SequenceEqual(again), "shore mask deterministic");
Check(first.Length == 8*8*4 && first.Where((_,i)=>i%4==3).All(a=>a==255),
      "RGBA shoreline texture dimensions and opaque alpha");
Check(Red(first,8,3,2) > 0, "shore water has foam next to sand");
Check(Red(first,8,3,3) == 0 && Green(first,8,3,3) == 0,
      "land does not receive water shader foam");
Check(Red(first,8,0,0) == 0, "distant deep sea contains no coastline foam");
Check(Green(first,8,0,0) > 0, "deep sea gets dark-water mask class");
map[3,3] = Terrain.ShallowWater;
byte[] painted = EnvironmentArtMath.BuildShoreMask(8,8,(x,y)=>map[x,y],20261030);
Check(Red(painted,8,3,2) == 0, "coast foam disappears after land becomes water");
Check(Green(painted,8,3,3) > Green(painted,8,0,0),
      "new shallow water is brighter than deep ocean");

var exact = EnvironmentArtMath.CornerColor(3,4,1,0,8,8,(x,y)=>map[x,y],234);
var repeat = EnvironmentArtMath.CornerColor(3,4,1,0,8,8,(x,y)=>map[x,y],234);
Check(exact == repeat, "biome corner color deterministic");
Check(EnvironmentArtMath.Mix(new(0,0,0),new(255,255,255),0) ==
      new EnvironmentArtMath.Rgb(0,0,0), "mix zero remains source");
Check(EnvironmentArtMath.Mix(new(0,0,0),new(255,255,255),1) ==
      new EnvironmentArtMath.Rgb(255,255,255), "mix one reaches target");
Check(EnvironmentArtMath.Mix(new(0,0,0),new(255,255,255),0.5f).R == 128,
      "palette lerp uses rounded channel values");
Check(EnvironmentArtMath.GroundColor(Terrain.Mountain).Packed !=
      EnvironmentArtMath.GroundColor(Terrain.Forest).Packed,
      "rock and forest palettes distinct");
Check(!EnvironmentArtMath.Water(Terrain.Grass) &&
      EnvironmentArtMath.Water(Terrain.DeepWater) &&
      EnvironmentArtMath.Water(Terrain.ShallowWater),
      "water and ground classes classified correctly");

// City-scale, bounded test. No full-world allocation beyond the tiny input.
var large = new WorldState(80, 64, 510);
var mask = EnvironmentArtMath.BuildShoreMask(large.Width,large.Height,large.Tile,large.Seed);
Check(mask.Length == 80*64*4, "current authoritative WorldState accepted");

bool invalid = false;
try { EnvironmentArtMath.BuildShoreMask(0,2,(x,y)=>Terrain.Grass,1); }
catch (ArgumentOutOfRangeException) { invalid = true; }
Check(invalid, "invalid mask extent rejected");
Console.WriteLine("PASS ALL environment art and dynamic shoreline tests");
