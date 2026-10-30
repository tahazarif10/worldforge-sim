using System;
using Godot;
using Vec2 = System.Numerics.Vector2;

namespace WorldForge.Camera;

/// <summary>
/// Godot adapter for deterministic CameraMath. No world simulation changes.
/// Mouse-based zoom stays focused on the pointed-at tile unless a world edge
/// prevents the camera from moving farther.
/// </summary>
public sealed class WorldCameraController
{
    private readonly Camera2D _camera;
    private readonly Viewport _viewport;
    private readonly Vec2 _world;
    private const float KeyboardPanPixelsPerSecond = 550f;

    public WorldCameraController(Camera2D camera, Viewport viewport, Vector2 worldSize)
    {
        _camera = camera;
        _viewport = viewport;
        _world = ToNumerics(worldSize);
        _camera.PositionSmoothingEnabled = false;
        _camera.Zoom = Vector2.One;
        _camera.Position = ToGodot(CameraMath.ClampCenter(
            _world / 2f, ViewportSize, _camera.Zoom.X, _world));
    }

    private Vec2 ViewportSize => ToNumerics(_viewport.GetVisibleRect().Size);

    public void ZoomAtScreen(Vector2 screenPoint, int direction)
    {
        float oldZoom = _camera.Zoom.X;
        float nextZoom = CameraMath.NextZoom(oldZoom, direction);
        if (Math.Abs(oldZoom - nextZoom) < 0.0001f) return;
        Vec2 nextCenter = CameraMath.ZoomAroundCursor(
            ToNumerics(_camera.Position), ViewportSize, ToNumerics(screenPoint),
            oldZoom, nextZoom, _world);
        _camera.Zoom = Vector2.One * nextZoom;
        _camera.Position = ToGodot(nextCenter);
    }

    public void DragByPixels(Vector2 screenDelta) =>
        MoveByPixels(screenDelta);

    public void PanByKeyboard(Vector2 direction, double delta)
    {
        if (direction == Vector2.Zero) return;
        // Keyboard motion is the inverse of dragging the terrain.
        MoveByPixels(-direction.Normalized() * KeyboardPanPixelsPerSecond * (float)delta);
    }

    public void KeepInBounds()
    {
        _camera.Position = ToGodot(CameraMath.SnapAndClamp(
            ToNumerics(_camera.Position), ViewportSize, _camera.Zoom.X, _world));
    }

    private void MoveByPixels(Vector2 screenDelta)
    {
        _camera.Position = ToGodot(CameraMath.PanByScreenDelta(
            ToNumerics(_camera.Position), ToNumerics(screenDelta),
            ViewportSize, _camera.Zoom.X, _world));
    }

    private static Vec2 ToNumerics(Vector2 value) => new(value.X, value.Y);
    private static Vector2 ToGodot(Vec2 value) => new(value.X, value.Y);
}
