using WorldForge.Rendering;

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

var dirty = new DirtyTileRegion(160, 120);
Check(!dirty.HasPending, "fresh region clean");
Check(!dirty.TryConsume(out _), "no initial region");
dirty.MarkCircle(40, 30, 3);
Check(dirty.TryConsume(out TileBounds small) && small == new TileBounds(37, 27, 43, 33),
      "brush marks local 7x7 bounding box");
Check(small.Area == 49, "brush area bounded not full 19200 pixels");
Check(!dirty.TryConsume(out _), "flush consumes pending region only once");

dirty.MarkCircle(1, 1, 4);
Check(dirty.TryConsume(out TileBounds clipped) &&
      clipped == new TileBounds(0, 0, 5, 5), "clip circle to top left");
dirty.MarkCircle(158, 119, 5);
Check(dirty.TryConsume(out TileBounds edge) &&
      edge == new TileBounds(153, 114, 159, 119), "clip circle to bottom right");

dirty.MarkCircle(22, 20, 2);
dirty.MarkCircle(27, 24, 2);
Check(dirty.TryConsume(out TileBounds merged) &&
      merged == new TileBounds(20, 18, 29, 26), "merge multiple input events into one update");

dirty.MarkCircle(-100, -100, 2);
Check(!dirty.HasPending, "ignore outside-world input");
dirty.MarkAll();
Check(dirty.TryConsume(out TileBounds full) &&
      full == new TileBounds(0, 0, 159, 119) && full.Area == 19200,
      "new world refreshes entire map");
Check(!dirty.HasPending, "full refresh consumed");
Check(!new DirtyTileRegion(1, 1).TryConsume(out _), "1x1 map stays valid");

Console.WriteLine("PASS ALL dirty-region smoke tests");
