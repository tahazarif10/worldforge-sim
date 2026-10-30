using System.Numerics;
using WorldSim;
using WorldForge.VisualLab;
using WorldForge.Camera3D;

static void Check(bool condition, string title)
{
    if (!condition) throw new InvalidOperationException("FAIL " + title);
    Console.WriteLine("PASS " + title);
}
float lift = TerrainReliefMath.CenterLift(Terrain.Mountain, 3, 4, 20261030);
Check(lift >= .12f && lift <= .281f, "bounded mountain apex relief");
Check(lift == TerrainReliefMath.CenterLift(Terrain.Mountain,3,4,20261030),
    "deterministic mountain shape");
Check(TerrainReliefMath.CenterLift(Terrain.DeepWater,3,4,1)==0f &&
      TerrainReliefMath.CenterLift(Terrain.ShallowWater,3,4,2)==0f,
      "water remains single opaque plane");
Check(TerrainReliefMath.CenterLift(Terrain.Sand,3,4,1) < .021f,
      "coast remains visually calm");
Check(TerrainReliefMath.SurfaceHeight(2,lift,0,0)==2f &&
      TerrainReliefMath.SurfaceHeight(2,lift,1,.7f)==2f &&
      TerrainReliefMath.SurfaceHeight(2,lift,.5f,.5f)==2f+lift,
      "tile corners and borders remain at old authoritative height");
Check(MathF.Abs(TerrainReliefMath.SurfaceHeight(2,lift,.25f,.5f) -
                (2f+lift*.5f))<1e-5f,
      "facet interpolation matches four-triangle top");

static float Ground(int x,int y) => 2f;
static float Relief(int x,int y) => TerrainReliefMath.CenterLift(Terrain.Mountain,x,y,200);
int passes=0;
foreach(float u in new[]{.08f,.15f,.25f,.4f,.5f,.6f,.75f,.85f,.92f})
foreach(float v in new[]{.08f,.2f,.36f,.5f,.65f,.82f,.92f})
{
    var origin=new Vector3(3+u,20,4+v);
    Check(TerrainGridPicker.TryPick(origin,-Vector3.UnitY,10,10,Ground,
        out var hit,Relief) && hit.X==3 && hit.Y==4,
        "exact sculpted tile selected");
    float expected=TerrainReliefMath.SurfaceHeight(2, Relief(3,4),u,v);
    Check(MathF.Abs(hit.Point.Y-expected) < .0005f,
        "picked ray intersection matches rendered facet elevation");
    passes++;
}
Check(passes==63,"every quadrant of four-triangle fan covered");
Check(TerrainGridPicker.TryPick(new(3.5f,20,4.5f),-Vector3.UnitY,
    10,10,Ground,out var oldFlat) && oldFlat.Point.Y==2f,
    "old flat-picking remains compatible when lift callback omitted");
Check(TerrainGridPicker.TryPick(new(3.5f,20,4.5f),-Vector3.UnitY,
    10,10,Ground,out var disabled,(_,_)=>0f) && disabled.Point.Y==2f,
    "F5 disabled visual geometry uses flat picking");
Check(TerrainGridPicker.TryPick(new(3.5f,20,4.5f),-Vector3.UnitY,
    10,10,Ground,out var raised,Relief) && raised.Point.Y>disabled.Point.Y,
    "F5 visual height changes picked height consistently");
Check(!TerrainGridPicker.TryPick(new(3.5f,20,4.5f),Vector3.UnitY,
    10,10,Ground,out _,Relief),
    "rays traveling up away from terrain rejected");
Console.WriteLine("PASS ALL sculpted relief geometry and precise camera brush picking tests");
