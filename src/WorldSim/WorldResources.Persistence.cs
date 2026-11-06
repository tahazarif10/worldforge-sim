using System;
using System.Collections.Generic;
using System.IO;

namespace WorldSim;

public sealed class ResourceSaveState
{
    public byte[] Kinds { get; set; }=[];
    public byte[] Remaining { get; set; }=[];
    public byte[] Capacity { get; set; }=[];
    public long[] NextGrowth { get; set; }=[];
    public long Revision { get; set; }
    public int WoodHarvested { get; set; }
    public int FoodHarvested { get; set; }
}

public sealed partial class WorldResources
{
    internal ResourceSaveState Capture()
    {
        var kinds=new byte[_kinds.Length];
        for(int i=0;i<kinds.Length;i++)kinds[i]=(byte)_kinds[i];
        return new ResourceSaveState
        {
            Kinds=kinds,
            Remaining=(byte[])_amounts.Clone(),
            Capacity=(byte[])_capacity.Clone(),
            NextGrowth=(long[])_nextGrowth.Clone(),
            Revision=_revision,
            WoodHarvested=WoodCollected,
            FoodHarvested=FoodCollected
        };
    }

    internal void Restore(ResourceSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int n=_kinds.Length;
        if(state.Kinds is null || state.Remaining is null ||
            state.Capacity is null || state.NextGrowth is null ||
            state.Kinds.Length!=n || state.Remaining.Length!=n ||
            state.Capacity.Length!=n || state.NextGrowth.Length!=n ||
            state.Revision<0 || state.WoodHarvested<0 || state.FoodHarvested<0)
            throw new InvalidDataException("Corrupt or incompatible resource arrays.");
        var food=new SortedSet<int>();
        var stumps=new SortedSet<int>();
        var growing=new HashSet<int>();
        for(int i=0;i<n;i++)
        {
            byte kind=state.Kinds[i],remaining=state.Remaining[i],
                cap=state.Capacity[i];
            long growth=state.NextGrowth[i];
            if(kind>(byte)ResourceKind.Food || remaining>cap || growth<0)
                throw new InvalidDataException("Invalid resource quantity or type.");
            if(kind==(byte)ResourceKind.None && (cap!=0 || remaining!=0))
                throw new InvalidDataException("Empty terrain contains nonzero resource stock.");
            if(kind==(byte)ResourceKind.Wood && (cap<5 || cap>8))
                throw new InvalidDataException("Invalid forest wood capacity.");
            if(kind==(byte)ResourceKind.Food && (cap<2 || cap>4))
                throw new InvalidDataException("Invalid berry capacity.");
            if(kind==(byte)ResourceKind.Food)
            {
                food.Add(i);
                if(remaining<cap)
                {
                    if(growth==0)
                        throw new InvalidDataException("Partly harvested food lost its growth deadline.");
                    growing.Add(i);
                }
            }
            if(kind==(byte)ResourceKind.Wood && remaining==0)
                stumps.Add(i);
        }
        for(int i=0;i<n;i++)
        {
            _kinds[i]=(ResourceKind)state.Kinds[i];
            _amounts[i]=state.Remaining[i];
            _capacity[i]=state.Capacity[i];
            _nextGrowth[i]=state.NextGrowth[i];
        }
        _foodSites.Clear();
        foreach(int i in food)_foodSites.Add(i);
        _stumpSites.Clear();
        foreach(int i in stumps)_stumpSites.Add(i);
        _growingFood.Clear();
        foreach(int i in growing)_growingFood.Add(i);
        _recentlyFelled.Clear(); // Actual stump meshes are reconstructed from data.
        _completedGrowth.Clear();
        _revision=state.Revision;
        WoodCollected=state.WoodHarvested;
        FoodCollected=state.FoodHarvested;
    }
}
