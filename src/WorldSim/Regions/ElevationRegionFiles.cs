using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldSim.Regions;

/// <summary>
/// V1 little-endian, fixed-size, checksummed signed 1/256m height-delta
/// region file. No compression and no shader/Godot dependency.
/// </summary>
public static class ElevationRegionCodec
{
    private const uint Magic = 0x45484657; // "WFHE" in little-endian
    private const int Version = 1;
    private const int DataLength = RegionCoordinates.CellCount;
    public const int EncodedLength = 4 + 4 + 4 + 4 + 8 + 8 + 8 + 4 +
                                    DataLength * sizeof(short) + 8;

    public static byte[] Encode(ElevationRegionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream(EncodedLength);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(RegionCoordinates.Size);
        writer.Write(snapshot.Seed);
        writer.Write(snapshot.Key.X);
        writer.Write(snapshot.Key.Y);
        writer.Write(snapshot.Revision);
        writer.Write(DataLength);
        foreach (short unit in snapshot.CopyUnits()) writer.Write(unit);
        writer.Flush();
        ulong checksum = Checksum(stream.ToArray());
        writer.Write(checksum);
        writer.Flush();
        return stream.ToArray();
    }

    public static ElevationRegionSnapshot Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != EncodedLength)
            throw new InvalidDataException("Invalid elevation file size.");
        using var stream = new MemoryStream(data, writable:false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen:true);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version ||
            reader.ReadInt32() != RegionCoordinates.Size)
            throw new InvalidDataException("Unknown elevation region format.");
        int seed = reader.ReadInt32();
        var key = new RegionKey(reader.ReadInt64(), reader.ReadInt64());
        long revision = reader.ReadInt64();
        int length = reader.ReadInt32();
        if (revision < 0 || length != DataLength)
            throw new InvalidDataException("Invalid elevation metadata.");
        short[] values = new short[length];
        for (int i = 0; i < values.Length; i++) values[i] = reader.ReadInt16();
        ulong checksum = reader.ReadUInt64();
        if (checksum != Checksum(data.AsSpan(0,data.Length-8)))
            throw new InvalidDataException("Elevation checksum mismatch.");
        return new ElevationRegionSnapshot(key, seed, revision, values);
    }

    private static ulong Checksum(ReadOnlySpan<byte> bytes)
    {
        ulong h = 14695981039346656037UL;
        foreach (byte b in bytes) { h ^= b; h *= 1099511628211UL; }
        return h;
    }
}

/// <summary>
/// Explicit save/load, not background work. Writes temporary files beside
/// destination files then renames on same volume; verifies every file before
/// changing in-memory state on load. This is not a multi-region atomic
/// transaction; interrupted saves can contain differing region revisions.
/// </summary>
public static class ElevationRegionFiles
{
    public static string FileName(RegionKey key) =>
        $"height_{key.X}_{key.Y}.wfh";

    public static int SaveModified(ElevationRegionStore store, string directory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        int written = 0;
        foreach (RegionKey key in store.ModifiedKeys())
        {
            string file = Path.Combine(directory, FileName(key));
            string temp = file + ".tmp";
            byte[] data = ElevationRegionCodec.Encode(store.Snapshot(key));
            // Protect existing save if serialization or writing fails.
            using (var output = new FileStream(temp, FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                output.Write(data);
                output.Flush(flushToDisk: true);
            }
            // Same-directory rename; never delete old save before replacement.
            File.Move(temp, file, overwrite:true);
            written++;
        }
        return written;
    }

    public static int LoadAll(ElevationRegionStore store, string directory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) return 0;
        var snapshots = new List<ElevationRegionSnapshot>();
        var seen = new HashSet<RegionKey>();
        foreach (string file in Directory.EnumerateFiles(directory, "height_*.wfh"))
        {
            ElevationRegionSnapshot snapshot = ElevationRegionCodec.Decode(File.ReadAllBytes(file));
            if (snapshot.Seed != store.Seed)
                throw new InvalidDataException("World seed mismatch in elevation file.");
            if (!string.Equals(Path.GetFileName(file), FileName(snapshot.Key),
                StringComparison.Ordinal))
                throw new InvalidDataException("Elevation file name/key mismatch.");
            if (!seen.Add(snapshot.Key))
                throw new InvalidDataException("Duplicate elevation region.");
            snapshots.Add(snapshot);
        }
        // Failure above does not erase unsaved state. Only now commit.
        store.Clear();
        foreach (var snapshot in snapshots) store.Restore(snapshot);
        return snapshots.Count;
    }
}
