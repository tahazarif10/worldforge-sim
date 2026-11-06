using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using WorldSim.Regions;

namespace WorldSim;

public sealed class ElevationSaveRegion
{
    public long X { get; set; }
    public long Y { get; set; }
    public long Revision { get; set; }
    public short[] Units { get; set; }=[];
}

public sealed class LivingWorldSave
{
    public int Version { get; set; }=1;
    public WorldSaveState World { get; set; }=new();
    public List<ElevationSaveRegion> Elevation { get; set; }=[];
}

internal sealed class VerifiedSaveEnvelope
{
    public int Version { get; set; }=1;
    public string Sha256 { get; set; }="";
    public string DataBase64 { get; set; }="";
}

/// <summary>
/// A single atomic, checksummed checkpoint with one backup. Contains the
/// complete logical world AND its editable height deltas so a loaded
/// foundation cannot move or silently disappear after restart.
/// </summary>
public static class WorldSaveFiles
{
    private const int MaxBytes=64*1024*1024;

    public static void Write(string file,LivingWorldSave snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(snapshot);
        Verify(snapshot);
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(snapshot);
        var envelope=new VerifiedSaveEnvelope
        {
            Sha256=Convert.ToHexString(SHA256.HashData(data)),
            DataBase64=Convert.ToBase64String(data)
        };
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(envelope);
        if(bytes.Length>MaxBytes)
            throw new InvalidDataException("World save exceeds the bounded file limit.");
        string full=Path.GetFullPath(file);
        string directory=Path.GetDirectoryName(full) ??
            throw new InvalidDataException("Save has no parent directory.");
        Directory.CreateDirectory(directory);
        string temp=full+".tmp";
        using(var output=new FileStream(temp,FileMode.Create,
            FileAccess.Write,FileShare.None))
        {
            output.Write(bytes);
            output.Flush(flushToDisk:true);
        }
        // Never truncate the previous successful save before a new one is
        // durably written. The .bak is retained for corruption recovery.
        if(File.Exists(full))
        {
            // Never replace a healthy backup with an already-corrupt primary.
            // A fresh, verified successful save will become the new primary.
            try
            {
                _=ReadAndVerify(full,snapshot.World.Seed,
                    snapshot.World.Width,snapshot.World.Height);
                File.Copy(full,full+".bak",overwrite:true);
            }
            catch(Exception e) when(e is InvalidDataException or IOException or
                JsonException or FormatException or ArgumentException)
            {
                // Preserve the previously verified backup.
            }
        }
        File.Move(temp,full,overwrite:true);
    }

    public static LivingWorldSave Read(string file,int seed,int width,int height,
        out bool usedBackup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        string full=Path.GetFullPath(file);
        usedBackup=false;
        try
        {
            return ReadAndVerify(full,seed,width,height);
        }
        catch(Exception e) when(e is InvalidDataException or IOException or
            UnauthorizedAccessException or JsonException or
            FormatException or ArgumentException)
        {
            if(!File.Exists(full+".bak"))throw;
            LivingWorldSave safe=ReadAndVerify(full+".bak",seed,width,height);
            usedBackup=true;
            return safe;
        }
    }

    private static LivingWorldSave ReadAndVerify(string file,
        int seed,int width,int height)
    {
        var info=new FileInfo(file);
        if(info.Length<1 || info.Length>MaxBytes)
            throw new InvalidDataException("Unexpected world save size.");
        VerifiedSaveEnvelope envelope=JsonSerializer.Deserialize<VerifiedSaveEnvelope>(
            File.ReadAllBytes(file)) ??
                throw new InvalidDataException("Empty world save envelope.");
        if(envelope.Version!=1 || envelope.DataBase64 is null ||
            envelope.Sha256 is null)
            throw new InvalidDataException("Unknown world save envelope.");
        byte[] payload=Convert.FromBase64String(envelope.DataBase64);
        if(!string.Equals(Convert.ToHexString(SHA256.HashData(payload)),
            envelope.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("World save checksum mismatch.");
        LivingWorldSave snapshot=JsonSerializer.Deserialize<LivingWorldSave>(payload) ??
            throw new InvalidDataException("World save has no payload.");
        if(snapshot.World is null || snapshot.World.Seed!=seed ||
            snapshot.World.Width!=width || snapshot.World.Height!=height)
            throw new InvalidDataException("World save has a different world seed or map size.");
        Verify(snapshot);
        return snapshot;
    }

    public static void Verify(LivingWorldSave snapshot)
    {
        if(snapshot.Version!=1 || snapshot.World is null ||
            snapshot.Elevation is null || snapshot.Elevation.Count>256)
            throw new InvalidDataException("Unsupported or oversized world save.");
        _=WorldState.RestoreSave(snapshot.World);
        var used=new HashSet<RegionKey>();
        foreach(ElevationSaveRegion region in snapshot.Elevation)
        {
            if(region is null || region.Units is null ||
                !used.Add(new RegionKey(region.X,region.Y)))
                throw new InvalidDataException("Duplicate or malformed elevation region.");
            _=new ElevationRegionSnapshot(new RegionKey(region.X,region.Y),
                snapshot.World.Seed,region.Revision,region.Units);
        }
    }
}
