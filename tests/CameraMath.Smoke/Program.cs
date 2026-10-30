using System.Numerics;
using WorldForge.Camera;

static void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

static void Near(Vector2 actual, Vector2 expected, string name, float epsilon = 0.51f)
{
    Check(Vector2.Distance(actual, expected) <= epsilon,
        $"{name}: actual={actual} expected={expected}");
}

Vector2 viewport = new(1280, 800);
Vector2 world = new(4000, 3000);
Vector2 center = new(2000, 1500);
Vector2 cursor = new(1100, 550);

Check(CameraMath.NextZoom(1f, 1) == 2f, "wheel up advances integer zoom level");
Check(CameraMath.NextZoom(3f, -1) == 2f, "wheel down decrements integer zoom level");
Check(CameraMath.NextZoom(4f, 1) == 4f, "max zoom clamped");
Check(CameraMath.NextZoom(0.5f, -1) == 0.5f, "min zoom clamped");

Vector2 before = CameraMath.WorldAtScreen(center, viewport, cursor, 1f);
Vector2 zoomed = CameraMath.ZoomAroundCursor(center, viewport, cursor, 1f, 2f, world);
Vector2 after = CameraMath.WorldAtScreen(zoomed, viewport, cursor, 2f);
Near(after, before, "zoom preserves world coordinate beneath cursor");

Vector2 pan = CameraMath.PanByScreenDelta(center, new Vector2(100, 40), viewport, 2f, world);
Near(pan, center - new Vector2(50, 20), "drag measured in screen pixels");

Vector2 low = CameraMath.ClampCenter(new Vector2(-10000, -10000), viewport, 1f, world);
Near(low, new Vector2(640, 400), "camera stays inside min world boundary", 0.01f);

Vector2 high = CameraMath.ClampCenter(new Vector2(10000, 10000), viewport, 1f, world);
Near(high, new Vector2(3360, 2600), "camera stays inside max world boundary", 0.01f);

Vector2 smallWorld = new(700, 500);
Near(CameraMath.ClampCenter(new Vector2(-1000, 7000), viewport, 0.5f, smallWorld),
     smallWorld / 2, "small world centered when viewport exceeds map", 0.01f);

Near(CameraMath.ZoomAroundCursor(center, viewport, cursor, 1f, 2f, world),
     CameraMath.ZoomAroundCursor(center, viewport, cursor, 1f, 2f, world),
     "same inputs are deterministic", 0f);

Console.WriteLine("PASS ALL camera math smoke tests");
