using System;
using System.Linq;
using WorldSim;

internal static class HouseSmoke
{
    private static void Require(bool condition,string why)
    {
        if(!condition)throw new InvalidOperationException("HOUSE SMOKE FAILED: "+why);
        Console.WriteLine("PASS HOUSE: "+why);
    }

    internal static void Run()
    {
        WorldState Prepare()
        {
            var w=new WorldState(96,96,20261106)
            {
                AutonomousGatheringEnabled=true,
                AutonomousHousingEnabled=true,
                PopulationLimit=4
            };
            w.Paint(48,48,20,Terrain.Grass);
            w.Paint(48,48,8,Terrain.Forest);
            Require(w.Spawn(48,48) && w.Spawn(49,49) &&
                w.Spawn(47,47) && w.Spawn(50,50),"four workers on reachable island");
            return w;
        }

        WorldState a=Prepare(),b=Prepare();
        bool sawSite=false,sawFoundation=false,sawFrame=false;
        bool sawRoof=false,sawDelivery=false,sawConstruction=false,sawRest=false;
        int lastHouseId=0;
        for(int tick=0;tick<1800;tick++)
        {
            a.Advance();b.Advance();

            WoodenHouse? house=a.FirstHouse;
            if(house is not null)
            {
                sawSite=true;
                lastHouseId=house.Id;
                sawFoundation|=house.Stage==HouseStage.Foundation;
                sawFrame|=house.Stage==HouseStage.Frame;
                sawRoof|=house.Stage==HouseStage.Roof;
                Require(house.WoodDelivered>=0 &&
                    house.WoodDelivered<=WoodenHouse.RequiredWood &&
                    house.WorkDone>=0 &&
                    house.Residents<=WoodenHouse.Capacity,
                    "construction stays within bounded material and occupancy limits");
            }
            sawDelivery|=a.People.Any(p=>p.Activity==CitizenActivity.Delivering);
            sawConstruction|=a.People.Any(p=>p.Activity==CitizenActivity.Building);
            sawRest|=a.People.Any(p=>p.Activity==CitizenActivity.Resting);

            // Material flows only from previously harvested logs in hands.
            int stored=a.FirstHouse?.WoodDelivered ?? 0;
            int carried=a.People.Sum(p=>p.WoodCarried);
            Require(a.Resources.WoodCollected==carried+stored+a.HouseWoodLost,
                "world lumber is conserved across carried, invested and destroyed stock");
            Require(a.People.All(p=>a.Walkable(p.X,p.Y)),
                "builders and gatherers never walk onto impassable terrain");

            if(a.CompletedHouseCount>0 && sawRest)break;
        }

        WoodenHouse finished=a.FirstHouse ??
            throw new InvalidOperationException("HOUSE SMOKE FAILED: no construction site");
        WoodenHouse replay=b.FirstHouse ??
            throw new InvalidOperationException("HOUSE SMOKE FAILED: missing replay site");
        Require(sawSite && sawFoundation && sawFrame && sawRoof &&
            finished.Stage==HouseStage.Complete,
            "the first home passes through planned, foundation, frame, roof and complete");
        Require(sawDelivery && sawConstruction && sawRest,
            "citizens visibly haul, build and eventually return home to rest");
        Require(finished.WoodDelivered==WoodenHouse.RequiredWood &&
            finished.WorkDone>=28 &&
            finished.ContributorCount>=2,
            "one finished home costs twelve real logs and at least two villagers contribute");
        Require(finished.Residents==WoodenHouse.Capacity &&
            a.People.Count(p=>p.HomeId==finished.Id)==WoodenHouse.Capacity,
            "completed wooden home supports exactly four occupants");
        Require(replay.Id==lastHouseId && replay.Id==finished.Id &&
            replay.X==finished.X && replay.Y==finished.Y &&
            replay.Stage==finished.Stage &&
            replay.WoodDelivered==finished.WoodDelivered &&
            replay.WorkDone==finished.WorkDone &&
            replay.Residents==finished.Residents &&
            replay.ContributorCount==finished.ContributorCount &&
            a.Resources.WoodCollected==b.Resources.WoodCollected &&
            a.People.Select(p=>(p.Id,p.X,p.Y,p.Hunger,p.Energy,p.WoodCarried,p.HomeId,p.Activity))
                .SequenceEqual(b.People.Select(p=>
                    (p.Id,p.X,p.Y,p.Hunger,p.Energy,p.WoodCarried,p.HomeId,p.Activity))),
            "same-seed world replays placement, building, delivery and residents identically");

        int invested=finished.WoodDelivered;
        a.Paint(finished.X,finished.Y,0,Terrain.DeepWater);
        Require(a.FirstHouse is null && a.CompletedHouseCount==0 &&
            a.HouseWoodLost==invested &&
            a.People.All(p=>p.HomeId==0) &&
            a.Resources.WoodCollected==a.People.Sum(p=>p.WoodCarried)+a.HouseWoodLost,
            "destroying the house clears residence and records lost material without refund");

        WorldState fresh=Prepare();
        Require(fresh.FirstHouse is null && fresh.HouseWoodLost==0 &&
            fresh.People.All(p=>p.HomeId==0),
            "New World has no inherited home or building inventory");
    }
}
