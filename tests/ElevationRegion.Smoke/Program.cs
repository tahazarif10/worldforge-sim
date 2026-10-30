using System;
using System.IO;
using System.Linq;
using System.Numerics;
using WorldForge.Camera3D;
using WorldForge.VisualLab;
using WorldSim.Regions;

static void Check(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + message);
    Console.WriteLine("PASS " + message);
}
static bool Near(float a,float b)=>MathF.Abs(a-b)<.00001f;

var store = new ElevationRegionStore(20261030, maxResidentRegions:2);
Check(store.Offset(new CellCoord(10,10)) == 0f &&
      store.ResidentRegions == 0 && store.ModifiedRegions == 0,
      "unedited world reads without allocating any region");
Check(store.AddOffset(new CellCoord(63,63),.125f) &&
      store.AddOffset(new CellCoord(64,63),.125f) &&
      store.AddOffset(new CellCoord(63,64),.125f) &&
      store.AddOffset(new CellCoord(64,64),.125f),
      "4-way boundary heights edit independently");
Check(store.ModifiedRegions==4 && Near(store.Offset(new CellCoord(63,63)),.125f),
      "four neighbor region keys tracked across boundary");
Check(!store.AddOffset(new CellCoord(63,63),0f), "zero adjustment is no-op");
Check(store.SetOffset(new CellCoord(-1,-1),-.5f) &&
      Near(store.Offset(new CellCoord(-1,-1)),-.5f) &&
      store.ModifiedRegions==5,
      "negative coordinates correctly floor-divide into signed region keys");
Check(store.GetRevision(new RegionKey(-1,-1)) == 1,
      "region revision advances only after actual edits");

store.Prime(new RegionKey(0,0));
store.Prime(new RegionKey(1,0));
store.Prime(new RegionKey(10000,-10000));
Check(store.ResidentRegions == 2 && Near(store.Offset(new CellCoord(63,63)),.125f),
      "resident region LRU stays bounded and never loses sparse edits");
store.SetOffset(new CellCoord(-1,-1),0f);
Check(store.NonzeroRegions==4 && store.ModifiedRegions==5,
      "reverting a point to zero clears sparse value but retains dirty region");

var key = new RegionKey(0,0);
var original = store.Snapshot(key);
var detached = original.CopyUnits();
detached[RegionCoordinates.LocalIndex(new LocalCell(63,63))] = 1990;
Check(original.Units(new LocalCell(63,63))==32,
      "snapshot owns independent immutable copy of edited height");
byte[] wire=ElevationRegionCodec.Encode(original);
Check(wire.Length==ElevationRegionCodec.EncodedLength,
      "fixed-size elevation snapshot binary format");
var decoded=ElevationRegionCodec.Decode(wire);
Check(decoded.Key==key && decoded.Seed==20261030 &&
      decoded.CopyUnits().SequenceEqual(original.CopyUnits()),
      "codec round-trip retains exact height, world and revision");
byte[] corrupt=(byte[])wire.Clone();
corrupt[100] ^= 0x20;
bool rejected=false;
try { ElevationRegionCodec.Decode(corrupt); }
catch(InvalidDataException){rejected=true;}
Check(rejected,"checksum rejects altered serialized height");
rejected=false;
try{ElevationRegionCodec.Decode(wire[..100]);}
catch(InvalidDataException){rejected=true;}
Check(rejected,"truncated regional height file rejected");

int changed=store.Sculpt(new CellCoord(-64,64),3,.11f);
Check(changed>12 && changed<30 && store.ModifiedRegions>=5,
      "bounded radial elevation sculpt works across signed region boundaries");
Check(store.SetOffset(new CellCoord(2,2),1000f) &&
      Near(store.Offset(new CellCoord(2,2)),ElevationRegionStore.MaxAbsoluteOffset),
      "extreme user brush offset clamps to 8 world units");

var fresh = new ElevationRegionStore(20261030,1);
fresh.Restore(decoded);
Check(Near(fresh.Offset(new CellCoord(63,63)),.125f) &&
      fresh.GetRevision(key)==original.Revision,
      "snapshot restore retains edit and monotonic regional revision");

var sparse=new ElevationRegionStore(20261030,2);
for(int i=0;i<100;i++)
    sparse.SetOffset(new CellCoord(8192L*i + 4, -(8192L*i) - 5),.08f);
