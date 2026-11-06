using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim;

/// <summary>A small cooperative wooden house in the first playable loop.</summary>
public enum HouseStage : byte { Planned, Foundation, Frame, Roof, Complete }
public enum HouseJob : byte { None, DeliverWood, Build, GoHome, Rest }

public sealed class WoodenHouse(int id,int x,int y,long foundedAt)
{
    // A single 2x2 grass plot, with a reachable entrance on the south side.
    public const int RequiredWood=12;
    public const int Capacity=4;
    public int Id { get; }=id;
    public int X { get; }=x;
    public int Y { get; }=y;
    public int EntranceX => X;
    public int EntranceY => Y+2;
    public long FoundedAt { get; }=foundedAt;
    public float GroundHeight { get; internal set; }
    public HouseStage Stage { get; internal set; }=HouseStage.Planned;
    public int WoodDelivered { get; internal set; }
    public int WorkDone { get; internal set; }
    public int Residents { get; internal set; }
    private readonly HashSet<int> _contributors=new();
    public int ContributorCount => _contributors.Count;
    internal void CreditBuilder(int citizenId) => _contributors.Add(citizenId);
    internal int[] ContributorIds() => _contributors.OrderBy(id=>id).ToArray();
    internal void RestoreContributors(IEnumerable<int> ids)
    {
        _contributors.Clear();
        foreach(int id in ids)_contributors.Add(id);
    }
}

/// <summary>
/// Bounded three-home village. One construction site is active at a time;
/// independently owned materials, residents and worker assignments are
/// authoritative in WorldSim, never in the renderer or wall clock.
/// </summary>
public sealed partial class WorldState
{
    public bool AutonomousHousingEnabled { get; set; }
    public const int MaxHouseCount=3;
    private readonly List<WoodenHouse> _houses=new();
    public IReadOnlyList<WoodenHouse> Houses => _houses;
    // Compatibility view for original one-home integration/tests.
    public WoodenHouse? FirstHouse => _houses.Count==0?null:_houses[0];
    public WoodenHouse? ActiveConstructionHouse =>
        _houses.FirstOrDefault(h=>h.Stage!=HouseStage.Complete);
    public int CompletedHouseCount => _houses.Count(h=>h.Stage==HouseStage.Complete);
    public int HouseWoodLost { get; private set; }
    public int HouseRevision { get; private set; }

    private int _nextHouseId=1;
    private const int HouseMaxPlacementRadius=12;
    private const int HouseJobDuration=4;
    private const int HomeRestTicks=12;
    private long _nextHousePlacementTick;
    // A terrain-height sampler is supplied by the approved V2 renderer. The
    // core only sees numeric surface samples and remains Godot-independent.
    // Unconfigured simulation fixtures model flat terrain at height zero.
    private Func<int,int,float,float,float>? _houseSurface;
    private const float MaxHouseElevationDifference=.30f;
    private const float MaxDoorElevationDifference=.52f;

    public void ConfigureHouseSurface(Func<int,int,float,float,float> sampler)
    {
        _houseSurface=sampler??throw new ArgumentNullException(nameof(sampler));
        InvalidateFirstHouseIfNecessary();
    }

    public void RecheckHouseSurface() => InvalidateFirstHouseIfNecessary();
    public bool CanPlaceHouseAt(int x,int y) => IsValidHousePlot(x,y);

    private static int NextWoodThreshold(HouseStage stage) => stage switch
    {
        HouseStage.Planned=>3,
        HouseStage.Foundation=>8,
        HouseStage.Frame=>WoodenHouse.RequiredWood,
        HouseStage.Roof=>WoodenHouse.RequiredWood,
        _=>int.MaxValue
    };

    private static int NextWorkThreshold(HouseStage stage) => stage switch
    {
        HouseStage.Planned=>4,
        HouseStage.Foundation=>12,
        HouseStage.Frame=>20,
        HouseStage.Roof=>28,
        _=>int.MaxValue
    };

    public void InvalidateFirstHouseIfNecessary() => InvalidateHousesIfNecessary();

