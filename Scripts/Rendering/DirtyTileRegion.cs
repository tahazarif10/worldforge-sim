using System;

namespace WorldForge.Rendering;

/// <summary>
/// Engine-independent, inclusive dirty-tile bounding rectangle.
/// Multiple brush events are coalesced into one CPU raster update per frame.
/// A rectangle may contain unaffected pixels; this is deliberate and bounded.
/// </summary>
public sealed class DirtyTileRegion
{
    private readonly int _width;
    private readonly int _height;
    private bool _dirty;
    private int _left, _top, _right, _bottom;

    public DirtyTileRegion(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        _width = width;
        _height = height;
    }

    public bool HasPending => _dirty;

    public void MarkAll() => MarkRectangle(0, 0, _width - 1, _height - 1);

    public void MarkCircle(int cx, int cy, int radius)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        // Conservative bounding square: never underreports tiles affected by a circle.
        long left = (long)cx - radius, top = (long)cy - radius;
        long right = (long)cx + radius, bottom = (long)cy + radius;
        MarkRectangle(
            (int)Math.Clamp(left, int.MinValue, int.MaxValue),
            (int)Math.Clamp(top, int.MinValue, int.MaxValue),
            (int)Math.Clamp(right, int.MinValue, int.MaxValue),
            (int)Math.Clamp(bottom, int.MinValue, int.MaxValue));
    }

    public void MarkRectangle(int left, int top, int right, int bottom)
    {
        if (right < 0 || bottom < 0 || left >= _width || top >= _height ||
            left > right || top > bottom) return;

        left = Math.Clamp(left, 0, _width - 1);
        top = Math.Clamp(top, 0, _height - 1);
        right = Math.Clamp(right, 0, _width - 1);
        bottom = Math.Clamp(bottom, 0, _height - 1);

        if (!_dirty)
        {
            _left = left;
            _top = top;
            _right = right;
            _bottom = bottom;
            _dirty = true;
        }
        else
        {
            _left = Math.Min(_left, left);
            _top = Math.Min(_top, top);
            _right = Math.Max(_right, right);
            _bottom = Math.Max(_bottom, bottom);
        }
    }

    public bool TryConsume(out TileBounds bounds)
    {
        if (!_dirty)
        {
            bounds = default;
            return false;
        }
        bounds = new TileBounds(_left, _top, _right, _bottom);
        _dirty = false;
        return true;
    }
}

public readonly record struct TileBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left + 1;
    public int Height => Bottom - Top + 1;
    public int Area => Width * Height;
}
