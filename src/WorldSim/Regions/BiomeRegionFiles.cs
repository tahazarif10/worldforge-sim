using System;
using System.Collections.Generic;
using System.IO;

namespace WorldSim.Regions;

/// <summary>
/// Explicit and bounded persistence for changed 64x64 biome regions.
/// All .wfr files are validated before any in-memory change. Each region
/// is replaced via same-directory write/flush/rename; this is NOT a
/// crash-atomic transaction over multiple files.
/// </summary>
public static class BiomeRegionFiles
{
    public static string FileName(RegionKey key) =>
        $"biome_{key.X}_{key.Y}.wfr";

    public static int SaveModified(RegionStore store, string folder)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Directory.CreateDirectory(folder);
        int count = 0;
        foreach (RegionKey key in store.ModifiedKeys())
        {
            string path = Path.Combine(folder, FileName(key));
            string pending = path + ".tmp";
            byte[] payload = RegionCodec.Encode(store.ReadRegion(key));
            using (var output = new FileStream(pending, FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                output.Write(payload);
                output.Flush(flushToDisk: true);
            }
            File.Move(pending, path, overwrite: true);
            count++;
        }
        return count;
    }

    public static int LoadAll(RegionStore store, string folder)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Directory.Exists(folder)) return 0;

        var staged = new List<RegionSnapshot>();
        var seen = new HashSet<RegionKey>();
        foreach (string file in Directory.EnumerateFiles(folder, "biome_*.wfr"))
        {
            RegionSnapshot snapshot = RegionCodec.Decode(File.ReadAllBytes(file));
            if (snapshot.Seed != store.Seed)
                throw new InvalidDataException("Biome world seed mismatch.");
            if (!string.Equals(Path.GetFileName(file), FileName(snapshot.Key),
                StringComparison.Ordinal))
                throw new InvalidDataException("Biome region filename/key mismatch.");
            if (!seen.Add(snapshot.Key))
                throw new InvalidDataException("Duplicate biome region in save.");
            staged.Add(snapshot);
        }

        // Preserve the previous in-memory state if any file was invalid.
        store.ClearEdits();
        foreach (RegionSnapshot snapshot in staged) store.RestoreRegion(snapshot);
        return staged.Count;
    }
}
