using System;
using System.Linq;

namespace WorldSim;

/// <summary>
/// Distinguishes historical harvesting counters from actual usable supplies.
 /// Manual Gather is a debug chest; worker resources live in person inventories.
 /// All losses (including death and demolition) remain accounted for.
 /// </summary>
public readonly record struct WorldInventory(
    int WoodAvailable,int FoodAvailable,int WoodCarried,int FoodCarried,
    int WoodInHouse,int WoodLost,int FoodConsumed,int FoodLost,
    int WoodHarvested,int FoodHarvested,int ManualWood,int ManualFood)
{
    public bool IsConserved =>
        WoodHarvested==WoodAvailable+WoodInHouse+WoodLost &&
        FoodHarvested==FoodAvailable+FoodConsumed+FoodLost;
}

public sealed partial class WorldState
{
    public int ManualWoodStock { get; private set; }
    public int ManualFoodStock { get; private set; }
    public int WoodLostOnDeath { get; private set; }
    public int FoodLostOnDeath { get; private set; }
    public int FoodConsumedByDeceased { get; private set; }

    public WorldInventory Inventory
    {
        get
        {
            int carriedWood=0,carriedFood=0,eaten=FoodConsumedByDeceased;
            foreach(Citizen person in People)
            {
                carriedWood+=person.WoodCarried;
                carriedFood+=person.FoodCarried;
                eaten+=person.FoodEaten;
            }
            return new WorldInventory(
                carriedWood+ManualWoodStock,
                carriedFood+ManualFoodStock,
                carriedWood,carriedFood,FirstHouse?.WoodDelivered??0,
                HouseWoodLost+WoodLostOnDeath,eaten,FoodLostOnDeath,
                Resources.WoodCollected,Resources.FoodCollected,
                ManualWoodStock,ManualFoodStock);
        }
    }

    private int RemoveCitizens(Predicate<Citizen> doomed)
    {
        int removed=0;
        foreach(Citizen citizen in People)
        {
            if(!doomed(citizen))continue;
            WoodLostOnDeath+=citizen.WoodCarried;
            FoodLostOnDeath+=citizen.FoodCarried;
            FoodConsumedByDeceased+=citizen.FoodEaten;
            removed++;
        }
        if(removed==0)return 0;
        People.RemoveAll(doomed);
        if(AutonomousGatheringEnabled)CleanupResourceReservations();
        if(AutonomousHousingEnabled)MaintainHouseResidents();
        return removed;
    }
}
