using System;
using System.IO;
using System.Text;

namespace WorldSim.Regions;

/// <summary>
/// Versioned portable little-endian region snapshot format.
/// Save/load callers must use atomic replace and an edit journal for reliable
/// persistent worlds; the byte codec alone is not a crash-safe save system.
/// </summary>
public static class RegionCodec
{
    private const uint Magic = 0x47524657; // 'WFRG' in little-endian bytes
    private const int Version = 1;
    private const int HeaderBytes = 4 + 4 + 4 + 8 + 8 + 4 + 8 + 4;
    private const int ChecksumBytes = 8;
    public const int EncodedBytes = HeaderBytes + RegionCoordinates.CellCount + ChecksumBytes;

    public static byte[] Encode(RegionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream(EncodedBytes);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(RegionCoordinates.Size);
        writer.Write(snapshot.Key.X);
        writer.Write(snapshot.Key.Y);
        writer.Write(snapshot.Seed);
        writer.Write(snapshot.Revision);
        writer.Write(RegionCoordinates.CellCount);
        writer.Write(snapshot.CopyTiles());
        writer.Flush();

        byte[] preChecksum = stream.ToArray();
        writer.Write(Checksum(preChecksum));
        writer.Flush();
        return stream.ToArray();
    }

    public static RegionSnapshot Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length != EncodedBytes)
            throw new InvalidDataException("Unexpected region snapshot byte count.");

        using var reader = new BinaryReader(new MemoryStream(encoded, writable: false),
            Encoding.UTF8, leaveOpen: false);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version ||
            reader.ReadInt32() != RegionCoordinates.Size)
            throw new InvalidDataException("Unsupported region format or version.");

        RegionKey key = new(reader.ReadInt64(), reader.ReadInt64());
        int seed = reader.ReadInt32();
        long revision = reader.ReadInt64();
        if (revision < 0 || reader.ReadInt32() != RegionCoordinates.CellCount)
            throw new InvalidDataException("Invalid revision or cell count.");
        byte[] cells = reader.ReadBytes(RegionCoordinates.CellCount);
        ulong recorded = reader.ReadUInt64();
        if (recorded != Checksum(encoded.AsSpan(0, encoded.Length - ChecksumBytes)))
            throw new InvalidDataException("Corrupted region: checksum mismatch.");

        return new RegionSnapshot(key, seed, revision, cells);
    }

    private static ulong Checksum(ReadOnlySpan<byte> data)
    {
        ulong state = 14695981039346656037UL; // FNV-1a
        foreach (byte b in data)
        {
            state ^= b;
            state *= 1099511628211UL;
        }
        return state;
    }
}
