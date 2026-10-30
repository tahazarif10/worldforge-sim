using System;
using Godot;
using Num3 = System.Numerics.Vector3;

namespace WorldForge.Camera3D;

/// <summary>
/// Interactive strategy camera: anchor position via actual camera rays.
/// The viewport is the only source of screen resolution, eliminating the
/// unequal vertical/horizontal drag speeds of flattened camera basis vectors.
/// </summary>
public sealed class StrategyCameraRig
{
    private readonly Godot.Camera3D _camera;
    private readonly Func<int, int, float> _heightAt;
    private readonly Func<int, int, float> _liftAt;
    private readonly Func<bool> _smoothEnabled;
    private readonly Func<int,int,float,float,float>? _sampleSurface;
    private int _width, _height;
    private Vector3 _pivot;
    private Vector3 _dragAnchor;
    private bool _dragging;

    private const float MinimumSize = 19f;
    private const float MaximumSize = 160f;
    private const float KeyboardPixelsPerSecond = 520f;
    private float _size = 79f;

    public StrategyCameraRig(Godot.Camera3D camera, Func<int, int, float> heightAt,
        int worldWidth, int worldHeight, Func<int, int, float>? liftAt = null,
        Func<bool>? smoothEnabled = null,
        Func<int,int,float,float,float>? sampleSurface = null)
    {
        _camera = camera;
        _heightAt = heightAt;
        _liftAt = liftAt ?? ((_, _) => 0f);
        _smoothEnabled = smoothEnabled ?? (() => false);
        _sampleSurface = sampleSurface;
        ResetWorld(worldWidth, worldHeight);
    }

    public void ResetWorld(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        _width = width;
        _height = height;
        _dragging = false;
        _pivot = new Vector3(width * 0.5f, 0, height * 0.5f);
        RefreshCamera();
    }

    public void BeginDrag(Vector2 screen)
    {
        float elevation = TryPickTerrain(screen, out var hit) ? hit.Point.Y : 0.12f;
        _dragging = RayOnPlane(screen, elevation, out _dragAnchor);
    }

    /// <summary>Explicit navigation to distant logical map coordinates
    /// without allocating intervening world regions or scene chunks.</summary>
    public void FocusAt(float x, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(x));
        _dragging = false;
        _pivot = new Vector3(x, 0, z);
        ClampFocus();
        RefreshCamera();
    }

    public void EndDrag() => _dragging = false;

    public void DragTo(Vector2 screen)
    {
        if (!_dragging) return;
        if (!RayOnPlane(screen, _dragAnchor.Y, out Vector3 rayPoint)) return;
        Vector3 correction = _dragAnchor - rayPoint;
        _pivot += new Vector3(correction.X, 0, correction.Z);
        ClampFocus();
        RefreshCamera();
    }

    public void ZoomAt(Vector2 screen, float scale)
    {
        float next = Mathf.Clamp(_size * scale, MinimumSize, MaximumSize);
        if (Mathf.Abs(next - _size) < 0.00001f) return;

        float elevation = TryPickTerrain(screen, out var hit) ? hit.Point.Y : 0.12f;
        bool hasAnchor = RayOnPlane(screen, elevation, out Vector3 anchor);

        _size = next;
        RefreshCamera();
        if (hasAnchor && RayOnPlane(screen, elevation, out Vector3 current))
        {
            _pivot += new Vector3(anchor.X - current.X, 0, anchor.Z - current.Z);
            ClampFocus();
            RefreshCamera();
        }
    }

    public void PanByKeyboard(Vector2 direction, double delta)
    {
        if (direction.LengthSquared() < 0.0001f || delta <= 0) return;
        Vector2 center = _camera.GetViewport().GetVisibleRect().Size / 2f;
        Vector2 screenDelta = direction.Normalized() *
                              KeyboardPixelsPerSecond * (float)Math.Min(delta, 0.1);
        if (RayOnPlane(center, 0.12f, out Vector3 basePoint) &&
            RayOnPlane(center + screenDelta, 0.12f, out Vector3 moved))
        {
            _pivot += new Vector3(moved.X - basePoint.X, 0, moved.Z - basePoint.Z);
            ClampFocus();
            RefreshCamera();
        }
    }

    public bool TryPickTerrain(Vector2 screen, out TerrainGridPicker.Hit hit)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screen);
        Vector3 direction = _camera.ProjectRayNormal(screen);
        return TerrainGridPicker.TryPick(ToNum(origin), ToNum(direction),
            _width, _height, _heightAt, out hit, _liftAt,
            _smoothEnabled() ? _sampleSurface : null);
    }

    private bool RayOnPlane(Vector2 screen, float elevation, out Vector3 result)
    {
        bool valid = CameraRayMath.IntersectHorizontalPlane(
            ToNum(_camera.ProjectRayOrigin(screen)),
            ToNum(_camera.ProjectRayNormal(screen)), elevation, out Num3 position);
        result = valid ? new Vector3(position.X, position.Y, position.Z) : default;
        return valid;
    }

    private void ClampFocus()
    {
        // Finite small lab; avoid blank over-pan. Large-world camera will use
        // a logical int64 pivot and view-driven streaming instead.
        _pivot.X = Mathf.Clamp(_pivot.X, 0, _width);
        _pivot.Z = Mathf.Clamp(_pivot.Z, 0, _height);
    }

    private void RefreshCamera()
    {
        _camera.Size = _size;
        _camera.Position = _pivot + new Vector3(1f, 1.20f, 1f).Normalized() * 155f;
        _camera.LookAt(_pivot, Vector3.Up);
    }

    private static Num3 ToNum(Vector3 v) => new(v.X, v.Y, v.Z);
}
