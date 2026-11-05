using System.Numerics;
using WorldSim;
using WorldForge.Streaming;
using WorldForge.VisualLab;

static void Check(bool ok,string label)
{
    if(!ok)throw new InvalidOperationException("FAIL "+label);
    Console.WriteLine("PASS "+label);
}
static void Reject(Action action,string label)
{
    bool rejected=false;
    try{action();}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}
    Check(rejected,label);
}
static void Until(Func<bool> condition)
{
    if(!SpinWait.SpinUntil(condition,TimeSpan.FromSeconds(5)))
        throw new TimeoutException("Deterministic worker barrier did not complete.");
}
var world=new WorldState(33,25,1315511732);
for(int z=0;z<25;z++)
for(int x=0;x<33;x++)world.Paint(x,z,0,(Terrain)((x*3+z*5)%7));
var generation=new ChunkGeneration(1,0,0,15);
var request=new ChunkRequest(new(1,0),generation,1);
int owner=Environment.CurrentManagedThreadId,reads=0;
Terrain ReadTile(int x,int z)
{
    if(Environment.CurrentManagedThreadId!=owner)throw new InvalidOperationException("Live data read off owner.");
    reads++;return world.Tile(x,z);
}
var snapshot=new ChunkBuildSnapshot(request,33,25,world.Seed,true,true,true,1023,false,false,
    ReadTile,(x,z)=>(x%3-z%3)*.25f);
Check(reads<=18*18 && snapshot.ArrayPayloadBytes<=18*18+17*17*4,
    "snapshot owns only chunk halo and corner lattice");
var before=ChunkMeshBuilder.Build(snapshot);
int originalReads=reads;
world.Paint(16,0,0,Terrain.DeepWater);
var parallel=Task.Run(()=>ChunkMeshBuilder.Build(snapshot)).GetAwaiter().GetResult();
Check(reads==originalReads && before.Positions.SequenceEqual(parallel.Positions) &&
    before.Normals.SequenceEqual(parallel.Normals) && before.Colors.SequenceEqual(parallel.Colors) &&
    before.Props.SequenceEqual(parallel.Props),"immutable worker result ignores subsequent live edits");
Reject(()=>snapshot.Tile(0,0),"snapshot rejects reads beyond captured halo");
Reject(()=>new ChunkBuildSnapshot(request,33,25,1,true,true,true,0,false,false,
    (_,_)=>Terrain.Grass,(_,_)=>float.NaN),"snapshot rejects invalid elevation");
Reject(()=>new ChunkBuildSnapshot(request with{Key=new(int.MaxValue,0)},33,25,1,true,true,true,0,false,false,
    (_,_)=>Terrain.Grass,(_,_)=>0),"overflowing chunk coordinate rejected before allocation");
using(var canceled=new CancellationTokenSource())
{
    canceled.Cancel();bool seen=false;
    try{ChunkMeshBuilder.Build(snapshot,canceled.Token);}catch(OperationCanceledException){seen=true;}
    Check(seen,"pre-canceled numeric work stops without publication");
}
long vertices=0;
foreach(bool rich in new[]{false,true})
foreach(bool facets in new[]{false,true})
foreach(bool smooth in new[]{false,true})
foreach(int assets in new[]{0,1023})
for(int z=0;z<2;z++)
for(int x=0;x<3;x++)
{
    var s=new ChunkBuildSnapshot(new(new(x,z),generation,1),33,25,world.Seed,rich,facets,smooth,assets,true,true,
        world.Tile,(_,_)=>0);
    var data=ChunkMeshBuilder.Build(s);vertices+=data.Positions.Length;
    if(data.PayloadBytes>ChunkMeshData.MaximumPayloadBytes || data.PropCount>s.Width*s.Depth ||
        data.Props.ToArray().Count(p=>p.Kind==NumericPropKind.ImportedTree)>2 ||
        data.Props.ToArray().Count(p=>p.Kind==NumericPropKind.ImportedRock)>1)
        throw new InvalidOperationException("Numeric output/prop bounds failed.");
    foreach(Vector3 n in data.Normals)
        if(!float.IsFinite(n.X)||!float.IsFinite(n.Y)||!float.IsFinite(n.Z)||Math.Abs(n.Length()-1)>.001f)
            throw new InvalidOperationException("Invalid numeric normal.");
}
Check(true,$"all eight geometry formats and art availability preserve output bounds ({vertices} vertices)");
byte[] originalCoast=WaterCoastMath.BuildMask(33,25,world.Tile,world.Seed);
var coast=new CoastBuildSnapshot(33,25,world.Seed,world.Tile);
world.Paint(16,12,4,Terrain.Forest);
Check(coast.Build(default).Rgba.SequenceEqual(originalCoast),"coast snapshot preserves exact original mask after edits");
// Expanded map keeps exact V2 coast pixel math and a bounded worker payload.
var expanded=new WorldState(192,144,1315511732);
var expandedCoast=new CoastBuildSnapshot(expanded.Width,expanded.Height,
    expanded.Seed,expanded.Tile).Build(default);
