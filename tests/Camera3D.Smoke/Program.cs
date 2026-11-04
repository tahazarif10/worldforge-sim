using System;
using System.Numerics;
using WorldForge.Camera3D;

static void Check(bool yes, string name)
{
    if (!yes) throw new InvalidOperationException("FAIL " + name);
    Console.WriteLine("PASS " + name);
}
static bool Near(Vector3 a, Vector3 b, float tolerance = 0.0005f) =>
    Vector3.Distance(a, b) < tolerance;

Check(CameraRayMath.IntersectHorizontalPlane(new(4, 10, 6), new(0, -1, 0), 3, out var p) &&
      Near(p, new(4, 3, 6)), "vertical ray plane intersection");
Check(!CameraRayMath.IntersectHorizontalPlane(new(0, 10, 0), new(1, 0, 0), 0, out _),
      "parallel ray rejected");
Check(!CameraRayMath.IntersectHorizontalPlane(new(0, 10, 0), new(0, 1, 0), 0, out _),
      "behind-camera plane rejected");

// A world point grabbed at screen A must remain at screen B by translating
// the camera pivot by anchor - reprojected(B). Works at any tilt/zoom.
var anchor = new Vector3(2, 5, 3);
Check(CameraRayMath.TryAnchorCorrection(anchor, new Vector3(8, 20, 9),
    Vector3.Normalize(new Vector3(1, -2, 1)), out var correction),
    "anchor ray correction valid");
Check(correction.Y == 0 && float.IsFinite(correction.X),
    "anchor corrects XZ only");
Check(CameraRayMath.IntersectHorizontalPlane(
    new Vector3(8, 20, 9) + correction,
    Vector3.Normalize(new Vector3(1, -2, 1)), anchor.Y, out var newPosition) &&
    Near(newPosition, anchor), "ground anchor remains under pointer after translation");

static float Flat(int x, int y) => 1f;
Check(TerrainGridPicker.TryPick(new Vector3(3.25f, 20, 4.75f),
    new Vector3(0, -1, 0), 10, 10, Flat, out var hit) &&
    hit.X == 3 && hit.Y == 4 && Near(hit.Point, new Vector3(3.25f, 1, 4.75f)),
    "vertical ray hits exact top tile");
Check(TerrainGridPicker.TryPick(new Vector3(-3f, 10, 4.5f),
    Vector3.Normalize(new Vector3(1, -1, 0)), 10, 10,
    (x, _) => x == 5 ? 6 : 1, out var cliff) &&
    cliff.X == 5, "outside-map ray finds tall cliff after crossing cells");
Check(!TerrainGridPicker.TryPick(new Vector3(-3, 10, 4.5f),
    new Vector3(-1, -1, 0), 10, 10, Flat, out _),
    "rays heading away from world rejected");
Check(!TerrainGridPicker.TryPick(new Vector3(3, 10, 3),
    new Vector3(0, 1, 0), 10, 10, Flat, out _),
    "upward rays cannot select surface below");
Check(TerrainGridPicker.TryPick(new Vector3(2.5f, 15, 2.5f),
    Vector3.Normalize(new Vector3(0.3f, -1f, 0.2f)), 10, 10, Flat, out var diagonal) &&
    diagonal.X >= 0 && diagonal.Y >= 0,
    "diagonal ray grid traversal");

int visited = 0;
_ = TerrainGridPicker.TryPick(new Vector3(-1, 120, 0.5f),
    Vector3.Normalize(new Vector3(1, -0.02f, 0)), 1000, 2,
    (x, y) => { visited++; return 0.12f; }, out _);
Check(visited < 1005, "cost bounded by crossed cells, not distance/.22");

Console.WriteLine("PASS ALL camera 3D ray and terrain picking tests");

// The scalar vertex callback is the retained oracle. Acquiring the same five
// values together must not change triangles, DDA ordering or edge tolerances.
static float Vertex(TerrainGridPicker.SurfaceFan fan, float u, float v) => (u, v) switch
{
    (0f, 0f) => fan.A, (0f, 1f) => fan.B,
    (1f, 0f) => fan.C, (1f, 1f) => fan.D,
    (.5f, .5f) => fan.Center,
    _ => throw new InvalidOperationException("Picker requested a non-vertex scalar sample.")
};