    private void InvalidateHousesIfNecessary()
    {
        bool demolished=false;
        foreach(WoodenHouse house in _houses.ToArray())
        {
            if(IsValidHousePlot(house.X,house.Y,house.Id))
            {
                if(TryHouseElevation(house.X,house.Y,out float top) &&
                    house.GroundHeight!=top)
                {
                    house.GroundHeight=top;
                    HouseRevision++;
                }
                continue;
            }

            // Never refund already invested logs after a terrain edit/meteor.
            HouseWoodLost+=house.WoodDelivered;
            _houses.Remove(house);
            HouseRevision++;
            demolished=true;
            foreach(Citizen citizen in People)
            {
                if(citizen.HomeId==house.Id)citizen.HomeId=0;
                if(citizen.HouseTargetId==house.Id)CancelHouseJob(citizen);
            }
        }
        if(demolished)
        {
            _nextHousePlacementTick=Tick+25;
            MaintainHouseResidents();
        }
    }

    private bool IsValidHousePlot(int x,int y,int existingId=0)
    {
        if(x<1 || y<1 || x+2>=Width || y+3>=Height ||
            !Walkable(x,y+2) || !Walkable(x+1,y+2) ||
            Tile(x,y+2)!=Terrain.Grass || Tile(x+1,y+2)!=Terrain.Grass)
            return false;
        for(int dy=0;dy<2;dy++)
        for(int dx=0;dx<2;dx++)
            if(Tile(x+dx,y+dy)!=Terrain.Grass ||
                Resources.At(x+dx,y+dy).Kind!=ResourceKind.None)
                return false;
        // Keep each 2x2 footprint and both entrance tiles distinct, with
        // a small gap between cabins for readable village navigation.
        foreach(WoodenHouse other in _houses)
            if(other.Id!=existingId &&
                x<=other.X+2 && x+2>=other.X &&
                y<=other.Y+3 && y+3>=other.Y)
                return false;
        if(existingId==0 && People.Exists(p=>
            p.X>=x && p.X<x+2 && p.Y>=y && p.Y<y+2))
            return false;
        return TryHouseElevation(x,y,out _);
    }

    private bool TryHouseElevation(int x,int y,out float top)
    {
        top=0f;
        if(_houseSurface is null)return true;
        float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
        // Four shared terrain corners per tile across the 2x2 foundation.
        // Comparing nine unique physical corners rejects both cliffs and
        // locally uneven ground without sampling a rendered mesh.
        for(int dz=0;dz<=2;dz++)
        for(int dx=0;dx<=2;dx++)
        {
            float height=_houseSurface(x+dx,y+dz,0f,0f);
            if(!float.IsFinite(height))return false;
            minimum=Math.Min(minimum,height);
            maximum=Math.Max(maximum,height);
        }
        float door=_houseSurface(x,y+2,.5f,.5f);
        if(!float.IsFinite(door) || maximum-minimum>MaxHouseElevationDifference ||
            Math.Abs(door-maximum)>MaxDoorElevationDifference)
            return false;
        top=maximum;
        return true;
    }

    private void TryStartNextHouse()
    {
        // Build a second home for citizen #5, a third for citizen #9.
        // Do not spawn empty buildings or parallel projects requiring fake wood.
        if(_houses.Count>=MaxHouseCount || ActiveConstructionHouse is not null ||
            Tick<_nextHousePlacementTick ||
            People.Count<2 || Resources.WoodCollected<2 ||
            (_houses.Count>0 &&
                People.Count<=_houses.Count*WoodenHouse.Capacity))
            return;
        _nextHousePlacementTick=Tick+20;
        int ax=_houses.Count==0?People[0].X:_houses[^1].X+1;
        int ay=_houses.Count==0?People[0].Y:_houses[^1].EntranceY;
        // Deterministic expanding search beside the established settlement.
        for(int radius=1;radius<=HouseMaxPlacementRadius;radius++)
        for(int dy=-radius;dy<=radius;dy++)
        for(int dx=-radius;dx<=radius;dx++)
        {
            if(Math.Abs(dx)+Math.Abs(dy)!=radius)continue;
            int x=ax+dx,y=ay+dy;
            if(!IsValidHousePlot(x,y))continue;
            if(!TryHouseElevation(x,y,out float top))continue;
            _houses.Add(new WoodenHouse(_nextHouseId++,x,y,Tick)
            {
                GroundHeight=top
            });
            HouseRevision++;
            return;
        }
    }

