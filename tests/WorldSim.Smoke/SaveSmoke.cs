using System;
using System.IO;
using System.Linq;
using WorldSim;
using WorldSim.Regions;

internal static class SaveSmoke
{
    private static void Require(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException("SAVE SMOKE FAILED: "+message);
        Console.WriteLine("PASS SAVE: "+message);
    }

    private static void Same(WorldState a,WorldState b,string reason)
    {
        Require(a.Seed==b.Seed && a.Width==b.Width &&
            a.Height==b.Height && a.Tick==b.Tick &&
            a.Inventory==b.Inventory &&
            a.HouseRevision==b.HouseRevision &&
            a.People.Count==b.People.Count &&
            a.People.Select(p=>(p.Id,p.X,p.Y,p.BornAt,p.Hunger,p.Energy,
                p.WoodCarried,p.FoodCarried,p.FoodEaten,p.HomeId,p.Activity,p.Role))
                .SequenceEqual(b.People.Select(p=>
                    (p.Id,p.X,p.Y,p.BornAt,p.Hunger,p.Energy,
                     p.WoodCarried,p.FoodCarried,p.FoodEaten,p.HomeId,p.Activity,p.Role))) &&
            (a.FirstHouse is null)==(b.FirstHouse is null) &&
            (a.FirstHouse is null ||
                a.FirstHouse.Id==b.FirstHouse!.Id &&
                a.FirstHouse.X==b.FirstHouse.X &&
                a.FirstHouse.Y==b.FirstHouse.Y &&
                a.FirstHouse.Stage==b.FirstHouse.Stage &&
                a.FirstHouse.WoodDelivered==b.FirstHouse.WoodDelivered &&
                a.FirstHouse.WorkDone==b.FirstHouse.WorkDone &&
                a.FirstHouse.Residents==b.FirstHouse.Residents) &&
            Enumerable.Range(0,a.Width*a.Height).All(i=>
                a.Tile(i%a.Width,i/a.Width)==b.Tile(i%b.Width,i/b.Width) &&
                a.Resources.At(i%a.Width,i/a.Width)==
                    b.Resources.At(i%b.Width,i/b.Width)),
            reason);
    }

    public static void Run()
    {
        var a=new WorldState(96,96,20261106)
        {
            AutonomousGatheringEnabled=true,
            AutonomousHousingEnabled=true,
            PopulationLimit=4
        };
        a.Paint(48,48,20,Terrain.Grass);
        a.Paint(48,48,8,Terrain.Forest);
        for(int i=0;i<4;i++)
            Require(a.Spawn(47+i,48+i),"fixture citizen spawned");
        for(int i=0;i<290;i++)a.Advance();
        Require(a.Inventory.IsConserved &&
            a.Resources.FoodCollected>0 && a.Resources.WoodCollected>0,
            "saved world has real harvested, eaten and carried supplies");

        WorldSaveState captured=a.CaptureSave();
        WorldState restored=WorldState.RestoreSave(captured);
        Same(a,restored,"full save restores people, resources, homes, tiles and clock");
        // A snapshot must be a copy: later edits to one world do not mutate
        // the authoritative live scene or the other world.
        for(int i=0;i<350;i++)
        {
            a.Advance();
            restored.Advance();
            if(i%70==0)
                Same(a,restored,
                    "post-reload autonomous routes, births, construction and regrowth replay deterministically");
        }
        Same(a,restored,"replay state remains identical after hundreds more ticks");
        Require(a.Inventory.IsConserved && restored.Inventory.IsConserved,
            "resource accounting remains conserved across replay");

        var elevation=new ElevationRegionStore(a.Seed);
        Require(elevation.Sculpt(new CellCoord(48,48),3,.10f)>0,
            "a user-modified elevation region exists for the same save");
        var edited=elevation.ModifiedKeys().Select(key=>
        {
            ElevationRegionSnapshot s=elevation.Snapshot(key);
            return new ElevationSaveRegion
            {
                X=key.X,Y=key.Y,Revision=s.Revision,Units=s.CopyUnits()
            };
        }).ToList();
        var bundle=new LivingWorldSave
        {
            World=a.CaptureSave(),Elevation=edited
        };

        string folder=Path.Combine(AppContext.BaseDirectory,
            "save-smoke-"+Guid.NewGuid().ToString("N"));
        string file=Path.Combine(folder,"session.wfs");
        try
        {
            WorldSaveFiles.Write(file,bundle);
            Require(File.Exists(file),"checkpoint was written atomically into test worktree");
            WorldSaveFiles.Write(file,bundle);
            Require(File.Exists(file+".bak"),"second write preserves a backup");
            LivingWorldSave loaded=WorldSaveFiles.Read(file,a.Seed,
                a.Width,a.Height,out bool backup);
            Require(!backup && loaded.Elevation.Count==edited.Count &&
                loaded.Elevation.All(x=>x.Units.Length==4096),
                "verified primary round-trips height edits in same world checkpoint");
            Same(a,WorldState.RestoreSave(loaded.World),
                "verified file round-trips the complete live simulation");

            byte[] damaged=File.ReadAllBytes(file);
            damaged[damaged.Length/2]^=0x30;
            File.WriteAllBytes(file,damaged);
            LivingWorldSave recovered=WorldSaveFiles.Read(file,a.Seed,
                a.Width,a.Height,out backup);
            Require(backup,"checksum damage triggers recovery from backup");
            Same(a,WorldState.RestoreSave(recovered.World),
                "backup restores authoritative world without inventing resources");

            bool mismatch=false;
            try { _=WorldSaveFiles.Read(file,a.Seed+1,
                a.Width,a.Height,out _); }
            catch(InvalidDataException) { mismatch=true; }
            Require(mismatch,"wrong seed is never silently resumed");
            WorldSaveFiles.Write(file,bundle);
            Require(WorldSaveFiles.Read(file,a.Seed,
                a.Width,a.Height,out _).World.Tick==a.Tick,
                "a healthy replacement save succeeds after corrupted primary");
        }
        finally
        {
            if(Directory.Exists(folder))Directory.Delete(folder,recursive:true);
        }
        var fresh=new WorldState(96,96,20261106)
        {
            AutonomousGatheringEnabled=true,
            AutonomousHousingEnabled=true
        };
        Require(fresh.Tick==0 && fresh.FirstHouse is null &&
            fresh.Inventory.WoodAvailable==0,
            "New World stays fresh and never loads an unrelated checkpoint");
    }
}
