using System;
using System.IO;
using System.Linq;
using WorldSim;
using WorldSim.Regions;
using WorldForge.Streaming;

static void Check(bool ok,string name)
{
    if(!ok)throw new InvalidOperationException("FAIL "+name);
    Console.WriteLine("PASS "+name);
}

var world=new RegionStore(20261030,maxResidentRegions:5);
Check(world.ResidentRegions==0,"new region world allocates no terrain");
Check(world.Tile(new CellCoord(4096,4096))==
      RegionGenerator.TerrainAt(new CellCoord(4096,4096),20261030),
      "absolute region generator deterministic across 8192 world");
Check(world.ResidentRegions==1,"only visited biome region allocated");
world.SetTile(new CellCoord(63,63),Terrain.Mountain);
world.SetTile(new CellCoord(64,63),Terrain.Forest);
world.SetTile(new CellCoord(63,64),Terrain.Grass);
world.SetTile(new CellCoord(64,64),Terrain.Sand);
Check(world.ModifiedKeys().Count<=4&&world.ModifiedKeys().Count>=1,
      "cross-region edits track their touched region keys");
world.SetTile(new CellCoord(-1,-1),Terrain.Burned);
Check(world.Tile(new CellCoord(-1,-1))==Terrain.Burned,
      "negative-coordinate edits remain valid for future infinite-world setup");
world.PaintCircle(new CellCoord(8191,8191),2,Terrain.Sand);
Check(world.Tile(new CellCoord(8191,8191))==Terrain.Sand,
      "large world edge painting works without full tile array");
for(long n=0;n<300;n++)
    _=world.Tile(new CellCoord(n*8192L+7,-n*8192L-9));
Check(world.ResidentRegions<=5,"LRU biomes stay bounded after 300 distant regions");
Check(world.Tile(new CellCoord(63,63))==Terrain.Mountain &&
      world.Tile(new CellCoord(-1,-1))==Terrain.Burned,
      "sparse edits survive eviction and revisiting");

var view=VisibleChunkPlanner.Plan(8192,8192,16,4080,4080,4208,4208,1);
Check(view.Count<=150 && view.Count>0,
      "8192² world only plans small local view footprint");
var active=new HashSet<(int X,int Y)>();
var planned=ChunkLoadBudget.Choose(view,active,4096,4096,16,2);
Check(planned.Count==2,"Eco limits new visible chunks to two per update");
foreach(var key in planned)active.Add(key);
Check(ChunkLoadBudget.Choose(view,active,4096,4096,16,2)
    .All(x=>!active.Contains(x)),"next frame skips resident chunks");
Check(VisibleChunkPlanner.Plan(8192,8192,16,80,80,140,140)
      .Overlaps(view)==false,"teleport doesn't need intervening chunks");

string root=Path.GetFullPath(Path.Combine("artifacts","regional-world-smoke",
    Guid.NewGuid().ToString("N")));
try
{
    int expected=world.ModifiedKeys().Count;
    int saved=BiomeRegionFiles.SaveModified(world,root);
    Check(saved==expected && saved>0 &&
          Directory.GetFiles(root,"biome_*.wfr").Length==saved,
          "save stores only edited regions");
    var copy=new RegionStore(20261030,3);
    int loaded=BiomeRegionFiles.LoadAll(copy,root);
    Check(loaded==saved &&
          copy.Tile(new CellCoord(63,63))==Terrain.Mountain &&
          copy.Tile(new CellCoord(-1,-1))==Terrain.Burned,
          "edited biomes round-trip across multiple regions");
    // One file is corrupt: reject entire recovery before wiping existing state.
    string poison=Path.Combine(root,"biome_999_999.wfr");
    File.WriteAllBytes(poison,new byte[]{1,2,3});
    var original=new RegionStore(20261030,2);
    original.SetTile(new CellCoord(700,700),Terrain.Mountain);
    bool rejected=false;
    try{BiomeRegionFiles.LoadAll(original,root);}
    catch(InvalidDataException){rejected=true;}
    Check(rejected&&original.Tile(new CellCoord(700,700))==Terrain.Mountain,
        "corrupt region rejected without losing unsaved edits");
    File.Delete(poison);
    var wrongSeed=new RegionStore(88,2);
    rejected=false;
    try{BiomeRegionFiles.LoadAll(wrongSeed,root);}
    catch(InvalidDataException){rejected=true;}
    Check(rejected,"wrong-world seed cannot import biome saves");
    var bad=world.ReadRegion(new RegionKey(0,0)).CopyTiles();
    bad[0]=255;
    rejected=false;
    try{_=new RegionSnapshot(new RegionKey(0,0),20261030,0,bad);}
    catch(ArgumentException){rejected=true;}
    Check(rejected,"invalid terrain values are refused before renderer receives them");
    // Restore clears old edits only after all files have passed validation.
    var reset= new RegionStore(20261030,1);
    reset.SetTile(new CellCoord(1121,1121),Terrain.Burned);
    BiomeRegionFiles.LoadAll(reset,root);
    Check(reset.Tile(new CellCoord(1121,1121))==
          RegionGenerator.TerrainAt(new CellCoord(1121,1121),20261030),
          "successful load replaces stale unsaved state");
}
finally{if(Directory.Exists(root))Directory.Delete(root,true);}
Console.WriteLine("PASS ALL regional-world 8192 logical navigation, LRU and biome persistence tests");