    private void MaintainHouseResidents()
    {
        foreach(Citizen citizen in People)
            if(citizen.HomeId!=0 && !_houses.Any(h=>
                h.Id==citizen.HomeId && h.Stage==HouseStage.Complete))
                citizen.HomeId=0;

        foreach(WoodenHouse house in _houses)
        {
            int residents=0;
            if(house.Stage==HouseStage.Complete)
            {
                foreach(Citizen citizen in People)
                {
                    if(citizen.HomeId!=house.Id)continue;
                    if(residents<WoodenHouse.Capacity)residents++;
                    else citizen.HomeId=0;
                }
            }
            if(house.Residents!=residents)
            {
                house.Residents=residents;
                HouseRevision++;
            }
        }
        foreach(Citizen citizen in People)
        {
            if(citizen.HomeId!=0)continue;
            foreach(WoodenHouse house in _houses)
            {
                if(house.Stage!=HouseStage.Complete ||
                    house.Residents>=WoodenHouse.Capacity)continue;
                citizen.HomeId=house.Id;
                house.Residents++;
                HouseRevision++;
                break;
            }
        }
    }

    private void CancelHouseJob(Citizen citizen)
    {
        citizen.HouseTask=HouseJob.None;
        citizen.HouseTargetId=0;
        citizen.HouseWorkX=0;
        citizen.HouseWorkY=0;
        citizen.Role=CitizenRole.None;
        citizen.Route.Clear();
        citizen.RouteIndex=0;
        citizen.WorkTicks=0;
        citizen.Activity=CitizenActivity.Idle;
    }

    private bool TryAssignHouseJob(Citizen citizen)
    {
        WoodenHouse? home=_houses.Find(h=>
            h.Id==citizen.HomeId && h.Stage==HouseStage.Complete);
        WoodenHouse? house;
        HouseJob task;
        if(home is not null && citizen.Energy<=25)
        {
            house=home;
            task=HouseJob.GoHome;
        }
        else
        {
            house=ActiveConstructionHouse;
            if(house is null)return false;
            if(house.WoodDelivered<NextWoodThreshold(house.Stage))
            {
                if(citizen.WoodCarried<=0)return false;
                task=HouseJob.DeliverWood;
            }
            else task=HouseJob.Build;
        }

        int maxWorkers=task switch
        {
            HouseJob.Build or HouseJob.DeliverWood=>2,
            _=>WoodenHouse.Capacity
        };
        if(People.Count(person=>person.HouseTargetId==house.Id &&
            person.HouseTask==task)>=maxWorkers)
            return false;
        // Distinct door-side work spots; quotas are per structure.
        int first=People.Count(person=>person.HouseTargetId==house.Id &&
            person.HouseTask==task &&
            person.HouseWorkX==house.EntranceX)==0?0:1;
        int tx=house.EntranceX+first,ty=house.EntranceY;
        if(!TryPlanHousePath(citizen,tx,ty))return false;
        citizen.HouseWorkX=tx;
        citizen.HouseWorkY=ty;
        citizen.HouseTargetId=house.Id;
        citizen.HouseTask=task;
        citizen.Role=task switch
        {
            HouseJob.Build=>CitizenRole.Builder,
            HouseJob.DeliverWood=>CitizenRole.Hauler,
            _=>CitizenRole.Resident
        };
        citizen.Activity=CitizenActivity.Walking;
        return true;
    }

