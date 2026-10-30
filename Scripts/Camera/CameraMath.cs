using System;
using System.Numerics;

namespace WorldForge.Camera;

/// <summary>
/// Engine-independent camera calculations. Screen positions and viewport sizes are
/// in logical Godot viewport pixels; world positions use Node2D world units.
/// The 0.5x overview is the only fractional zoom; all close-up levels use
/// integer scaling to keep the nearest-neighbor pixel grid crisp.
/// </summary>
public static class CameraMath
{
    private static readonly float[] ZoomLevels = [0.5f, 1.0f, 2.0f, 3.0f, 4.0f];

    public static float NextZoom(float current, int direction)
    {
        if (direction > 0)
        {
            foreach (float level in ZoomLevels)
                if (level > current + 0.0001f) return level;
            return ZoomLevels[^1];
        }
        if (direction < 0)
        {
            for (int i = ZoomLevels.Length - 1; i >= 0; i--)
                if (ZoomLevels[i] < current - 0.0001f) return ZoomLevels[i];
            return ZoomLevels[0];
        }
        return current;
    }

    public static Vector2 WorldAtScreen(Vector2 center, Vector2 viewport, Vector2 screen, float zoom)
    {
        Validate(viewport, zoom);
        return center + (screen - viewport * 0.5f) / zoom;
    }

    public static Vector2 ZoomAroundCursor(
        Vector2 currentCenter, Vector2 viewport, Vector2 cursor, float oldZoom,
        float newZoom, Vector2 world)
    {
        Validate(viewport, oldZoom);
        Validate(viewport, newZoom);
        Vector2 mouseWorld = WorldAtScreen(currentCenter, viewport, cursor, oldZoom);
        Vector2 newCenter = mouseWorld - (cursor - viewport * 0.5f) / newZoom;
        return SnapAndClamp(newCenter, viewport, newZoom, world);
    }

    /// <summary>Positive screen drag moves the terrain with the pointer.</summary>
    public static Vector2 PanByScreenDelta(Vector2 center, Vector2 screenDelta, Vector2 viewport,
        float zoom, Vector2 world)
    {
        Validate(viewport, zoom);
        return SnapAndClamp(center - screenDelta / zoom, viewport, zoom, world);
    }

    public static Vector2 SnapAndClamp(Vector2 center, Vector2 viewport, float zoom, Vector2 world)
    {
        Validate(viewport, zoom);
        if (world.X <= 0 || world.Y <= 0) throw new ArgumentOutOfRangeException(nameof(world));
        // One display pixel is 1/zoom world units. Snapping the camera center
        // avoids sub-pixel shimmer while panning across original pixel art.
        float step = 1f / zoom;
        Vector2 snapped = new(MathF.Round(center.X / step) * step, MathF.Round(center.Y / step) * step);
        return ClampCenter(snapped, viewport, zoom, world);
    }

    public static Vector2 ClampCenter(Vector2 center, Vector2 viewport, float zoom, Vector2 world)
    {
        Validate(viewport, zoom);
        if (world.X <= 0 || world.Y <= 0) throw new ArgumentOutOfRangeException(nameof(world));
        Vector2 halfView = viewport / (2f * zoom);
        return new Vector2(
            ClampAxis(center.X, halfView.X, world.X),
            ClampAxis(center.Y, halfView.Y, world.Y));
    }

    private static float ClampAxis(float center, float visibleHalfSpan, float worldSpan)
    {
        if (visibleHalfSpan * 2 >= worldSpan) return worldSpan * 0.5f;
        return Math.Clamp(center, visibleHalfSpan, worldSpan - visibleHalfSpan);
    }

    private static void Validate(Vector2 viewport, float zoom)
    {
        if (!float.IsFinite(zoom) || zoom <= 0 || viewport.X <= 0 || viewport.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Viewport and zoom must be positive.");
    }
}
