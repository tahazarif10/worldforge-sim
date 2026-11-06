using System;
using System.Collections.Generic;
using System.IO;

namespace WorldSim;

public sealed partial class WorldState
{
    private uint NextAiUint()
    {
        uint x=_aiPopulationRandomState;
        x^=x<<13;
        x^=x>>17;
        x^=x<<5;
        _aiPopulationRandomState=x;
        return x;
    }
    private int NextAiInt(int max) => checked((int)(NextAiUint()%(uint)max));
    private double NextAiFraction() => NextAiUint()/(double)uint.MaxValue;

    public WorldSaveState CaptureSave()
    {
        if(!AutonomousGatheringEnabled)
            throw new InvalidOperationException(
                "Replayable save is supported only in the opted-in living-world simulation.");
        if(!Inventory.IsConserved)
            throw new InvalidOperationException(
                "Resource ledger is inconsistent; refusing to write a corrupt world.");
        byte[] tiles=new byte[_tiles.Length];
        for(int i=0;i<tiles.Length;i++)tiles[i]=(byte)_tiles[i];
        var save=new WorldSaveState
        {
            Width=Width,Height=Height,Seed=Seed,Tick=Tick,Tiles=tiles,
            Resources=Resources.Capture(),
            PopulationLimit=PopulationLimit,
            AutonomousGatheringEnabled=AutonomousGatheringEnabled,
            AutonomousHousingEnabled=AutonomousHousingEnabled,
            PopulationRngState=_aiPopulationRandomState,
            NextCitizenId=_nextId,NextHouseId=_nextHouseId,
            NextHousePlacementTick=_nextHousePlacementTick,
            HouseWoodLost=HouseWoodLost,HouseRevision=HouseRevision,
            ManualWoodStock=ManualWoodStock,ManualFoodStock=ManualFoodStock,
            WoodLostOnDeath=WoodLostOnDeath,FoodLostOnDeath=FoodLostOnDeath,
            FoodConsumedByDeceased=FoodConsumedByDeceased
        };
        foreach(Citizen p in People)
            save.Citizens.Add(new CitizenSaveState
            {
                Id=p.Id,X=p.X,Y=p.Y,BornAt=p.BornAt,
                Activity=p.Activity,Role=p.Role,
                Hunger=p.Hunger,Energy=p.Energy,FoodCarried=p.FoodCarried,
                FoodEaten=p.FoodEaten,WoodCarried=p.WoodCarried,HomeId=p.HomeId,
                HouseTask=p.HouseTask,HouseWorkX=p.HouseWorkX,HouseWorkY=p.HouseWorkY,
                ReservedSiteId=p.ReservedSiteId,TargetX=p.TargetX,TargetY=p.TargetY,
                TargetKind=p.TargetKind,WorkTicks=p.WorkTicks,
                NextPlanTick=p.NextPlanTick,RouteIndex=p.RouteIndex,
                Route=p.Route.ToArray()
            });
        if(FirstHouse is WoodenHouse h)
            save.House=new WoodenHouseSaveState
            {
                Id=h.Id,X=h.X,Y=h.Y,FoundedAt=h.FoundedAt,
                GroundHeight=h.GroundHeight,Stage=h.Stage,
                WoodDelivered=h.WoodDelivered,WorkDone=h.WorkDone,
                Residents=h.Residents,Contributors=h.ContributorIds()
            };
        return save;
    }

