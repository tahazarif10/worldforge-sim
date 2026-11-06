using System;
using System.Linq;
using WorldSim;

internal static class InventorySmoke
{
    private static void Check(bool condition,string what)
    {
        if(!condition)throw new InvalidOperationException("INVENTORY FAIL: "+what);
        Console.WriteLine("PASS INVENTORY: "+what);
    }

    public static void Run()
    {
        var manual=new WorldState(96,96,1315511732);
        var resources=Enumerable.Range(0,manual.Width*manual.Height)
            .Select(i=>(X:i%manual.Width,Y:i/manual.Width,
                Deposit:manual.Resources.At(i%manual.Width,i/manual.Width))).ToArray();
        var wood=resources.First(x=>x.Deposit.Kind==ResourceKind.Wood);
        var food=resources.First(x=>x.Deposit.Kind==ResourceKind.Food);
        Check(manual.TryGather(wood.X,wood.Y,out _) &&
            manual.TryGather(food.X,food.Y,out _),"debug Gather collects finite items");
        WorldInventory supplies=manual.Inventory;
        Check(supplies.WoodAvailable==1 && supplies.FoodAvailable==1 &&
            supplies.WoodHarvested==1 && supplies.FoodHarvested==1 &&
            supplies.IsConserved,"manual Gather is assigned to debug chest inventory");
        Check(manual.Inventory==supplies && manual.Inventory.IsConserved,
            "camera-independent inventory remains stable without simulation advancement");

        var living=new WorldState(96,96,20261106)
        {
            AutonomousGatheringEnabled=true,
            PopulationLimit=3
        };
        living.Paint(48,48,18,Terrain.Grass);
        living.Paint(48,48,8,Terrain.Forest);
        Check(living.Spawn(48,48)&&living.Spawn(49,49)&&living.Spawn(47,47),
            "three people start on the same deterministic real-resource island");
        bool woodCarried=false,foodEaten=false;
        for(int i=0;i<600;i++)
        {
            living.Advance();
            Check(living.Inventory.IsConserved,"all gathering and consumption stays balanced");
            woodCarried|=living.People.Any(p=>p.WoodCarried>0);
            foodEaten|=living.People.Any(p=>p.FoodEaten>0);
            if(woodCarried && foodEaten)break;
        }
        Check(woodCarried&&foodEaten,"citizens actually carry wood and eat gathered food");
        var worker=living.People.First(p=>p.WoodCarried>0);
        int carriedWood=worker.WoodCarried,carriedFood=worker.FoodCarried;
        int historicalEaten=worker.FoodEaten;
        living.Meteor(worker.X,worker.Y,0);
        WorldInventory after=living.Inventory;
        Check(!living.People.Contains(worker) &&
            after.WoodLost>=carriedWood &&
            after.FoodLost>=carriedFood &&
            after.FoodConsumed>=historicalEaten &&
            after.IsConserved,
            "death moves carried supplies to recorded losses and retains consumption history");
        Check(manual.Inventory.IsConserved && after.IsConserved,
            "independent worlds keep separate conservation ledgers");
    }
}
