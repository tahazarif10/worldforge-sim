using System;
using System.Collections.Generic;

namespace WorldSim.Regions;

/// <summary>
/// A signed, quantized delta (1/256 world unit) at each absolute terrain
/// lattice vertex, independent of biome. This first-stage overlay preserves
/// existing worlds' terrain until a user edits height.
/// </summary>
public sealed class ElevationRegionSnapshot
{
    private readonly short[] _units;
    public RegionKey Key { get; }
    public int Seed { get; }
    public long Revision { get; }

    public ElevationRegionSnapshot(RegionKey key, int seed, long revision, short[] units)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (revision < 0 || units.Length != RegionCoordinates.CellCount)
            throw new ArgumentException("Invalid elevation snapshot size or revision.");
        Key = key;
        Seed = seed;
        Revision = revision;
        _units = (short[])units.Clone();
    }

    public short Units(LocalCell local) =>
        local.X is < 0 or >= RegionCoordinates.Size ||
        local.Y is < 0 or >= RegionCoordinates.Size
            ? throw new ArgumentOutOfRangeException(nameof(local))
            : _units[RegionCoordinates.LocalIndex(local)];

    public short[] CopyUnits() => (short[])_units.Clone();
}

/// <summary>
/// In-process LRU for nonzero vertex deltas. Unedited vertices require NO
/// allocated region. Edits survive resident cache eviction in sparse maps.
/// Persist explicitly through ElevationRegionFiles; not a background I/O
/// worker or the final per-region streaming simulation.
/// </summary>
public sealed class ElevationRegionStore
{
    public const float UnitsPerMetre = 256f;
    public const float MaxAbsoluteOffset = 8f;
    private const short MaxUnits = (short)(8 * 256);

    private sealed class Resident(short[] units, LinkedListNode<RegionKey> node)
    {
        public short[] Units { get; } = units;
        public LinkedListNode<RegionKey> Node { get; } = node;
    }

    private readonly Dictionary<RegionKey, Resident> _resident = new();
    private readonly LinkedList<RegionKey> _lru = new();
    private readonly Dictionary<RegionKey, Dictionary<int, short>> _nonzero = new();
    private readonly Dictionary<RegionKey, long> _revisions = new();
    private readonly HashSet<RegionKey> _modified = new();

    public int Seed { get; }
    public int MaxResidentRegions { get; }
    public int ResidentRegions => _resident.Count;
    public int ModifiedRegions => _modified.Count;
    public int NonzeroRegions => _nonzero.Count;

    public ElevationRegionStore(int seed, int maxResidentRegions = 16)
    {
        if (maxResidentRegions is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(maxResidentRegions));
        Seed = seed;
        MaxResidentRegions = maxResidentRegions;
    }

    public float Offset(CellCoord vertex) => Units(vertex) / UnitsPerMetre;

    public short Units(CellCoord vertex)
    {
        RegionKey key = RegionCoordinates.RegionOf(vertex);
        int index = RegionCoordinates.LocalIndex(RegionCoordinates.LocalOf(vertex));
        // No region materialization for unedited terrain.
        if (_resident.TryGetValue(key, out Resident? entry))
        {
            Touch(entry);
            return entry.Units[index];
        }
        if (_nonzero.TryGetValue(key, out Dictionary<int, short>? changes) &&
            changes.TryGetValue(index, out short value)) return value;
        return 0;
    }

    public bool AddOffset(CellCoord vertex, float metres)
    {
        if (!float.IsFinite(metres)) throw new ArgumentOutOfRangeException(nameof(metres));
        int increment = (int)MathF.Round(metres * UnitsPerMetre);
        if (increment == 0) return false;
        long candidate = (long)Units(vertex) + increment;
        return SetUnits(vertex, (short)Math.Clamp(candidate, -MaxUnits, MaxUnits));
    }

    public bool SetOffset(CellCoord vertex, float metres)
    {
        if (!float.IsFinite(metres)) throw new ArgumentOutOfRangeException(nameof(metres));
        short value = (short)Math.Clamp((int)MathF.Round(
            Math.Clamp(metres, -MaxAbsoluteOffset, MaxAbsoluteOffset) * UnitsPerMetre),
            -MaxUnits, MaxUnits);
        return SetUnits(vertex, value);
    }