    public static WorldState RestoreSave(WorldSaveState save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if(save.Version!=1 || save.Width<16 || save.Height<16 ||
            save.Width>1024 || save.Height>1024 ||
            save.Tiles is null ||
            save.Tiles.Length!=checked(save.Width*save.Height) ||
            save.Citizens is null || save.Resources is null ||
            save.PopulationLimit is < 1 or > 2048 ||
            save.Citizens.Count>save.PopulationLimit ||
            !save.AutonomousGatheringEnabled ||
            save.AutonomousHousingEnabled && !save.AutonomousGatheringEnabled ||
            save.Tick<0 || save.PopulationRngState==0 ||
            save.NextCitizenId<1 || save.NextHouseId<1 ||
            save.NextHousePlacementTick<0 ||
            save.HouseWoodLost<0 || save.HouseRevision<0 ||
            save.ManualWoodStock<0 || save.ManualFoodStock<0 ||
            save.WoodLostOnDeath<0 || save.FoodLostOnDeath<0 ||
            save.FoodConsumedByDeceased<0)
            throw new InvalidDataException("Unsupported or invalid world save header.");

        var world=new WorldState(save.Width,save.Height,save.Seed)
        {
            Tick=save.Tick,PopulationLimit=save.PopulationLimit,
            AutonomousGatheringEnabled=save.AutonomousGatheringEnabled,
            AutonomousHousingEnabled=save.AutonomousHousingEnabled,
            _aiPopulationRandomState=save.PopulationRngState,
            _nextId=save.NextCitizenId,_nextHouseId=save.NextHouseId,
            _nextHousePlacementTick=save.NextHousePlacementTick,
            HouseWoodLost=save.HouseWoodLost,HouseRevision=save.HouseRevision,
            ManualWoodStock=save.ManualWoodStock,ManualFoodStock=save.ManualFoodStock,
            WoodLostOnDeath=save.WoodLostOnDeath,FoodLostOnDeath=save.FoodLostOnDeath,
            FoodConsumedByDeceased=save.FoodConsumedByDeceased
        };

        for(int i=0;i<save.Tiles.Length;i++)
        {
            if(save.Tiles[i]>(byte)Terrain.Burned)
                throw new InvalidDataException("Invalid biome type in saved terrain.");
            world._tiles[i]=(Terrain)save.Tiles[i];
        }
        world.Resources.Restore(save.Resources);

        if(save.House is WoodenHouseSaveState hs)
        {
            if(hs.Id<1 || hs.Id>=save.NextHouseId ||
                hs.FoundedAt<0 || hs.FoundedAt>save.Tick ||
                hs.X<1 || hs.Y<1 || hs.X+2>=world.Width ||
                hs.Y+3>=world.Height ||
                !float.IsFinite(hs.GroundHeight) ||
                hs.Stage>HouseStage.Complete ||
                hs.WoodDelivered is <0 or >WoodenHouse.RequiredWood ||
                hs.WorkDone is <0 or >32 ||
                hs.Residents is <0 or >WoodenHouse.Capacity ||
                hs.Contributors is null ||
                hs.Contributors.Length>save.PopulationLimit)
                throw new InvalidDataException("Invalid saved wooden house.");
            var house=new WoodenHouse(hs.Id,hs.X,hs.Y,hs.FoundedAt)
            {
                GroundHeight=hs.GroundHeight,Stage=hs.Stage,
                WoodDelivered=hs.WoodDelivered,WorkDone=hs.WorkDone,
                Residents=hs.Residents
            };
            house.RestoreContributors(hs.Contributors);
            world.FirstHouse=house;
        }

        var ids=new HashSet<int>();
        foreach(CitizenSaveState cs in save.Citizens)
        {
            if(cs is null || cs.Id<1 || cs.Id>=save.NextCitizenId ||
                !ids.Add(cs.Id) || !world.Walkable(cs.X,cs.Y) ||
                cs.BornAt<0 || cs.BornAt>save.Tick ||
                cs.Hunger is <0 or >100 || cs.Energy is <0 or >100 ||
                cs.FoodCarried<0 || cs.FoodEaten<0 || cs.WoodCarried<0 ||
                cs.HomeId<0 ||
                (cs.HomeId>0 && (world.FirstHouse is null ||
                    cs.HomeId!=world.FirstHouse.Id)) ||
                cs.Activity>CitizenActivity.Resting ||
                cs.Role>CitizenRole.Resident ||
                cs.HouseTask>HouseJob.Rest ||
                cs.TargetKind>ResourceKind.Food ||
                cs.WorkTicks<0 || cs.NextPlanTick<0 ||
                cs.Route is null || cs.Route.Length>MaxSearchCells ||
                cs.RouteIndex<0 || cs.RouteIndex>cs.Route.Length ||
                cs.ReservedSiteId<0)
                throw new InvalidDataException("Invalid saved citizen or assigned job.");

            foreach(int step in cs.Route)
                if(step<0 || step>=world.Width*world.Height)
                    throw new InvalidDataException("Invalid saved navigation route.");
            var citizen=new Citizen(cs.Id,cs.X,cs.Y,cs.BornAt)
            {
                Activity=cs.Activity,Role=cs.Role,Hunger=cs.Hunger,
                Energy=cs.Energy,FoodCarried=cs.FoodCarried,
                FoodEaten=cs.FoodEaten,WoodCarried=cs.WoodCarried,
                HomeId=cs.HomeId,HouseTask=cs.HouseTask,
                HouseWorkX=cs.HouseWorkX,HouseWorkY=cs.HouseWorkY,
                ReservedSiteId=cs.ReservedSiteId,TargetX=cs.TargetX,
                TargetY=cs.TargetY,TargetKind=cs.TargetKind,
                WorkTicks=cs.WorkTicks,NextPlanTick=cs.NextPlanTick,
                RouteIndex=cs.RouteIndex
            };
            citizen.Route.AddRange(cs.Route);
            world.People.Add(citizen);
            if(cs.ReservedSiteId>0)
            {
                if(cs.ReservedSiteId!=world.Resources.SiteId(cs.TargetX,cs.TargetY) ||
                    !world._reservedResources.TryAdd(cs.ReservedSiteId,cs.Id))
                    throw new InvalidDataException("Duplicate or invalid resource reservation.");
            }
        }
        if(world.FirstHouse is WoodenHouse restoredHouse &&
            restoredHouse.Residents!=world.People.FindAll(p=>
                p.HomeId==restoredHouse.Id).Count)
            throw new InvalidDataException("House occupancy does not match residents.");
        if(!world.Inventory.IsConserved)
            throw new InvalidDataException("Saved resources do not balance.");
        return world;
    }
}
