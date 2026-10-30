using System;
using WorldForge.Streaming;

static void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL " + label);
    Console.WriteLine("PASS " + label);
}

var small = VisibleChunkPlanner.Plan(120, 88, 16, 38, 38, 47, 47, marginChunks: 0);
Check(small.Count == 1 && small.Contains((2,2)), "single visible 16x16 chunk");
var border = VisibleChunkPlanner.Plan(120, 88, 16, 15, 15, 18, 18, marginChunks: 0);
Check(border.Count == 4 && border.Contains((0,0)) && border.Contains((1,1)),
    "frustum spanning chunk corner returns four chunks");
var margin = VisibleChunkPlanner.Plan(120, 88, 16, 38, 38, 47, 47, marginChunks: 1);
Check(margin.Count == 9 && margin.Contains((1,1)) && margin.Contains((3,3)),
    "one chunk safe margin around view");
var leftEdge = VisibleChunkPlanner.Plan(120, 88, 16, -100, -100, 4, 4);
Check(leftEdge.Contains((0,0)) && leftEdge.Count <= 4, "edge clipped safely");
Check(VisibleChunkPlanner.Plan(120, 88, 16, -300, -300, -50, -50).Count == 0,
    "totally offscreen camera requests zero chunks");
var huge = VisibleChunkPlanner.Plan(8192, 8192, 64, 4096, 4000, 4160, 4064, marginChunks: 1);
Check(huge.Count <= 16 && huge.Count >= 4, "8192x8192 world selects bounded local chunks");
Check(huge.Count < 100, "no full-world enumeration for huge map");
var end = VisibleChunkPlanner.Plan(8192, 8192, 64, 8170, 8170, 8200, 8200);
Check(end.Contains((127,127)) && end.Count <= 4, "last chunk included and clipped");
bool rejected = false;
try { _ = VisibleChunkPlanner.Plan(0, 100, 16, 0,0,1,1); }
catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "invalid dimensions rejected");
rejected = false;
try { _ = VisibleChunkPlanner.Plan(100, 100, 16, float.NaN,0,1,1); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "nonfinite camera rectangle rejected");
Console.WriteLine("PASS ALL viewport-driven chunk selection tests");
