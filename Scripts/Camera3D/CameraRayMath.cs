using System;
using System.Numerics;

namespace WorldForge.Camera3D;

/// <summary>
/// Engine-free screen-ray/ground math. Ray direction can be orthographic
/// (identical direction for every pixel) or perspective (different direction).
/// </summary>
public static class CameraRayMath
{
    private const float Epsilon = 0.00001f;

    public static bool IntersectHorizontalPlane(
        Vector3 origin, Vector3 direction, float planeHeight, out Vector3 point)
    {
        point = default;
        if (!float.IsFinite(planeHeight) || !IsFinite(origin) || !IsFinite(direction) ||
            MathF.Abs(direction.Y) < Epsilon) return false;
        float distance = (planeHeight - origin.Y) / direction.Y;
        if (!float.IsFinite(distance) || distance < 0) return false;
        point = origin + direction * distance;
        return IsFinite(point);
    }

    /// <summary>
    /// Camera translation keeping a frozen world point beneath the mouse.
    /// Reproject at a FIXED height during a drag; never change anchor height
    /// when the pointer passes from ground to mountain.
    /// </summary>
    public static bool TryAnchorCorrection(Vector3 oldAnchor,
        Vector3 newRayOrigin, Vector3 newRayDirection, out Vector3 correction)
    {
        correction = default;
        if (!IntersectHorizontalPlane(newRayOrigin, newRayDirection, oldAnchor.Y,
            out Vector3 projected)) return false;
        correction = oldAnchor - projected;
        correction.Y = 0;
        return true;
    }

    public static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

/// <summary>
/// Deterministic hit against a grid of constant-height tile surfaces.
/// DDA visits cells along the XZ ray instead of sampling at every .22 units.
/// Handles vertical cliff entry as well as horizontal terrain tops.
/// The height supplier must be read-only and return positive finite heights.
/// </summary>
public static class TerrainGridPicker
{
    private const float Epsilon = 0.0001f;
    public readonly record struct Hit(int X, int Y, Vector3 Point);

    public static bool TryPick(Vector3 origin, Vector3 direction,
        int width, int height, Func<int, int, float> heightAt, out Hit hit,
        Func<int, int, float>? liftAt = null,
        Func<int, int, float, float, float>? surfaceHeightAt = null)
    {
        hit = default;
        if (width <= 0 || height <= 0 || !CameraRayMath.IsFinite(origin) ||
            !CameraRayMath.IsFinite(direction) || direction.LengthSquared() < 0.00000001f)
            return false;

        ArgumentNullException.ThrowIfNull(heightAt);
        const float maxDistance = 2000f;
        float start = 0, end = maxDistance;
        if (!ClipSlab(origin.X, direction.X, 0, width, ref start, ref end) ||
            !ClipSlab(origin.Z, direction.Z, 0, height, ref start, ref end) || end < start)
            return false;

        float t = MathF.Max(0, start);
        // Enter a non-boundary point to disambiguate the first cell.
        float sampleT = MathF.Min(end, t + Epsilon);
        Vector3 sample = origin + direction * sampleT;
        int x = Math.Clamp((int)MathF.Floor(sample.X), 0, width - 1);
        int z = Math.Clamp((int)MathF.Floor(sample.Z), 0, height - 1);
        int dx = Math.Sign(direction.X), dz = Math.Sign(direction.Z);

        float nextX = dx == 0 ? float.PositiveInfinity :
            ((dx > 0 ? x + 1 : x) - origin.X) / direction.X;
        float nextZ = dz == 0 ? float.PositiveInfinity :
            ((dz > 0 ? z + 1 : z) - origin.Z) / direction.Z;
        float deltaX = dx == 0 ? float.PositiveInfinity : MathF.Abs(1f / direction.X);
        float deltaZ = dz == 0 ? float.PositiveInfinity : MathF.Abs(1f / direction.Z);

        // A ray through a rectangular grid can cross at most W+H+2 cells.
        for (int steps = 0; steps < width + height + 4 && t <= end + Epsilon; steps++)
        {
            if (x < 0 || x >= width || z < 0 || z >= height) return false;
            float exit = MathF.Min(end, MathF.Min(nextX, nextZ));
            float surface = heightAt(x, z);
            if (!float.IsFinite(surface)) return false;

            float lift = liftAt?.Invoke(x, z) ?? 0f;
            if (TrySurfaceHit(origin, direction, x, z, surface, lift,
                t, exit, out Vector3 topPoint, surfaceHeightAt))
            {
                hit = new Hit(x, z, topPoint);
                return true;
            }

            // The ray entered this cell through an exposed high cliff wall.
            // Do not select cliffs if the ray entered from outside world
            // underneath the visible base plane.
            if (surfaceHeightAt is null && t >= 0 && t <= exit + Epsilon)
            {
                Vector3 entry = origin + direction * t;
                if (entry.Y >= 0.12f - Epsilon && entry.Y <= surface + Epsilon)
                {
                    hit = new Hit(x, z, entry);
                    return true;
                }
            }

            if (exit >= end - Epsilon) break;
            if (nextX <= nextZ)
            {
                x += dx;
                t = nextX;
                nextX += deltaX;
            }
            else
            {
                z += dz;
                t = nextZ;
                nextZ += deltaZ;
            }
        }
        return false;
    }

