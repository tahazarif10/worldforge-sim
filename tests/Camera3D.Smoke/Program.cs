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
