using System.Numerics;
using WorldForge.Streaming;

static void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL " + label);
    Console.WriteLine("PASS " + label);
}
static void Reject(Action action, string label)
{
    bool rejected = false;
    try { action(); }
    catch (ArgumentException) { rejected = true; }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, label);
}
static Vector3[] Box(float left, float top, float right, float bottom,
    float nearY = 20, float farY = -20) =>
[
    new(left, nearY, top), new(right, nearY, top),
    new(right, nearY, bottom), new(left, nearY, bottom),
    new(left, farY, top), new(right, farY, top),
    new(right, farY, bottom), new(left, farY, bottom)
];

var one = ViewportChunkPlanner.Plan(120, 88, 16, Box(38,38,47,47), -8,13,0);
Check(one.Visible.Count == 1 && one.Desired.SetEquals(one.Visible) &&
    one.Visible.Contains(new(2,2)), "finite orthographic volume selects one chunk");
var border = ViewportChunkPlanner.Plan(120,88,16,Box(15,15,18,18),-8,13,0);
Check(border.Visible.Count == 4, "four touching chunk cells are retained");
var padded = ViewportChunkPlanner.Plan(120,88,16,Box(38,38,47,47),-8,13);
Check(padded.Visible.Count == 1 && padded.Desired.Count == 9, "margin separate from visible demand");
var worldEdge = ViewportChunkPlanner.Plan(120,88,16,Box(115,83,140,110),-8,13,0);
Check(worldEdge.Desired.SetEquals([new ChunkCoord(7,5)]), "partial final world chunk included");
Check(ViewportChunkPlanner.Plan(120,88,16,Box(-100,-100,-20,-20),-8,13).Desired.Count == 0,
    "volume outside world produces empty plan");
Check(ViewportChunkPlanner.Plan(120,88,16,Box(20,20,40,40,40,20),-8,13).Desired.Count == 0,
    "finite near/far volume above terrain is not extended to infinity");
Check(ViewportChunkPlanner.Plan(120,88,16,Box(20,20,40,40,10,0),-8,13,0).Visible.Count == 4,
    "whole camera volume inside height slab keeps original vertices");

Vector3[] tilted = Box(10,14,18,22,30,-30);
for (int i=4; i<8; i++) tilted[i].Z += 60;
var fullHeight = ViewportChunkPlanner.Plan(120,120,16,tilted,-8,13,0);
var oldHeight = ViewportChunkPlanner.Plan(120,120,16,tilted,.12,5,0);
Check(fullHeight.Visible.Contains(new(0,1)) && !oldHeight.Visible.Contains(new(0,1)),
    "positive sculpt envelope adds geometry outside the old five-metre slab");
Check(fullHeight.Visible.Contains(new(0,3)), "negative sculpt envelope covers below-sea terrain");

Vector3[] horizontal =
[
    new(10,4,10), new(20,4,10), new(20,2,10), new(10,2,10),
    new(10,4,100), new(20,4,100), new(20,2,100), new(10,2,100)
];
Check(ViewportChunkPlanner.Plan(128,128,16,horizontal,0,3,0).Visible.Contains(new(0,6)),
    "near-horizontal camera uses clipped finite volume without a ray-plane singularity");
var local = ViewportChunkPlanner.Plan(8192,8192,16,Box(4097,4097,4110,4110),-8,13);
Check(local.Desired.Count == 9, "large logical world still enumerates only local demand");
Reject(() => ViewportChunkPlanner.Plan(8192,8192,1,Box(0,0,8192,8192),-8,13,capacity:8),
    "over-capacity viewport refuses partial geometry before enumeration");
Reject(() => ViewportChunkPlanner.Plan(int.MaxValue,int.MaxValue,1,
    Box(0,0,int.MaxValue,int.MaxValue),-8,13,capacity:8), "large dimension count cannot overflow into an accepted plan");