    private static bool TrySurfaceHit(
        Vector3 origin, Vector3 direction, int x, int z,
        float baseY, float centerLift, float firstT, float lastT,
        out Vector3 intersection,
        Func<int, int, float, float, float>? surfaceHeightAt)
    {
        intersection = default;
        // Keep original inexpensive flat pick when smooth terrain is off.
        if (surfaceHeightAt is null && centerLift <= 0.000001f)
        {
            if (MathF.Abs(direction.Y) < 0.000001f) return false;
            float t = (baseY - origin.Y) / direction.Y;
            if (t < MathF.Max(0f, firstT) - Epsilon ||
                t > lastT + Epsilon) return false;
            intersection = origin + direction * t;
            return true;
        }

        // Four triangles match VisualTerrain3D.AddTopFace EXACTLY:
        // a,b,mid; b,d,mid; d,c,mid; c,a,mid.
        float aY = surfaceHeightAt?.Invoke(x,z,0f,0f) ?? baseY;
        float bY = surfaceHeightAt?.Invoke(x,z,0f,1f) ?? baseY;
        float cY = surfaceHeightAt?.Invoke(x,z,1f,0f) ?? baseY;
        float dY = surfaceHeightAt?.Invoke(x,z,1f,1f) ?? baseY;
        float midY = surfaceHeightAt?.Invoke(x,z,.5f,.5f) ?? baseY + centerLift;
        Vector3 a = new(x, aY, z);
        Vector3 b = new(x, bY, z + 1);
        Vector3 c = new(x + 1, cY, z);
        Vector3 d = new(x + 1, dY, z + 1);
        Vector3 mid = new(x + .5f, midY, z + .5f);
        float best = float.PositiveInfinity;
        if (HitTriangle(origin,direction,a,b,mid,out float t0)) best = MathF.Min(best,t0);
        if (HitTriangle(origin,direction,b,d,mid,out float t1)) best = MathF.Min(best,t1);
        if (HitTriangle(origin,direction,d,c,mid,out float t2)) best = MathF.Min(best,t2);
        if (HitTriangle(origin,direction,c,a,mid,out float t3)) best = MathF.Min(best,t3);
        if (!float.IsFinite(best) || best < MathF.Max(0f, firstT) - Epsilon ||
            best > lastT + Epsilon) return false;
        intersection = origin + direction * best;
        return CameraRayMath.IsFinite(intersection);
    }

    // Two-sided Moller-Trumbore. No collision nodes or allocations per pick.
    private static bool HitTriangle(Vector3 origin, Vector3 direction,
        Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;
        Vector3 e1 = b - a, e2 = c - a;
        Vector3 p = Vector3.Cross(direction, e2);
        float determinant = Vector3.Dot(e1, p);
        if (MathF.Abs(determinant) < 0.000001f) return false;
        float inverse = 1f / determinant;
        Vector3 t = origin - a;
        float u = Vector3.Dot(t, p) * inverse;
        if (u < -Epsilon || u > 1f + Epsilon) return false;
        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < -Epsilon || u + v > 1f + Epsilon) return false;
        distance = Vector3.Dot(e2, q) * inverse;
        return float.IsFinite(distance) && distance >= -Epsilon;
    }

    private static bool ClipSlab(float coordinate, float direction,
        float minimum, float maximum, ref float start, ref float end)
    {
        if (MathF.Abs(direction) < 0.000001f)
            return coordinate >= minimum && coordinate < maximum;
        float a = (minimum - coordinate) / direction;
        float b = (maximum - coordinate) / direction;
        if (a > b) (a, b) = (b, a);
        start = MathF.Max(start, a);
        end = MathF.Min(end, b);
        return start <= end;
    }
}