var fanCases = new[]
{
    new TerrainGridPicker.SurfaceFan(.12f, .12f, .12f, .12f, .12f),
    new TerrainGridPicker.SurfaceFan(.2f, 2.3f, 4.1f, -1.1f, 7.2f),
    new TerrainGridPicker.SurfaceFan(-7.39f, 8.61f, -7.39f, 8.61f, .61f),
    new TerrainGridPicker.SurfaceFan(4f, 4f, 4f, 4f, 4.000001f)
};
int fanComparisons = 0;
foreach (TerrainGridPicker.SurfaceFan fan in fanCases)
{
    bool Matches(Vector3 origin, Vector3 direction)
    {
        bool scalar = TerrainGridPicker.TryPick(origin, direction, 3, 3, Flat,
            out var a, surfaceHeightAt: (_, _, u, v) => Vertex(fan, u, v));
        bool batched = TerrainGridPicker.TryPick(origin, direction, 3, 3, Flat,
            out var b, surfaceFanAt: (_, _) => fan);
        fanComparisons++;
        return scalar == batched && (!scalar || a == b);
    }
    foreach (float u in new[] { 0f, .000001f, .1f, .25f, .499999f, .5f, .500001f, .75f, .999999f })
    foreach (float v in new[] { 0f, .000001f, .1f, .5f, .9f, .999999f })
        if (!Matches(new Vector3(1 + u, 20, 1 + v), -Vector3.UnitY))
            throw new InvalidOperationException("Batched fan changed a triangle interior, shared edge or center epsilon.");
    foreach (Vector3 direction in new[]
    {
        Vector3.Normalize(new Vector3(1f, -2f, .3f)),
        Vector3.Normalize(new Vector3(-.3f, -2f, -1f)),
        Vector3.Normalize(new Vector3(1f, -.01f, 1f)),
        Vector3.UnitY, Vector3.UnitX, Vector3.Zero
    })
    foreach (Vector3 origin in new[]
    {
        new Vector3(-1f, 3f, .5f), new Vector3(3.000001f, 3f, 1.5f),
        new Vector3(.5f, -8f, .5f), new Vector3(1.5f, 12f, 1.5f),
        new Vector3(float.NaN, 1f, 1f)
    })
        if (!Matches(origin, direction))
            throw new InvalidOperationException("Batched fan changed oblique, below-surface, outside-map or invalid-ray behavior.");
}
Check(fanComparisons>300,"batched and scalar fans preserve every differential ray and surface edge case");

int scalarReads = 0, fanReads = 0;
var countFan = new TerrainGridPicker.SurfaceFan(1f, 1f, 1f, 1f, 2f);
Check(TerrainGridPicker.TryPick(new Vector3(.25f, 10f, .75f), -Vector3.UnitY,
    1, 1, Flat, out var scalarCountHit, surfaceHeightAt: (_, _, u, v) =>
    { scalarReads++; return Vertex(countFan, u, v); }), "scalar count fixture hits land");
Check(TerrainGridPicker.TryPick(new Vector3(.25f, 10f, .75f), -Vector3.UnitY,
    1, 1, Flat, out var fanCountHit,
    surfaceHeightAt: (_, _, _, _) => throw new InvalidOperationException("Scalar path used despite supplied fan."),
    surfaceFanAt: (_, _) => { fanReads++; return countFan; }) &&
    scalarCountHit == fanCountHit && scalarReads == 5 && fanReads == 1,
    "one fan acquisition replaces five scalar calls without changing the hit");

int traversedCells = 0, acquiredFans = 0;
_ = TerrainGridPicker.TryPick(new Vector3(-1, 120, .5f),
    Vector3.Normalize(new Vector3(1, -.02f, 0)), 1000, 2,
    (_, _) => { traversedCells++; return .12f; }, out _,
    surfaceFanAt: (_, _) => { acquiredFans++; return fanCases[0]; });
Check(traversedCells < 1005 && acquiredFans == traversedCells,
    "fan acquisition stays once per crossed cell without changing DDA bounds");
Console.WriteLine($"PASS SINGLE-FAN PICKER: {fanComparisons} scalar-oracle rays; five-to-one sampling and unchanged traversal");