Check(sparse.ResidentRegions <= 2 && sparse.ModifiedRegions==100 &&
      Near(sparse.Offset(new CellCoord(8192L*99+4,-(8192L*99)-5)),.078125f),
      "large sparse signed coordinates allocate only edited data, no full world array");

string directory=Path.GetFullPath(Path.Combine("artifacts","test-elevation",
    "run-" + Guid.NewGuid().ToString("N")));
try
{
    int saved=ElevationRegionFiles.SaveModified(store,directory);
    Check(saved==store.ModifiedRegions &&
          Directory.GetFiles(directory,"height_*.wfh").Length==saved,
          "explicit on-disk snapshots contain only modified 64x64 regions");
    var recovered=new ElevationRegionStore(20261030,2);
    int loaded=ElevationRegionFiles.LoadAll(recovered,directory);
    Check(loaded==saved &&
          Near(recovered.Offset(new CellCoord(63,63)),.125f) &&
          Near(recovered.Offset(new CellCoord(64,63)),.125f) &&
          Near(recovered.Offset(new CellCoord(2,2)),8f),
          "multiple independently edited regions reload from disk faithfully");
    // An invalid region must be rejected before partially restoring any one.
    string badFile=Path.Combine(directory,"height_999_999.wfh");
    File.WriteAllBytes(badFile,corrupt);
    var guarded=new ElevationRegionStore(20261030,2);
    guarded.SetOffset(new CellCoord(333,333),.5f);
    bool refused=false;
    try { ElevationRegionFiles.LoadAll(guarded,directory); }
    catch(InvalidDataException){refused=true;}
    Check(refused && Near(guarded.Offset(new CellCoord(333,333)),.5f),
          "corrupt multi-region load rejected without modifying existing edits");
    File.Delete(badFile);
    var wrongSeed=new ElevationRegionStore(20261031,2);
    refused=false;
    try { ElevationRegionFiles.LoadAll(wrongSeed,directory); }
    catch(InvalidDataException){refused=true;}
    Check(refused,"world seed mismatch refuses loading another world's heights");
}
finally { if(Directory.Exists(directory)) Directory.Delete(directory,recursive:true); }
// The persisted offset belongs to the ABSOLUTE shared vertex, not to
// either neighboring chunk's private mesh. Both renderers and ray pickers
// must interpolate the same surface on x=16 (a 16x16 chunk boundary).
var sculpted = new ElevationRegionStore(77, 2);
sculpted.SetOffset(new CellCoord(16,20),.5f);
SharedCornerHeightfield.Fan Editable(int x, int y)
{
    var flat = SharedCornerHeightfield.Tile(x,y,80,80,(_,_)=>1f,0f);
    float a = sculpted.Offset(new CellCoord(x,y));
    float b = sculpted.Offset(new CellCoord(x,y+1));
    float cc = sculpted.Offset(new CellCoord(x+1,y));
    float d = sculpted.Offset(new CellCoord(x+1,y+1));
    return new SharedCornerHeightfield.Fan(flat.A+a,flat.B+b,flat.C+cc,
        flat.D+d,flat.Center+(a+b+cc+d)*.25f);
}
var west = Editable(15,20);
var east = Editable(16,20);
Check(Near(west.C,east.A) && Near(west.D,east.B) &&
      Near(west.C,1.5f),"edited corner agrees across 16-cell visual chunk boundary");
for(int v=0;v<=10;v++)
    Check(Near(west.Sample(1f,v/10f),east.Sample(0f,v/10f)),
        "entire sculpted edge matches across chunks");
for(int j=0;j<2;j++)
{
    float globalX=j==0?15.90f:16.10f;
    float localX=j==0?.90f:.10f;
    float expected=Editable(15+j,20).Sample(localX,.10f);
    bool hit=TerrainGridPicker.TryPick(new Vector3(globalX,12,20.1f),
        -Vector3.UnitY,80,80,(_,_)=>1f,out var point,
        (_,_)=>0f,(x,y,u,v)=>Editable(x,y).Sample(u,v));
    Check(hit && Near(point.Point.Y,expected) &&
          point.X==15+j && point.Y==20,
          "camera ray hits edited terrain triangle on both sides of region boundary");
}

Console.WriteLine("PASS ALL persistent regional elevation and recovery tests");