var invalid = Box(0,0,1,1); invalid[3].X = float.NaN;
Reject(() => ViewportChunkPlanner.Plan(120,88,16,invalid,-8,13), "nonfinite projection rejected");
Reject(() => ViewportChunkPlanner.Plan(120,88,16,new Vector3[7],-8,13), "missing frustum corner rejected");
Reject(() => ViewportChunkPlanner.Plan(120,88,16,Box(0,0,1,1),13,-8), "inverted height slab rejected");

// Independent interior ray sampling, not a copy of the edge-extrema algorithm.
var random = new Random(341);
int tested = 0;
for (int fixture=0; fixture<120; fixture++)
{
    double yaw = fixture * .397;
    double pitch = .03 + (fixture % 20) * .075;
    Vector3 forward = Vector3.Normalize(new((float)Math.Cos(yaw),
        -(float)Math.Tan(pitch), (float)Math.Sin(yaw)));
    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
    Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
    Vector3 origin = new(128 + random.Next(-80,81), 60, 128 + random.Next(-80,81));
    float size = new[] {19f,79f,160f}[fixture%3];
    float aspect = new[] {.5f,1.6f,3.2f}[fixture%3];
    var corners = new Vector3[8];
    for (int depth=0; depth<2; depth++)
    {
        float distance = depth==0 ? .1f : 400f;
        float scale = fixture%2==0 ? 1 : (depth==0 ? .01f : 2f);
        Vector3 center = origin + forward * distance;
        Vector3 r = right * size * aspect * .5f * scale;
        Vector3 u = up * size * .5f * scale;
        corners[depth*4] = center-r+u;
        corners[depth*4+1] = center+r+u;
        corners[depth*4+2] = center+r-u;
        corners[depth*4+3] = center-r-u;
    }
    var plan = ViewportChunkPlanner.Plan(256,256,16,corners,-8,13,0);
    for(int sy=0;sy<=16;sy++)
    for(int sx=0;sx<=16;sx++)
    {
        float u=sx/16f,v=sy/16f;
        Vector3 a=Vector3.Lerp(Vector3.Lerp(corners[0],corners[1],u),
            Vector3.Lerp(corners[3],corners[2],u),v);
        Vector3 b=Vector3.Lerp(Vector3.Lerp(corners[4],corners[5],u),
            Vector3.Lerp(corners[7],corners[6],u),v);
        foreach(float height in new[] {-8f,.12f,13f})
        {
            double dy=(double)b.Y-a.Y;
            if(Math.Abs(dy)<1e-8) continue;
            double fraction=(height-a.Y)/dy;
            if(fraction<0 || fraction>1) continue;
            double x=a.X+fraction*((double)b.X-a.X), z=a.Z+fraction*((double)b.Z-a.Z);
            if(x<0 || z<0 || x>=256 || z>=256) continue;
            if(!plan.Visible.Contains(new((int)x/16,(int)z/16)))
                throw new InvalidOperationException($"Missing interior sample {fixture}: {x},{z}");
            tested++;
        }
    }
}
Check(tested > 10000, $"conservative coverage for {tested} independent perspective/orthographic samples");

var generation = new ChunkGeneration(1,0,0,1);
ChunkDemand Demand(int x,int z,bool visible=true) => new(new(x,z),generation,visible);
using var queue = new ChunkResidencyQueue(8);
queue.Reconcile([Demand(0,0,false),Demand(7,7)]);
Check(queue.TryTake(0,0,16,out var first) && first.Key == new ChunkCoord(7,7),
    "visible demand precedes nearby prefetch");
for(int i=0;i<100;i++) queue.Reconcile([Demand(7,7),Demand(i%5,0,false)]);
Check(queue.StateOf(first.Key)==ChunkResidencyState.Generating && queue.Complete(first),
    "continuous camera demand does not invalidate useful in-flight work");
