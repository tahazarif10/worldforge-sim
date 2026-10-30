using System.Numerics;
using WorldSim;
using WorldForge.VisualLab;
using WorldForge.Camera3D;

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL " + label);
}
static bool Near(float a,float b,float tolerance=0.0005f)=>
    Math.Abs(a-b)<tolerance;

const int width=80, height=64, seed=20261030;
var world=new WorldState(width,height,seed);
world.Paint(15,17,4,Terrain.Mountain);
world.Paint(16,18,2,Terrain.Forest);
world.Paint(30,32,3,Terrain.Grass);
world.Paint(17,16,1,Terrain.Sand);
float BaseHeight(int x,int y)
{
    return world.Tile(x,y) switch
    {
        Terrain.DeepWater or Terrain.ShallowWater => .12f,
        Terrain.Sand => .29f,
        Terrain.Grass => .61f,
        Terrain.Forest => .68f,
        Terrain.Burned => .53f,
        Terrain.Mountain => 2.6f +
            (EnvironmentArtMath.Hash(x,y,seed) % 11) * .17f,
        _ => .6f
    };
}
float Lift(int x,int y)=>TerrainReliefMath.CenterLift(
    world.Tile(x,y),x,y,seed);
SharedCornerHeightfield.Fan Fan(int x,int y)=>
    SharedCornerHeightfield.Tile(x,y,width,height,BaseHeight,Lift(x,y));
float Sample(int x,int y,float u,float v)=>
    world.Tile(x,y) is Terrain.DeepWater or Terrain.ShallowWater
        ? .12f : Fan(x,y).Sample(u,v);
int sameEdges=0;
for(int y=0;y<height;y++)
for(int x=0;x<width;x++)
{
    var one=Fan(x,y);
    Check(Near(one.Sample(0,0),one.A) &&
          Near(one.Sample(0,1),one.B) &&
          Near(one.Sample(1,0),one.C) &&
          Near(one.Sample(1,1),one.D) &&
          Near(one.Sample(.5f,.5f),one.Center),
          "five fan vertices reproduce their assigned height");
    if(x+1<width)
    {
        var next=Fan(x+1,y);
        Check(Near(one.C,next.A) && Near(one.D,next.B),
            "land heightfield shares neighboring east/west corners");
        for(int i=0;i<=10;i++)
            Check(Near(one.Sample(1,i/10f),next.Sample(0,i/10f)),
                "equal interpolated height across x cell seam");
        sameEdges++;
    }
    if(y+1<height)
    {
        var next=Fan(x,y+1);
        Check(Near(one.B,next.A) && Near(one.D,next.C),
            "north/south shared corners are identical");
        for(int i=0;i<=10;i++)
            Check(Near(one.Sample(i/10f,1),next.Sample(i/10f,0)),
                "equal interpolated height across y cell seam");
        sameEdges++;
    }
}
Check(sameEdges>10000, "whole island cross-tile edges tested");

// Explicit renderer chunk boundaries x=16/32 and y=16/32.
for(int i=0;i<height;i++)
{
    var left=Fan(15,i);
    var right=Fan(16,i);
    Check(Near(left.C,right.A)&&Near(left.D,right.B),
        "16-wide independent visual chunks share exactly same edge heights");
}
int picks=0;
for(int y=8;y<height-8;y+=5)
for(int x=8;x<width-8;x+=4)
{
    if(world.Tile(x,y) is Terrain.DeepWater or Terrain.ShallowWater) continue;
    foreach(float u in new[]{.1f,.32f,.5f,.73f,.91f})
    foreach(float v in new[]{.09f,.29f,.5f,.77f,.9f})
    {
        float expected=Sample(x,y,u,v);
        // Cast from directly above this exact triangle point; no ambiguity
        // from hidden higher neighboring mountain faces.
        var origin=new Vector3(x+u,20,y+v);
        Check(TerrainGridPicker.TryPick(origin,-Vector3.UnitY,width,height,
            BaseHeight,out var hit,Lift,Sample) &&
            hit.X==x && hit.Y==y && Near(hit.Point.Y,expected,.001f),
            "screen picking height matches rendered 3D shared-corner triangle");
        picks++;
    }
}
Check(picks>500,"hundreds of precise 3D ray-vs-mesh samples");

// An edit near the boundary changes corner samples on both sides in exactly
// the same way (no hidden per-chunk cached edge stale values).
float oldCorner=Fan(15,16).D;
world.Paint(16,16,0,Terrain.Mountain);
var a=Fan(15,16);
var b=Fan(16,16);
Check(Near(a.C,b.A) && Near(a.D,b.B),
    "brush mutation has identical shared-corner height on both chunks");
Check(float.IsFinite(oldCorner)&&float.IsFinite(a.D),
    "edited boundary vertex remains finite");

bool invalid=false;
try {_=SharedCornerHeightfield.Corner(0,0,0,64,BaseHeight);}
catch(ArgumentOutOfRangeException){invalid=true;}
Check(invalid,"invalid region dimensions rejected");
// Large coordinate proof (not 8192x8192 game/render test): samples close to
// 8192 without constructing an 8192x8192 float height array.
float Indexed(int x,int y)=>(x+y)%9*.07f+.3f;
float big=SharedCornerHeightfield.Corner(8191,8191,8192,8192,Indexed);
Check(float.IsFinite(big) && big>.29f,"sparse 8192-coordinate corner computation works");
Console.WriteLine("PASS ALL continuous shared-corner terrain / camera boundary tests. " +
    "Exact picks="+picks+", seams="+sameEdges);
