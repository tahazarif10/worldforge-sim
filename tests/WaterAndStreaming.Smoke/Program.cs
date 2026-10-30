using WorldSim;
using WorldForge.Streaming;
using WorldForge.VisualLab;

static void Check(bool yes, string name)
{
    if (!yes) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

const int size = 11;
Terrain[,] tiles = new Terrain[size,size];
for (int y=0; y<size; y++)
for (int x=0; x<size; x++) tiles[x,y] = Terrain.DeepWater;
tiles[5,5] = Terrain.Sand;
byte[] mask = WaterCoastMath.BuildMask(size,size,(x,y)=>tiles[x,y],101);
byte[] again = WaterCoastMath.BuildMask(size,size,(x,y)=>tiles[x,y],101);
static int Red(byte[] mask, int w, int x, int y) => mask[(y*w+x)*4];
static int Green(byte[] mask, int w, int x, int y) => mask[(y*w+x)*4+1];
Check(mask.SequenceEqual(again), "water distance image is seeded deterministic");
Check(mask.Length == size*size*4 &&
      mask.Where((v,i)=>i%4==3).All(v=>v==255),
      "opaque RGBA8 water mask dimensions");
Check(Red(mask,size,6,5) == 226 &&
      Red(mask,size,7,5) == 114 &&
      Red(mask,size,8,5) == 45 &&
      Red(mask,size,9,5) == 0,
      "smooth graded shore foam fades by 3 logical cells");
Check(Red(mask,size,5,5) == 0 &&
      Red(mask,size,6,6) == 114,
      "land has zero foam, diagonal Manhattan radius2 supported");
Check(Green(mask,size,5,5) == 0 &&
      Green(mask,size,0,0) == 28,
      "land excluded and deep sea tint correct");
tiles[5,5] = Terrain.ShallowWater;
byte[] afterPaint = WaterCoastMath.BuildMask(size,size,(x,y)=>tiles[x,y],101);
Check(Red(afterPaint,size,6,5) == 0 &&
      Green(afterPaint,size,5,5) == 216,
      "brush turns land to water, coast foam disappears");
tiles[5,5] = Terrain.Forest;
byte[] back = WaterCoastMath.BuildMask(size,size,(x,y)=>tiles[x,y],101);
Check(Red(back,size,6,5) == 226, "foam returns after restoring coast");
bool caught = false;
try { _ = WaterCoastMath.BuildMask(-1,1,(x,y)=>Terrain.DeepWater,1); }
catch (ArgumentOutOfRangeException) { caught=true; }
Check(caught, "invalid water dimensions rejected");
var large = WaterCoastMath.BuildMask(512,512,(x,y)=>x<256?Terrain.Forest:Terrain.DeepWater,10);
Check(large.Length == 512*512*4 && Red(large,512,256,100)==226,
      "512 square O(area) water mask correctly produces thin coastline");

var desired = VisibleChunkPlanner.Plan(8192,8192,64,3900,3900,4240,4240);
var active = new HashSet<(int X,int Y)>();
var selected = ChunkLoadBudget.Choose(desired,active,4090,4090,64,3);
Check(selected.Count == 3 && selected.All(desired.Contains),
      "large-world planner builds at most 3 nearest chunks per frame");
Check(selected[0] == (63,63) || selected[0] == (64,63) ||
      selected[0] == (63,64) || selected[0] == (64,64),
      "center-nearest missing region prioritized on camera teleport");
foreach (var key in selected) active.Add(key);
var next = ChunkLoadBudget.Choose(desired,active,4090,4090,64,3);
Check(next.All(k=>!active.Contains(k)), "resident chunks are not rebuilt");
Check(ChunkLoadBudget.Choose(desired,active,4090,4090,64,0).Count==0,
      "quality frame budget 0 avoids all new builds");
var identical = ChunkLoadBudget.Choose(desired,new HashSet<(int X,int Y)>(),
                                      4090,4090,64,3);
Check(identical.SequenceEqual(selected), "nearest-first order deterministic");
caught = false;
try { _=ChunkLoadBudget.Choose(desired,active,4090,4090,0,3); }
catch (ArgumentOutOfRangeException) { caught=true; }
Check(caught,"invalid chunk size rejected");

Console.WriteLine("PASS ALL water quality and chunk load-budget tests");