Check(!queue.Complete(first), "duplicate completion rejected");
var revision = generation with {DataRevision=1};
queue.Invalidate(first.Key,revision);
Check(queue.StateOf(first.Key)==ChunkResidencyState.Stale, "resident geometry retained as stale on edit");
Check(queue.TryBegin(first.Key,out var edited), "edited resident can request replacement");
Check(!queue.Complete(edited with {Generation=revision with {FormatVersion=2}}),
    "wrong geometry format rejected");
Check(!queue.Complete(edited with {Generation=revision with {Lod=1}}), "wrong LOD rejected");
Check(!queue.Complete(edited with {Generation=revision with {WorldEpoch=2}}), "wrong world rejected");
Check(!queue.Complete(edited with {Generation=generation}), "wrong data revision rejected");
Check(!queue.Complete(edited with {Ticket=edited.Ticket+99}), "wrong request lease rejected");
Check(queue.Fail(edited), "failed build is requeued");
Check(queue.TryBegin(first.Key,out var retry) && retry.Ticket!=edited.Ticket,
    "failed build retries with a distinct lease");
Check(!queue.Complete(edited) && queue.Complete(retry), "late failed response cannot replace retry");
queue.Reconcile([Demand(2,2)]);
queue.TryBegin(new(2,2),out var removed);
queue.Reconcile([]);
queue.Reconcile([Demand(2,2)]);
queue.TryBegin(new(2,2),out var readded);
Check(!queue.Complete(removed) && queue.Complete(readded), "remove/re-add ABA rejected even at identical generation");

queue.Reconcile([Demand(3,3)]);
queue.TryBegin(new(3,3),out var priorWorld);
queue.Reset();
queue.Reconcile([new ChunkDemand(new(3,3),generation with {WorldEpoch=2},true)]);
queue.TryBegin(new(3,3),out var newWorld);
Check(!queue.Complete(priorWorld) && queue.Complete(newWorld), "world replacement rejects previous request");

queue.Reset();
queue.Reconcile([Demand(100,100)]);
queue.Reconcile([Demand(100,100),Demand(0,0)]);
Check(queue.TryTake(0,0,16,out var oldest) && oldest.Key==new ChunkCoord(100,100),
    "older still-visible cohort cannot starve behind newly arriving nearer work");
queue.Complete(oldest);
Reject(() => queue.Reconcile(Enumerable.Range(0,9).Select(i=>Demand(i,0)).ToArray()),
    "queue capacity checked before current demand is mutated");
Check(queue.Count==2 && queue.StateOf(oldest.Key)==ChunkResidencyState.Ready,
    "capacity refusal preserves prior resident state");
Reject(() => queue.Reconcile([Demand(1,1),Demand(1,1)]), "duplicate demand rejected atomically");

for(int step=0;step<1000;step++)
{
    queue.Reconcile(Enumerable.Range(0,4).Select(i=>Demand(step+i,step%11)).ToArray());
    int built=0;
    while(queue.TryTake(step*16,0,16,out var work))
    {
        CheckCompletion(queue.Complete(work));
        if(++built>4) throw new InvalidOperationException("Unbounded build loop");
    }
    if(queue.Count!=4 || queue.PendingCount!=0) throw new InvalidOperationException("Unbounded or undrained queue");
}
Check(true, "1000 changing demand windows retain only four entries and drain");
queue.Reconcile([Demand(1,1)]);
queue.TryBegin(new(1,1),out var teardown);
queue.Dispose();
Check(!queue.Complete(teardown) && queue.Count==0 &&
    queue.StateOf(new(1,1))==ChunkResidencyState.Disposed, "teardown releases metadata and rejects late work");
Console.WriteLine("PASS ALL V3.4.1 viewport and residency tests");

static void CheckCompletion(bool ok)
{
    if(!ok) throw new InvalidOperationException("Valid synchronous lease rejected");
}

FallbackChecks.Run();
Console.WriteLine("PASS ALL V3.4.2 surface fallback tests");
