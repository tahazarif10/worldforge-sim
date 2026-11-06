using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim;

/// <summary>
/// Small fixed-tick autonomous gathering loop, wholly independent of Godot
/// and camera residency. Each planned route and claimed resource is owned by
/// one citizen; no duplicate collection and no unbounded path searches.
/// </summary>
public sealed partial class WorldState
{
    public bool AutonomousGatheringEnabled { get; set; }
    public int WorkingCitizenCount
    {
        get
        {
            int count=0;
            foreach(Citizen citizen in People)
                if(citizen.Activity is CitizenActivity.Picking or
                    CitizenActivity.Chopping or CitizenActivity.Delivering or
                    CitizenActivity.Building)
                    count++;
            return count;
        }
    }

    private const int SearchBudgetPerTick=6;
    private const int MaxSearchCells=1200;
    private const int MaxDistanceFromStart=22;
    private const int ChopDurationTicks=7;
    private const int PickDurationTicks=5;
    private const int MaxWoodCarry=4;
    private readonly Dictionary<int,int> _reservedResources=new();

    private void CleanupResourceReservations()
    {
        if(_reservedResources.Count==0)return;
        var alive=new HashSet<int>();
        foreach(Citizen citizen in People)alive.Add(citizen.Id);
        foreach(var claim in new List<KeyValuePair<int,int>>(_reservedResources))
            if(!alive.Contains(claim.Value))
                _reservedResources.Remove(claim.Key);
    }

    private void ReleaseJob(Citizen citizen)
    {
        if(citizen.ReservedSiteId>0 &&
            _reservedResources.TryGetValue(citizen.ReservedSiteId,out int owner) &&
            owner==citizen.Id)
            _reservedResources.Remove(citizen.ReservedSiteId);
        citizen.ReservedSiteId=0;
        citizen.Route.Clear();
        citizen.RouteIndex=0;
        citizen.WorkTicks=0;
        citizen.Activity=CitizenActivity.Idle;
        citizen.Role=CitizenRole.None;
    }

    private void AdvanceCitizenJobs()
    {
        CleanupResourceReservations();
        if(AutonomousHousingEnabled)TryStartNextHouse();
        int searches=0;
        foreach(Citizen person in People)
        {
            if(Tick%3==0 && person.Hunger<100)person.Hunger++;
            if(AutonomousHousingEnabled)
            {
                if(Tick%6==0 && person.Energy>0)person.Energy--;
                if(AdvanceHouseJob(person))continue;
            }
            if(person.WorkTicks>0)
            {
                person.WorkTicks--;
                if(person.WorkTicks==0)CompleteWork(person);
                continue;
            }

            if(person.ReservedSiteId>0)
            {
                var deposit=Resources.At(person.TargetX,person.TargetY);
                bool owned=_reservedResources.TryGetValue(person.ReservedSiteId,out int id) &&
                    id==person.Id;
                if(!owned || deposit.Kind!=person.TargetKind || deposit.Remaining==0)
                {
                    ReleaseJob(person);
                    person.NextPlanTick=Tick+2;
                    continue;
                }

                if(person.X==person.TargetX && person.Y==person.TargetY)
                {
                    BeginWork(person);
                    continue;
                }

                if(person.RouteIndex<person.Route.Count)
                {
                    int cell=person.Route[person.RouteIndex];
                    int x=cell%Width,y=cell/Width;
                    if(Walkable(x,y) &&
                        Math.Abs(person.X-x)+Math.Abs(person.Y-y)==1)
                    {
                        person.X=x;person.Y=y;
                        person.RouteIndex++;
                        person.Activity=CitizenActivity.Walking;
                        continue;
                    }
                }

                // A paint event or changed obstacle invalidated a route.
                ReleaseJob(person);
                person.NextPlanTick=Tick+4;
                continue;
            }

            person.Activity=CitizenActivity.Idle;
            if(searches>=SearchBudgetPerTick || Tick<person.NextPlanTick)continue;
            if(AutonomousHousingEnabled)
            {
                WoodenHouse? shelter=ActiveConstructionHouse;
                bool needsRest=person.HomeId!=0 && person.Energy<=25 &&
                    Houses.Any(h=>h.Id==person.HomeId && h.Stage==HouseStage.Complete);
                bool needsHouseTrip=needsRest || (shelter is not null &&
                    (shelter.WoodDelivered<NextWoodThreshold(shelter.Stage)
                        ? person.WoodCarried>0 : true));
                if(needsHouseTrip)
                {
                    searches++;
                    if(TryAssignHouseJob(person))continue;
                    if(searches>=SearchBudgetPerTick)continue;
                }
            }
            searches++;
            // Shared house demand wins when reasonably fed. Citizens with
            // enough carried logs stop indiscriminately cutting more forest.
            // When no more wood is needed, a portion become foragers rather
            // than all four searching the same tree/resource category.
            bool needHouseWood=AutonomousHousingEnabled &&
                ActiveConstructionHouse is WoodenHouse construction &&
                construction.WoodDelivered<NextWoodThreshold(construction.Stage);
            ResourceKind first=person.Hunger>=70 || person.WoodCarried>=MaxWoodCarry
                ? ResourceKind.Food
                : needHouseWood ? ResourceKind.Wood
                : person.Hunger>=35 || person.Id%3!=0
                    ? ResourceKind.Food : ResourceKind.Wood;
            ResourceKind second=first==ResourceKind.Food
                ? ResourceKind.Wood : ResourceKind.Food;
            if(!TryAssignNearestResource(person,first) &&
                !TryAssignNearestResource(person,second))
                person.NextPlanTick=Tick+12;
        }
    }

