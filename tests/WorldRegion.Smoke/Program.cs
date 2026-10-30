using System;
using System.IO;
using System.Linq;
using WorldSim;
using WorldSim.Regions;

static void Check(bool yes, string name)
{
    if (!yes) throw new InvalidOperationException("FAIL " + name);
    Console.WriteLine("PASS " + name);
}

Check(RegionCoordinates.RegionOf(new CellCoord(-1, -1)) == new RegionKey(-1, -1),
    "negative world coordinates use floor division");
Check(RegionCoordinates.LocalOf(new CellCoord(-1, -1)) == new LocalCell(63, 63),
    "negative world coordinates wrap into positive local region coords");
Check(RegionCoordinates.RegionOf(new CellCoord(64, 64)) == new RegionKey(1, 1) &&
      RegionCoordinates.LocalOf(new CellCoord(64, 64)) == new LocalCell(0, 0),
    "positive region boundary");
Check(RegionCoordinates.RegionOf(new CellCoord(-64, -65)) == new RegionKey(-1, -2) &&
      RegionCoordinates.LocalOf(new CellCoord(-64, -65)) == new LocalCell(0, 63),
    "exact negative boundary and one cell beyond");
Check(RegionCoordinates.GlobalOf(new RegionKey(-2, 3), new LocalCell(63, 0)) ==
      new CellCoord(-65, 192), "round-trip absolute coordinate");
Check(RegionCoordinates.RegionOf(new CellCoord(8191, 8191)) == new RegionKey(127,127),
    "8192-wide map supports 128 regions without full array allocation");

var store = new RegionStore(20261030, 2);
var other = new RegionStore(20261030, 2);
var coords = new CellCoord(63, 63);
Check(store.Tile(coords) == other.Tile(coords), "deterministic generation regardless of store instance");

var changed = store.Tile(coords) == Terrain.Mountain ? Terrain.Grass : Terrain.Mountain;
store.SetTile(coords, changed);
Check(store.Tile(coords) == changed, "edits visible immediately");
RegionSnapshot snap = store.ReadRegion(RegionCoordinates.RegionOf(coords));
Check(snap.Tile(RegionCoordinates.LocalOf(coords)) == changed && snap.Revision > 0,
    "snapshot has immutable revision and tile");
// Mutating returned copy cannot mutate original snapshot nor store.
byte[] detached = snap.CopyTiles();
detached[RegionCoordinates.LocalIndex(RegionCoordinates.LocalOf(coords))] = 99;
Check(snap.Tile(RegionCoordinates.LocalOf(coords)) == changed &&
      store.Tile(coords) == changed, "snapshots isolate tile arrays");

store.Tile(new CellCoord(2048, 2048));
store.Tile(new CellCoord(-8192, 8192));
Check(store.ResidentRegions <= 2, "bounded LRU resident region cache");
Check(store.Tile(coords) == changed, "sparse edits survive resident eviction");

var world = new RegionStore(111, 4);
var center = new CellCoord(63, 63);
Terrain newKind = Terrain.Burned;
world.PaintCircle(center, 2, newKind);
Check(world.Tile(new CellCoord(63,63)) == newKind &&
      world.Tile(new CellCoord(64,63)) == newKind &&
      world.Tile(new CellCoord(63,64)) == newKind &&
      world.Tile(new CellCoord(62,62)) == newKind,
      "one circular brush updates all 4 adjacent regions");
Check(world.ModifiedRegions == 4, "cross-border brush marks four independent regions");

var encoded = RegionCodec.Encode(world.ReadRegion(new RegionKey(1,1)));
Check(encoded.Length == RegionCodec.EncodedBytes, "fixed versioned region snapshot size");
RegionSnapshot decoded = RegionCodec.Decode(encoded);
Check(decoded.Key == new RegionKey(1,1) && decoded.Seed == 111 &&
      decoded.CopyTiles().SequenceEqual(world.ReadRegion(new RegionKey(1,1)).CopyTiles()),
      "region snapshot serializes reproducibly");

var restored = new RegionStore(111, 1);
restored.RestoreRegion(decoded);
Check(restored.ReadRegion(decoded.Key).CopyTiles().SequenceEqual(decoded.CopyTiles()),
      "restored snapshot survives cache re-generation");
restored.Tile(new CellCoord(4096, -4096)); // eviction
Check(restored.ReadRegion(decoded.Key).CopyTiles().SequenceEqual(decoded.CopyTiles()),
      "restored changes survive eviction even with one resident region");

byte[] bad = (byte[])encoded.Clone();
bad[bad.Length - 9] ^= 0x20;
bool detected = false;
try { RegionCodec.Decode(bad); } catch (InvalidDataException) { detected = true; }
Check(detected, "corrupted region bytes rejected by checksum");
detected = false;
try { RegionCodec.Decode(encoded.Take(200).ToArray()); } catch (InvalidDataException) { detected = true; }
Check(detected, "truncated region rejected");

var far = new RegionStore(222, maxResidentRegions: 3);
for (int i = 0; i < 120; i++)
    _ = far.Tile(new CellCoord(i * 8192L + 32, -i * 4096L));
Check(far.ResidentRegions <= 3 && far.ModifiedRegions == 0,
    "huge sparse coordinates do not allocate all intervening regions");

Console.WriteLine("PASS ALL WorldRegion region store and codec smoke tests");