    // Bounded, fixed-order BFS shared in spirit with resource navigation.
    // Never query visual meshes or load/unload any chunk to find a path.
    private bool TryPlanHousePath(Citizen person,int targetX,int targetY)
    {
        if(!Walkable(targetX,targetY) ||
            Math.Abs(targetX-person.X)+Math.Abs(targetY-person.Y)>MaxDistanceFromStart)
            return false;
        int source=person.Y*Width+person.X;
        int goal=targetY*Width+targetX;
        var queue=new Queue<int>(256);
        var previous=new Dictionary<int,int>(512){{source,-1}};
        queue.Enqueue(source);
        int examined=0;
        while(queue.Count>0 && examined++<MaxSearchCells &&
              !previous.ContainsKey(goal))
        {
            int cell=queue.Dequeue();
            int x=cell%Width,y=cell/Width;
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
        if(!previous.ContainsKey(goal))return false;
        var reverse=new List<int>();
        for(int cell=goal;cell!=source;cell=previous[cell])
            reverse.Add(cell);
        reverse.Reverse();
        person.Route.Clear();
        person.Route.AddRange(reverse);
        person.RouteIndex=0;
        return true;
    }

    /// <summary>True when the citizen's house job handled this tick.</summary>
    private bool AdvanceHouseJob(Citizen citizen)
    {
        if(citizen.HouseTask==HouseJob.None)return false;
        WoodenHouse? house=_houses.Find(h=>h.Id==citizen.HouseTargetId);
        if(house is null)
        {
            CancelHouseJob(citizen);
            citizen.NextPlanTick=Tick+2;
            return true;
        }

        // A stage transition can change the task while citizens are en route.
        if((house.Stage==HouseStage.Complete &&
                citizen.HouseTask is HouseJob.Build or HouseJob.DeliverWood) ||
           (house.Stage!=HouseStage.Complete &&
                citizen.HouseTask is HouseJob.GoHome or HouseJob.Rest))
        {
            CancelHouseJob(citizen);
            citizen.NextPlanTick=Tick+1;
            return true;
        }

        if(citizen.HouseTask==HouseJob.Rest)
        {
            citizen.Activity=CitizenActivity.Resting;
            citizen.Energy=Math.Min(100,citizen.Energy+7);
            if(--citizen.WorkTicks<=0 || citizen.Energy>=95)
            {
                CancelHouseJob(citizen);
                citizen.NextPlanTick=Tick+4;
            }
            return true;
        }

        if(citizen.X!=citizen.HouseWorkX || citizen.Y!=citizen.HouseWorkY)
        {
            if(citizen.RouteIndex<citizen.Route.Count)
            {
                int cell=citizen.Route[citizen.RouteIndex];
                int x=cell%Width,y=cell/Width;
                if(Walkable(x,y) &&
                    Math.Abs(citizen.X-x)+Math.Abs(citizen.Y-y)==1)
                {
                    citizen.X=x;citizen.Y=y;
                    citizen.RouteIndex++;
                    citizen.Activity=CitizenActivity.Walking;
                    return true;
                }
            }
            CancelHouseJob(citizen);
            citizen.NextPlanTick=Tick+4;
            return true;
        }

        if(citizen.HouseTask==HouseJob.GoHome)
        {
            citizen.HouseTask=HouseJob.Rest;
            citizen.WorkTicks=HomeRestTicks;
            citizen.Activity=CitizenActivity.Resting;
            return true;
        }

        if(citizen.WorkTicks<=0)
        {
            citizen.WorkTicks=HouseJobDuration;
            citizen.Activity=citizen.HouseTask==HouseJob.Build
                ? CitizenActivity.Building : CitizenActivity.Delivering;
            return true;
        }
        citizen.WorkTicks--;
        if(citizen.WorkTicks>0)return true;
        if(citizen.HouseTask==HouseJob.DeliverWood)
        {
            int needed=NextWoodThreshold(house.Stage)-house.WoodDelivered;
            int delivered=Math.Min(citizen.WoodCarried,Math.Max(0,needed));
            if(delivered>0)
            {
                citizen.WoodCarried-=delivered;
                house.WoodDelivered+=delivered;
                HouseRevision++;
            }
        }
        else if(citizen.HouseTask==HouseJob.Build &&
            house.WoodDelivered>=NextWoodThreshold(house.Stage))
        {
            house.WorkDone+=4;
            house.CreditBuilder(citizen.Id);
            HouseRevision++;
            if(house.WorkDone>=NextWorkThreshold(house.Stage))
            {
                house.Stage++;
                HouseRevision++;
                if(house.Stage==HouseStage.Complete)
                    MaintainHouseResidents();
            }
        }
        CancelHouseJob(citizen);
        citizen.NextPlanTick=Tick+1;
        return true;
    }
}