    private void BeginWork(Citizen citizen)
    {
        citizen.Activity=citizen.TargetKind==ResourceKind.Wood
            ? CitizenActivity.Chopping : CitizenActivity.Picking;
        citizen.WorkTicks=citizen.TargetKind==ResourceKind.Wood
            ? ChopDurationTicks : PickDurationTicks;
    }

    private void CompleteWork(Citizen citizen)
    {
        if(Resources.TryGather(citizen.TargetX,citizen.TargetY,Tick,
            out GatherResult gathered) && gathered.Kind==citizen.TargetKind)
        {
            if(gathered.Kind==ResourceKind.Food)
            {
                citizen.FoodCarried++;
                if(citizen.Hunger>=40 && citizen.FoodCarried>0)
                {
                    citizen.FoodCarried--;
                    citizen.FoodEaten++;
                    citizen.Hunger=Math.Max(0,citizen.Hunger-52);
                }
            }
            else
                citizen.WoodCarried++;

            // Continue chopping one reserved tree until it is exhausted,
            // unless hunger is urgent. Every hit consumes fixed ticks.
            if(gathered.Kind==ResourceKind.Wood && gathered.Remaining>0 &&
                citizen.WoodCarried<MaxWoodCarry && citizen.Hunger<82)
            {
                BeginWork(citizen);
                return;
            }
        }
        ReleaseJob(citizen);
        citizen.NextPlanTick=Tick+2;
    }

    /// <summary>
    /// Deterministic BFS on logical walkable tiles, not the expensive visual
    /// meshes. At most 1200 visited cells within a 22-tile radius, at most six
    /// new plans per simulation tick. Cardinal direction order is stable.
    /// </summary>
    private bool TryAssignNearestResource(Citizen person,ResourceKind kind)
    {
        if(kind==ResourceKind.Wood && person.WoodCarried>=MaxWoodCarry)
            return false;
        int origin=person.Y*Width+person.X;
        var queue=new Queue<int>(256);
        var previous=new Dictionary<int,int>(512){{origin,-1}};
        queue.Enqueue(origin);
        int examined=0;
        int found=-1;
        while(queue.Count>0 && examined++<MaxSearchCells)
        {
            int cell=queue.Dequeue();
            int x=cell%Width,y=cell/Width;
            var deposit=Resources.At(x,y);
            int site=Resources.SiteId(x,y);
            if(deposit.Kind==kind && deposit.Remaining>0 &&
                !_reservedResources.ContainsKey(site))
            {
                found=cell;
                break;
            }

            for(int direction=0;direction<4;direction++)
            {
                int nx=x+(direction==0?-1:direction==1?1:0);
                int ny=y+(direction==2?-1:direction==3?1:0);
                if(!Walkable(nx,ny) ||
                    Math.Abs(nx-person.X)+Math.Abs(ny-person.Y)>MaxDistanceFromStart)
                    continue;
                int next=ny*Width+nx;
                if(previous.ContainsKey(next))continue;
                previous.Add(next,cell);
                queue.Enqueue(next);
            }
        }
        if(found<0)return false;
        var path=new List<int>();
        for(int cell=found;cell!=origin;cell=previous[cell])
            path.Add(cell);
        path.Reverse();

        int tx=found%Width,ty=found/Width;
        person.TargetX=tx;person.TargetY=ty;
        person.TargetKind=kind;
        person.ReservedSiteId=Resources.SiteId(tx,ty);
        _reservedResources.Add(person.ReservedSiteId,person.Id);
        person.Route.Clear();
        person.Route.AddRange(path);
        person.RouteIndex=0;
        person.Activity=CitizenActivity.Walking;
        person.Role=kind==ResourceKind.Food
            ? CitizenRole.Forager : CitizenRole.Woodcutter;
        return true;
    }
}
