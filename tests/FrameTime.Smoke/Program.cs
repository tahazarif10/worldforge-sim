using System;
using WorldForge.Instrumentation;

static void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
var probe = new FrameTimeProbe(16);
Check(probe.Summarize().Count == 0, "empty sample summary");
for (int i = 1; i <= 16; i++) probe.Record(i);
var s = probe.Summarize();
Check(s.Count == 16 && Math.Abs(s.AverageMs - 8.5) < .00001,
    "mean frame time");
Check(s.P95Ms == 16 && s.P99Ms == 16 && s.MaxMs == 16,
    "nearest-rank p95 p99");
probe.Record(50);
Check(probe.Count == 16 && probe.Snapshot()[0] == 2 &&
      probe.Snapshot()[15] == 50, "ring buffer evicts oldest sample");
probe.Record(double.NaN);
probe.Record(-1);
Check(probe.Count == 16 && probe.Summarize().MaxMs == 50,
    "invalid frame intervals rejected");
string csv = probe.ExportCsv();
Check(csv.Contains("p95_ms=") && csv.Contains("p99_ms=") &&
      csv.Contains("sample,frame_ms") && csv.Contains("15,50.0000"),
    "CSV includes samples and measured percentiles");
Console.WriteLine("PASS ALL frame sampling and percentile tests");