    private bool SetUnits(CellCoord vertex, short next)
    {
        RegionKey key = RegionCoordinates.RegionOf(vertex);
        int index = RegionCoordinates.LocalIndex(RegionCoordinates.LocalOf(vertex));
        if (Units(vertex) == next) return false;
        if (!_nonzero.TryGetValue(key, out Dictionary<int, short>? edits))
        {
            edits = new Dictionary<int, short>();
            _nonzero.Add(key, edits);
        }
        if (next == 0)
        {
            edits.Remove(index);
            if (edits.Count == 0) _nonzero.Remove(key);
        }
        else edits[index] = next;

        if (_resident.TryGetValue(key, out Resident? entry))
            entry.Units[index] = next;
        _revisions[key] = GetRevision(key) + 1;
        _modified.Add(key);
        return true;
    }

    /// <summary>Bounded O(radius²) absolute vertex brush. Returns actual
    /// edited vertex count. Handles negative coordinates with floor division.</summary>
    public int Sculpt(CellCoord center, int radius, float metres)
    {
        if (radius is < 0 or > 24) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(metres) || MathF.Abs(metres) > 1f)
            throw new ArgumentOutOfRangeException(nameof(metres));
        int count = 0;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            float strength = radius == 0 ? 1f :
                1f - 0.70f * (dx * dx + dy * dy) / (float)(radius * radius);
            if (AddOffset(new CellCoord(checked(center.X + dx),
                                       checked(center.Y + dy)), metres * strength))
                count++;
        }
        return count;
    }

    public long GetRevision(RegionKey key) =>
        _revisions.TryGetValue(key, out long value) ? value : 0;

    public IReadOnlyList<RegionKey> ModifiedKeys()
    {
        var keys = new List<RegionKey>(_modified);
        keys.Sort((a,b) =>
        {
            int compare = a.Y.CompareTo(b.Y);
            return compare == 0 ? a.X.CompareTo(b.X) : compare;
        });
        return keys;
    }

    public ElevationRegionSnapshot Snapshot(RegionKey key)
    {
        short[] units = new short[RegionCoordinates.CellCount];
        if (_nonzero.TryGetValue(key, out Dictionary<int, short>? edits))
            foreach (var edit in edits) units[edit.Key] = edit.Value;
        // Only allocate resident cache for snapshots of genuinely edited regions.
        return new ElevationRegionSnapshot(key, Seed, GetRevision(key), units);
    }

    public void Restore(ElevationRegionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Seed != Seed) throw new ArgumentException("World seed mismatch.", nameof(snapshot));
        short[] all = snapshot.CopyUnits();
        var changes = new Dictionary<int, short>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is < -MaxUnits or > MaxUnits)
                throw new ArgumentException("Height offset outside permitted range.", nameof(snapshot));
            if (all[i] != 0) changes[i] = all[i];
        }
        if (changes.Count == 0) _nonzero.Remove(snapshot.Key);
        else _nonzero[snapshot.Key] = changes;
        _revisions[snapshot.Key] = snapshot.Revision;
        _modified.Add(snapshot.Key);
        if (_resident.TryGetValue(snapshot.Key, out Resident? entry))
            Array.Copy(all, entry.Units, all.Length);
    }

    public void Clear()
    {
        _resident.Clear();
        _lru.Clear();
        _nonzero.Clear();
        _revisions.Clear();
        _modified.Clear();
    }

    public void Prime(RegionKey key)
    {
        if (_resident.TryGetValue(key, out Resident? existing))
        {
            Touch(existing);
            return;
        }
        var units = new short[RegionCoordinates.CellCount];
        if (_nonzero.TryGetValue(key, out Dictionary<int, short>? edits))
            foreach (var item in edits) units[item.Key] = item.Value;
        var node = _lru.AddFirst(key);
        _resident[key] = new Resident(units, node);
        while (_resident.Count > MaxResidentRegions)
        {
            RegionKey last = _lru.Last!.Value;
            _lru.RemoveLast();
            _resident.Remove(last);
        }
    }

    private void Touch(Resident entry)
    {
        _lru.Remove(entry.Node);
        _lru.AddFirst(entry.Node);
    }
}
