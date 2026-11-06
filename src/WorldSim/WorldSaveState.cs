using System;
using System.Collections.Generic;

namespace WorldSim;

/// <summary>Versioned WorldSim-only session snapshot. Numeric, no Godot types.</summary>
public sealed class WorldSaveState
{
    public int Version { get; set; }=1;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Seed { get; set; }
    public long Tick { get; set; }
    public byte[] Tiles { get; set; }=[];
    public ResourceSaveState Resources { get; set; }=new();
    public int PopulationLimit { get; set; }
    public bool AutonomousGatheringEnabled { get; set; }
    public bool AutonomousHousingEnabled { get; set; }
    public uint PopulationRngState { get; set; }
    public int NextCitizenId { get; set; }
    public int NextHouseId { get; set; }
    public long NextHousePlacementTick { get; set; }
    public int HouseWoodLost { get; set; }
    public int HouseRevision { get; set; }
    public int ManualWoodStock { get; set; }
    public int ManualFoodStock { get; set; }
    public int WoodLostOnDeath { get; set; }
    public int FoodLostOnDeath { get; set; }
    public int FoodConsumedByDeceased { get; set; }
    public List<CitizenSaveState> Citizens { get; set; }=[];
    public WoodenHouseSaveState? House { get; set; }
}

public sealed class CitizenSaveState
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public long BornAt { get; set; }
    public CitizenActivity Activity { get; set; }
    public CitizenRole Role { get; set; }
    public int Hunger { get; set; }
    public int Energy { get; set; }
    public int FoodCarried { get; set; }
    public int FoodEaten { get; set; }
    public int WoodCarried { get; set; }
    public int HomeId { get; set; }
    public HouseJob HouseTask { get; set; }
    public int HouseWorkX { get; set; }
    public int HouseWorkY { get; set; }
    public int ReservedSiteId { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public ResourceKind TargetKind { get; set; }
    public int WorkTicks { get; set; }
    public long NextPlanTick { get; set; }
    public int RouteIndex { get; set; }
    public int[] Route { get; set; }=[];
}

public sealed class WoodenHouseSaveState
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public long FoundedAt { get; set; }
    public float GroundHeight { get; set; }
    public HouseStage Stage { get; set; }
    public int WoodDelivered { get; set; }
    public int WorkDone { get; set; }
    public int Residents { get; set; }
    public int[] Contributors { get; set; }=[];
}