var expandedOracle=WaterCoastMath.BuildMask(expanded.Width,expanded.Height,
    expanded.Tile,expanded.Seed);
Check(expandedCoast.Rgba.SequenceEqual(expandedOracle) &&
    expandedCoast.PayloadBytes==192L*144*4 &&
    expandedCoast.PayloadBytes<=256*1024,
    "expanded world coast worker matches V2 and remains in 256 KiB reservation");
Reject(()=>new CoastBuildSnapshot(192,145,1,(_,_)=>Terrain.DeepWater),
    "expanded preview coast ceiling rejects extra rows before allocation");
Reject(()=>new CoastBuildSnapshot(8192,8192,1,(_,_)=>Terrain.DeepWater),
    "large global coast allocation refused before sampling");

using(var pool=new BoundedWorkPool<int,Box>(2,4,100))
using(var gate=new ManualResetEventSlim(false))
using(var started=new CountdownEvent(2))
{
    Box Work(CancellationToken token){started.Signal();if(!gate.Wait(TimeSpan.FromSeconds(5),token))throw new TimeoutException();return new Box(7);}
    Check(pool.TryStart(1,50,Work)&&pool.TryStart(2,50,Work),"two bounded workers accepted");
    Check(started.Wait(TimeSpan.FromSeconds(5)),"both workers reached controlled barrier");
    Check(pool.Running==2 && pool.ReservedBytes==100 && !pool.TryStart(3,1,_=>new Box(0)),
        "running count and byte reservation enforce backpressure");
    Reject(()=>pool.Release(1),"running reservation cannot be released");
    bool wrongOwner=Task.Run(()=>
    {
        try{pool.CanStart(1);return false;}catch(InvalidOperationException){return true;}
    }).GetAwaiter().GetResult();
    Check(wrongOwner,"pool metadata rejects non-owner mutation");
    gate.Set();Until(()=>pool.Completed==2);
    Check(pool.Running==0 && !pool.CanStart(1) && pool.ReservedBytes==100,
        "completed result memory stays reserved until consumer releases it");
    foreach(var done in pool.Ready())
    {
        Check(done.WorkerThreadId!=owner && done.Result?.Value==7 && done.Failure is null,
            "pure work executes on background thread and produces an owned result");
        pool.Release(done.Id);
    }
    Check(pool.ReservedBytes==0 && pool.Count==0,"result consumption releases every reservation");
}
using(var pool=new BoundedWorkPool<int,Box>(1,2,100))
using(var gate=new ManualResetEventSlim(false))
using(var started=new ManualResetEventSlim(false))
{
    pool.TryStart(1,100,_=>{started.Set();if(!gate.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();return new Box(1);});
    Check(started.Wait(TimeSpan.FromSeconds(5)),"late-cancel worker started");
    pool.CancelWhere(_=>true);
    Check(pool.Running==1 && pool.ReservedBytes==100 && !pool.CanStart(1),
        "cancellation does not free a still-running reservation");
    gate.Set();Until(()=>pool.Completed==1);
    var done=pool.Ready().Single();
    Check(done.Canceled,"late result remains canceled even when delegate returned normally");
    pool.Release(done.Id);
    pool.TryStart(2,100,_=>throw new InvalidOperationException("intentional worker failure"));
    Until(()=>pool.Completed==1);
    done=pool.Ready().Single();
    Check(done.Failure?.Contains("intentional worker failure")==true && done.Result is null,
        "worker exception is observed as a bounded failure result");
    pool.Release(done.Id);
}
int stopped=0;
using(var started=new ManualResetEventSlim(false))
{
    var pool=new BoundedWorkPool<int,Box>(1,2,100);
    pool.TryStart(1,100,token=>
    {
        started.Set();
        try{token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();return new Box(0);}
        finally{Interlocked.Increment(ref stopped);}
    });
    Check(started.Wait(TimeSpan.FromSeconds(5)),"teardown worker started");
    pool.Dispose();
    Check(stopped==1 && pool.Count==0 && pool.ReservedBytes==0,"teardown cancels and joins before releasing owner");
    pool.Dispose();
}

using(var queue=new ChunkResidencyQueue(4))
using(var pool=new BoundedWorkPool<ChunkRequest,Box>(1,2,100))
using(var gate=new ManualResetEventSlim(false))
{
    queue.Reconcile([new(new(0,0),generation,true)]);
    queue.TryBegin(new(0,0),out var old);
    pool.TryStart(old,100,_=>{if(!gate.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();return new Box(1);});
    queue.Reset();
    queue.Reconcile([new(new(0,0),generation with{WorldEpoch=2},true)]);
    pool.CancelWhere(r=>!queue.IsCurrent(r));
    gate.Set();Until(()=>pool.Completed==1);
    var done=pool.Ready().Single();
    Check(!queue.IsCurrent(done.Tag)&&!queue.Complete(done.Tag),
        "old-world work cannot publish into replacement world");
    pool.Release(done.Id);
    queue.TryBegin(new(0,0),out var current);
    Check(queue.Abandon(current) && queue.StateOf(current.Key)==ChunkResidencyState.Failed &&
        !queue.TryBegin(current.Key,out _),"exhausted failure blocks a spinning retry");
    queue.Invalidate(current.Key,generation with{WorldEpoch=2,DataRevision=1});
    Check(queue.TryBegin(current.Key,out var retry) && retry.Ticket!=current.Ticket,
        "new data generation may retry with a new lease");
}
double clock=0;
var budget=new PublicationBudget(2,1000,4,()=>clock);
Check(budget.TryBegin(600)&&!budget.TryBegin(500)&&budget.TryBegin(400)&&!budget.TryBegin(0),
    "publication enforces hard count and payload limits");
clock=0;budget=new PublicationBudget(4,1000,4,()=>clock);
Check(budget.TryBegin(100),"first non-preemptible upload admitted");
clock=7;
Check(budget.Overran&&!budget.TryBegin(100),"measured overrun blocks further uploads, not falsely a hard deadline");
clock=0;budget=new PublicationBudget(0,1000,4,()=>clock);
Check(!budget.TryBegin(0),"zero count budget never publishes");
clock=2;budget=new PublicationBudget(1,1000,4,()=>clock);clock=1;
Reject(()=>_ = budget.ElapsedMilliseconds,"backward budget clock fails closed");
clock=0;budget=new PublicationBudget(2,1000,4,()=>clock);
Check(budget.TryBegin(800),"terrain phase admitted within its own payload");
clock=4;
Check(!budget.TryBegin(200),"foliage phase waits when terrain reaches the soft deadline");
clock=0;budget=new PublicationBudget(1,1000,4,()=>clock);
Check(budget.TryBegin(800)&&!budget.TryBegin(200),"one-unit admission forces a distinct foliage update");
clock=10;budget=new PublicationBudget(1,1000,4,()=>clock);
Check(budget.TryBegin(200)&&budget.Bytes==200,"resumed phase charges remaining payload, not terrain twice");
Console.WriteLine("PASS ALL immutable work, retained ownership and separate phase admission budgets");

internal sealed record Box(int Value);
